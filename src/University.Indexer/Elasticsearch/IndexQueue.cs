using System.Collections.Immutable;
using Jinaga;
using University.Model;

namespace University.Indexer.Elasticsearch
{
    public class IndexQueue
    {
        private readonly JinagaClient jinagaClient;
        private readonly Lock lockObject = new Lock();

        private ImmutableDictionary<string, OfferingIndex> offerings = ImmutableDictionary<string, OfferingIndex>.Empty;

        public IndexQueue(JinagaClient jinagaClient)
        {
            this.jinagaClient = jinagaClient;
        }

        private string ComputeRecordId(object record)
        {
            return jinagaClient.Hash(record).Replace('+', '-').Replace('/', '_').TrimEnd('=');
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
        }
    }
}
