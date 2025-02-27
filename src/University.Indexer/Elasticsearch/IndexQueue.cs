using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Jinaga;
using Serilog;
using University.Model;

namespace University.Indexer.Elasticsearch
{
    public class IndexQueue
    {
        private readonly JinagaClient jinagaClient;
        private readonly Lock lockObject = new Lock();
        private readonly ElasticsearchClientProxy elasticsearchClient;
        private readonly ActivitySource activitySource;
        private readonly ILogger logger;
        private readonly Counter<long> recordsProcessedCounter;
        private readonly Counter<long> recordsFailedCounter;
        private readonly Histogram<double> processingTimeHistogram;
        private Timer? indexTimer;
        private bool isIndexing = false;

        private ImmutableDictionary<string, OfferingIndex> offerings = ImmutableDictionary<string, OfferingIndex>.Empty;

        public IndexQueue(
            JinagaClient jinagaClient, 
            ElasticsearchClientProxy elasticsearchClient,
            ActivitySource activitySource,
            ILogger logger,
            Meter meter)
        {
            this.jinagaClient = jinagaClient;
            this.elasticsearchClient = elasticsearchClient;
            this.activitySource = activitySource;
            this.logger = logger;
            this.recordsProcessedCounter = meter.CreateCounter<long>("records_processed", "Records", "Number of records processed by the indexer");
            this.recordsFailedCounter = meter.CreateCounter<long>("records_failed", "Records", "Number of records that failed to be indexed");
            this.processingTimeHistogram = meter.CreateHistogram<double>("processing_time_ms", "ms", "Time taken to process records in milliseconds");
        }

        public void PushOffering(Offering offering)
        {
            string recordId = ComputeRecordId(offering);
            lock (lockObject)
            {
                if (offerings.ContainsKey(recordId))
                {
                    return;
                }
                var index = OfferingIndex.Create(offering);
                offerings = offerings.Add(recordId, index);
                logger.Debug("Added offering {CourseCode} to index queue", offering.course.code);
            }
            StartIndexTimer();
        }

        public void RemoveOffering(Offering offering)
        {
            string recordId = ComputeRecordId(offering);
            lock (lockObject)
            {
                if (!offerings.ContainsKey(recordId))
                {
                    return;
                }
                offerings = offerings.Remove(recordId);
            }
        }

        public void PushOfferingLocation(OfferingLocation location)
        {
            string recordId = ComputeRecordId(location.offering);
            lock (lockObject)
            {
                if (!offerings.TryGetValue(recordId, out var index))
                {
                    return;
                }
                index = index.WithLocation(location);
                offerings = offerings.SetItem(recordId, index);
            }
            StartIndexTimer();
        }

        public void PushOfferingTime(OfferingTime time)
        {
            string recordId = ComputeRecordId(time.offering);
            lock (lockObject)
            {
                if (!offerings.TryGetValue(recordId, out var index))
                {
                    return;
                }
                index = index.WithTime(time);
                offerings = offerings.SetItem(recordId, index);
            }
            StartIndexTimer();
        }

        public void PushOfferingInstructor(OfferingInstructor instructor)
        {
            string recordId = ComputeRecordId(instructor.offering);
            lock (lockObject)
            {
                if (!offerings.TryGetValue(recordId, out var index))
                {
                    return;
                }
                index = index.WithInstructor(instructor);
                offerings = offerings.SetItem(recordId, index);
            }
            StartIndexTimer();
        }

        private string ComputeRecordId(object record)
        {
            return jinagaClient.Hash(record).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private void StartIndexTimer()
        {
            if (indexTimer == null)
            {
                // Start a timer to index offerings every 5 seconds
                indexTimer = new Timer(async _ =>
                {
                    await IndexOfferings();
                }, null, 5000, 5000);
            }
        }

        private async Task IndexOfferings()
        {
            using var activity = activitySource.StartActivity("IndexOfferings");
            
            ImmutableDictionary<string, OfferingIndex> currentOfferings;
            lock (lockObject)
            {
                if (isIndexing)
                {
                    logger.Debug("Already indexing offerings, skipping this cycle");
                    return;
                }
                isIndexing = true;
                currentOfferings = offerings;
                // If there are no offerings, stop the timer
                if (currentOfferings.Count == 0)
                {
                    logger.Debug("No offerings to index, stopping timer");
                    indexTimer?.Dispose();
                    indexTimer = null;
                    isIndexing = false;
                    return;
                }
            }

            activity?.SetTag("queueSize", currentOfferings.Count);
            logger.Information("Starting to index {Count} offerings", currentOfferings.Count);
            
            var stopwatch = Stopwatch.StartNew();
            int successCount = 0;
            int failureCount = 0;

            try
            {
                using var convertActivity = activitySource.StartActivity("ConvertToSearchRecords");
                // Convert offerings to search records
                var searchRecords = currentOfferings.Select(kvp => 
                    kvp.Value.GetSearchRecord(kvp.Key)).ToList();
                convertActivity?.SetTag("recordCount", searchRecords.Count);
                
                using var indexActivity = activitySource.StartActivity("BulkIndexRecords");
                // Use bulk indexing instead of individual indexing
                var result = await elasticsearchClient.IndexManyRecordsAsync(searchRecords);
                indexActivity?.SetTag("success", result.IsValid);
                indexActivity?.SetTag("hasErrors", result.HasErrors);
                
                // Handle errors if needed
                if (result.HasErrors)
                {
                    failureCount = result.Errors.Count;
                    successCount = searchRecords.Count - failureCount;
                    logger.Warning("Completed indexing with {ErrorCount} errors", failureCount);
                    
                    // Errors are already logged in the ElasticsearchClientProxy
                }
                else
                {
                    successCount = searchRecords.Count;
                    logger.Information("Successfully indexed {Count} offerings", successCount);
                }
            }
            catch (Exception ex)
            {
                failureCount = currentOfferings.Count;
                logger.Error(ex, "Error occurred while indexing offerings");
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
            finally
            {
                stopwatch.Stop();
                processingTimeHistogram.Record(stopwatch.ElapsedMilliseconds);
                recordsProcessedCounter.Add(successCount);
                
                if (failureCount > 0)
                {
                    recordsFailedCounter.Add(failureCount);
                }
                
                lock (lockObject)
                {
                    isIndexing = false;
                }
                
                logger.Debug("Completed indexing cycle in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
