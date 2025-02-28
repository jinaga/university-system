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
    public string Id { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
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

    private async Task<T?> ExecuteWithRetry<T>(Func<Task<T>> action)
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
        Action<BulkDescriptor>? configureBulk = null)
    {
        using var activity = _activitySource.StartActivity("BulkIndexRecords");
        var recordsList = searchRecords.ToList();
        activity?.SetTag("recordCount", recordsList.Count);
        
        var stopwatch = Stopwatch.StartNew();
        
        // Check cluster health before attempting bulk operation
        var healthResponse = await client.ClusterHealthAsync();
        if (healthResponse.IsValid)
        {
            activity?.SetTag("clusterStatus", healthResponse.Status.ToString());
            activity?.SetTag("activeShards", healthResponse.ActiveShards);
            activity?.SetTag("unassignedShards", healthResponse.UnassignedShards);
            
            _logger.Debug("Cluster health before bulk operation: Status={Status}, UnassignedShards={UnassignedShards}", 
                healthResponse.Status, healthResponse.UnassignedShards);
            
            // If there are unassigned shards, try to reallocate them
            if (healthResponse.UnassignedShards > 0)
            {
                _logger.Warning("Detected {UnassignedShards} unassigned shards before bulk operation", 
                    healthResponse.UnassignedShards);
                await ElasticsearchUtils.ReallocateUnassignedShards(client, _logger);
            }
        }
        
        // Check if the index is in read-only mode and try to clear it
        bool readOnlyCleared = await ElasticsearchUtils.CheckAndClearReadOnlyFlag(client, "offerings", _logger);
        if (!readOnlyCleared)
        {
            _logger.Warning("Index may still be in read-only mode. Proceeding with bulk operation anyway...");
        }
        
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
                    var errorReason = itemWithError.Error?.Reason ?? "Unknown error";
                    var errorType = itemWithError.Error?.Type ?? "Unknown";
                    
                    errors.Add(new BulkError
                    {
                        Id = itemWithError.Id,
                        Error = errorReason
                    });
                    
                    _logger.Error("Failed to index course {Id}: {ErrorType} - {ErrorReason}", 
                        itemWithError.Id, errorType, errorReason);
                    
                    // Check for read-only errors specifically
                    if (errorType == "cluster_block_exception" && errorReason.Contains("read_only"))
                    {
                        _logger.Warning("Detected read-only error during bulk operation. Attempting to clear...");
                        await ElasticsearchUtils.CheckAndClearReadOnlyFlag(client, "offerings", _logger);
                    }
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
            
            // As a last resort, try to clear read-only flag and reallocate shards
            await ElasticsearchUtils.CheckAndClearReadOnlyFlag(client, "offerings", _logger);
            await ElasticsearchUtils.ReallocateUnassignedShards(client, _logger);
        }
        
        return result ?? new BulkIndexResult { IsValid = false, HasErrors = true, Errors = Array.Empty<BulkError>() };
    }
}
