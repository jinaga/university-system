using System.Collections.Immutable;
using Jinaga;
using University.Model;

namespace University.Indexer.Elasticsearch
{
    public class IndexQueue
    {
        private readonly JinagaClient jinagaClient;
        private readonly Lock lockObject = new Lock();
        private readonly ElasticsearchClientProxy elasticsearchClient;
        private Timer? indexTimer;
        private bool isIndexing = false;

        private ImmutableDictionary<string, OfferingIndex> offerings = ImmutableDictionary<string, OfferingIndex>.Empty;

        public IndexQueue(JinagaClient jinagaClient, ElasticsearchClientProxy elasticsearchClient)
        {
            this.jinagaClient = jinagaClient;
            this.elasticsearchClient = elasticsearchClient;
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
            ImmutableDictionary<string, OfferingIndex> currentOfferings;
            lock (lockObject)
            {
                if (isIndexing)
                {
                    return;
                }
                isIndexing = true;
                currentOfferings = offerings;
                // If there are no offerings, stop the timer
                if (currentOfferings.Count == 0)
                {
                    indexTimer?.Dispose();
                    indexTimer = null;
                    isIndexing = false;
                    return;
                }
            }

            try
            {
                foreach (var kvp in currentOfferings)
                {
                    var recordId = kvp.Key;
                    var offering = kvp.Value;
                    var searchRecord = offering.GetSearchRecord(recordId);
                    await elasticsearchClient.IndexRecord(searchRecord);
                }
            }
            finally
            {
                lock (lockObject)
                {
                    isIndexing = false;
                }
            }
        }
    }
}