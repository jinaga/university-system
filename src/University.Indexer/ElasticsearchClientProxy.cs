using Nest;

namespace University.Indexer
{
    public class ElasticsearchClientProxy
    {
        private readonly ElasticClient client;

        public ElasticsearchClientProxy(string elasticsearchUrl)
        {
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
                IIndexResponse indexResponse = await client.IndexDocumentAsync(searchRecord);
                return indexResponse.IsValid;
            });
            if (!success)
            {
                Console.WriteLine($"Failed to index {searchRecord.CourseCode} {searchRecord.CourseName}");
            }
            return success;
        }

        public async Task<bool> UpdateRecord(SearchRecord searchRecord)
        {
            var success = await ExecuteWithRetry(async () =>
            {
                IUpdateResponse<SearchRecord> updateResponse = await client.UpdateAsync<SearchRecord, SearchRecord>(searchRecord.Id, u => u.Doc(searchRecord));
                return updateResponse.IsValid;
            });
            if (!success)
            {
                Console.WriteLine($"Failed to update {searchRecord.CourseCode} {searchRecord.CourseName}");
            }
            return success;
        }
    }
}
