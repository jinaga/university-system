using Nest;
using Serilog;
using System.Diagnostics;

namespace University.Indexer.Elasticsearch;

public class BulkIndexResult
{
    public bool IsValid { get; set; }
    public bool HasErrors { get; set; }
    public IReadOnlyCollection<BulkError> Errors { get; set; } = Array.Empty<BulkError>();
}

public class BulkError
{
    public string Id { get; set; }
    public string Error { get; set; }
}

public class ElasticsearchClientProxy
{
    private readonly ElasticClient client;
    private readonly ILogger _logger;
    private readonly ActivitySource _activitySource;

    public ElasticsearchClientProxy(string elasticsearchUrl, ILogger logger, ActivitySource activitySource)
    {
        _logger = logger;
        _activitySource = activitySource;
        var settings = new ConnectionSettings(new Uri(elasticsearchUrl))
            .DefaultIndex("offerings");
        client = new ElasticClient(settings);
    }

    public async Task Initialize()
    {
        using var activity = _activitySource.StartActivity("ElasticsearchInitialize");
        
        // Ensure index exists with proper mappings
        var existsResponse = await client.IndexExistsAsync("offerings");
        if (!existsResponse.Exists)
        {
            _logger.Information("Creating offerings index in Elasticsearch");
            activity?.SetTag("creatingIndex", true);
            
            await client.CreateIndexAsync("offerings", c => c
                .Mappings(m => m
                    .Map<SearchRecord>(mm => mm
                        .Properties(p => p
                            .Keyword(k => k.Name(n => n.Id))
                            .Keyword(k => k.Name(n => n.CourseCode))
                            .Text(t => t.Name(n => n.CourseName))
                            .Keyword(k => k.Name(n => n.Days))
                            .Keyword(k => k.Name(n => n.Time))
                            .Keyword(k => k.Name(n => n.Instructor))
                            .Keyword(k => k.Name(n => n.Location))
                        )
                    )
                )
            );
            _logger.Information("Successfully created offerings index");
        }
        else
        {
            activity?.SetTag("indexExists", true);
            _logger.Debug("Offerings index already exists in Elasticsearch");
        }
    }

    private async Task<T> ExecuteWithRetry<T>(Func<Task<T>> action)
    {
        int retryCount = 0;
        const int maxRetries = 10;
        const int delayMilliseconds = 200;

        while (retryCount < maxRetries)
        {
            try
            {
                var response = await action();
                return response;
            }
            catch (Exception)
            {
                // Retry
            }
            retryCount++;
            await Task.Delay(delayMilliseconds * (int)Math.Pow(2, retryCount));
        }

        return default;
    }

    public async Task<bool> IndexRecord(SearchRecord searchRecord)
    {
        using var activity = _activitySource.StartActivity("IndexRecord");
        activity?.SetTag("courseCode", searchRecord.CourseCode);
        activity?.SetTag("courseName", searchRecord.CourseName);
        
        var success = await ExecuteWithRetry(async () =>
        {
            var updateResponse = await client.UpdateAsync<SearchRecord, SearchRecord>(DocumentPath<SearchRecord>.Id(searchRecord.Id), u => u
                .Index("offerings")
                .Doc(searchRecord)
                .DocAsUpsert(true)
            );
            return updateResponse.IsValid;
        });
        
        if (!success)
        {
            _logger.Error("Failed to index course {CourseCode} {CourseName}", searchRecord.CourseCode, searchRecord.CourseName);
            activity?.SetStatus(ActivityStatusCode.Error, $"Failed to index course {searchRecord.CourseCode}");
        }
        else
        {
            _logger.Debug("Successfully indexed course {CourseCode}", searchRecord.CourseCode);
        }
        
        return success;
    }

    public async Task<bool> UpdateRecordTime(string id, string days, string time)
    {
        var success = await ExecuteWithRetry(async () =>
        {
            var updateResponse = await client.UpdateAsync<SearchRecord, object>(DocumentPath<SearchRecord>.Id(id), u => u
                .Index("offerings")
                .Doc(new
                {
                    Days = days,
                    Time = time
                })
            );
            return updateResponse.IsValid;
        });
        if (!success)
        {
            _logger.Error("Failed to update time for course {Id}", id);
        }
        return success;
    }

