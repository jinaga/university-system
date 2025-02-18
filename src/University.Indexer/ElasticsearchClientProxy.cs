using Nest;
using Serilog;

namespace University.Indexer
{
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
    }
}
