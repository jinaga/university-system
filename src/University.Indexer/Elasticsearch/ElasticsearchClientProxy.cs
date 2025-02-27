using Nest;
using Serilog;
using System.Collections.Generic;

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

    public ElasticsearchClientProxy(string elasticsearchUrl, ILogger logger)
    {
        _logger = logger;
        var settings = new ConnectionSettings(new Uri(elasticsearchUrl))
            .DefaultIndex("offerings");
        client = new ElasticClient(settings);
    }

    public async Task Initialize()
    {
        // Ensure index exists with proper mappings
        var existsResponse = await client.IndexExistsAsync("offerings");
        if (!existsResponse.Exists)
        {
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
        var success = await ExecuteWithRetry(async () =>
        {
            IIndexResponse indexResponse = await client.IndexAsync(searchRecord, i => i.Id(searchRecord.Id));
            return indexResponse.IsValid;
        });
        if (!success)
        {
            _logger.Error("Failed to index course {CourseCode} {CourseName}", searchRecord.CourseCode, searchRecord.CourseName);
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
    public async Task<BulkIndexResult> IndexManyRecordsAsync(IEnumerable<SearchRecord> searchRecords)
    {
        return await BulkIndexRecordsAsync(searchRecords);
    }

    /// <summary>
    /// Indexes multiple search records in a single bulk operation with custom configuration.
    /// </summary>
    /// <param name="searchRecords">The collection of search records to index.</param>
    /// <param name="configureBulk">Optional action to configure the bulk descriptor.</param>
    /// <returns>A result object containing information about the bulk operation.</returns>
    public async Task<BulkIndexResult> BulkIndexRecordsAsync(
        IEnumerable<SearchRecord> searchRecords,
        Action<BulkDescriptor> configureBulk = null)
    {
        var result = await ExecuteWithRetry(async () =>
        {
            var bulkDescriptor = new BulkDescriptor().Index("offerings");
            
            // Add each record to the bulk descriptor
            foreach (var record in searchRecords)
            {
                bulkDescriptor.Index<SearchRecord>(i => i
                    .Id(record.Id)
                    .Document(record)
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
            }
            
            return bulkResult;
        });
        
        return result ?? new BulkIndexResult { IsValid = false, HasErrors = true };
    }
}