    public async Task<bool> UpdateRecordLocation(string id, string building, string room)
    {
        var success = await ExecuteWithRetry(async () =>
        {
            var updateResponse = await client.UpdateAsync<SearchRecord, object>(DocumentPath<SearchRecord>.Id(id), u => u
                .Index("offerings")
                .Doc(new
                {
                    Location = $"{building} {room}"
                })
            );
            return updateResponse.IsValid;
        });
        if (!success)
        {
            _logger.Error("Failed to update location for course {Id}", id);
        }
        return success;
    }

    public async Task<bool> UpdateRecordInstructor(string id, string instructor)
    {
        var success = await ExecuteWithRetry(async () =>
        {
            var updateResponse = await client.UpdateAsync<SearchRecord, object>(DocumentPath<SearchRecord>.Id(id), u => u
                .Index("offerings")
                .Doc(new
                {
                    Instructor = instructor
                })
            );
            return updateResponse.IsValid;
        });
        if (!success)
        {
            _logger.Error("Failed to update instructor for course {Id}", id);
        }
        return success;
    }

    /// <summary>
    /// Indexes multiple search records in a single bulk operation.
    /// </summary>
    /// <param name="searchRecords">The collection of search records to index.</param>
    /// <returns>A result object containing information about the bulk operation.</returns>
    public async Task<BulkIndexResult> IndexManyRecords(IEnumerable<SearchRecord> searchRecords)
    {
        using var activity = _activitySource.StartActivity("IndexManyRecords");
        var recordsList = searchRecords.ToList();
        activity?.SetTag("recordCount", recordsList.Count);
        
        _logger.Debug("Bulk indexing {Count} records", recordsList.Count);
        return await BulkIndexRecords(recordsList);
    }

    /// <summary>
    /// Indexes multiple search records in a single bulk operation with custom configuration.
    /// </summary>
    /// <param name="searchRecords">The collection of search records to index.</param>
    /// <param name="configureBulk">Optional action to configure the bulk descriptor.</param>
    /// <returns>A result object containing information about the bulk operation.</returns>
    public async Task<BulkIndexResult> BulkIndexRecords(
        IEnumerable<SearchRecord> searchRecords,
        Action<BulkDescriptor> configureBulk = null)
    {
        using var activity = _activitySource.StartActivity("BulkIndexRecords");
        var recordsList = searchRecords.ToList();
        activity?.SetTag("recordCount", recordsList.Count);
        
        var stopwatch = Stopwatch.StartNew();
        
        var result = await ExecuteWithRetry(async () =>
        {
            var bulkDescriptor = new BulkDescriptor().Index("offerings");
            
            // Add each record to the bulk descriptor
            foreach (var record in recordsList)
            {
                bulkDescriptor.Update<SearchRecord>(u => u
                    .Id(record.Id)
                    .Doc(record)
                    .DocAsUpsert(true)
                );
            }
            
            // Apply custom configuration if provided
            configureBulk?.Invoke(bulkDescriptor);
            
            // Execute the bulk operation
            var bulkResponse = await client.BulkAsync(bulkDescriptor);
            
            // Create result object
            var bulkResult = new BulkIndexResult
            {
                IsValid = bulkResponse.IsValid,
                HasErrors = bulkResponse.Errors
            };
            
            // Process errors if any
            if (bulkResponse.Errors)
            {
                var errors = new List<BulkError>();
                foreach (var itemWithError in bulkResponse.ItemsWithErrors)
                {
                    errors.Add(new BulkError
                    {
                        Id = itemWithError.Id,
                        Error = itemWithError.Error?.Reason ?? "Unknown error"
                    });
                    
                    _logger.Error("Failed to index course {Id}: {Error}", 
                        itemWithError.Id, itemWithError.Error?.Reason);
                }
                bulkResult.Errors = errors;
                
                activity?.SetStatus(ActivityStatusCode.Error, $"Bulk indexing completed with {errors.Count} errors");
            }
            
            return bulkResult;
        });
        
        stopwatch.Stop();
        activity?.SetTag("durationMs", stopwatch.ElapsedMilliseconds);
        
        if (result != null)
        {
            _logger.Information("Bulk indexing completed in {ElapsedMilliseconds}ms with {ErrorCount} errors", 
                stopwatch.ElapsedMilliseconds, 
                result.HasErrors ? result.Errors.Count : 0);
        }
        else
        {
            _logger.Error("Bulk indexing failed after retries");
            activity?.SetStatus(ActivityStatusCode.Error, "Bulk indexing failed after retries");
        }
        
        return result ?? new BulkIndexResult { IsValid = false, HasErrors = true };
    }
}
