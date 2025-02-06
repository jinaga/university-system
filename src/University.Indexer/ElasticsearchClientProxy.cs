using System;
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

        public async Task<bool> IndexRecord(SearchRecord searchRecord)
        {
            int retryCount = 0;
            const int maxRetries = 10;
            const int delayMilliseconds = 200;

            while (retryCount < maxRetries)
            {
                try
                {
                    var response = await client.IndexDocumentAsync(searchRecord);
                    if (response.IsValid)
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    // Retry
                }
                retryCount++;
                await Task.Delay(delayMilliseconds * (int)Math.Pow(2, retryCount));
            }

            Console.WriteLine($"Failed to index {searchRecord.CourseCode} after {maxRetries} attempts.");
            return false;
        }
    }
}
