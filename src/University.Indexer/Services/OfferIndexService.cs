using Jinaga;
using Jinaga.Extensions;
using System.Diagnostics.Metrics;
using University.Common;
using University.Model;
using ILogger = Serilog.ILogger;
using System.Diagnostics;
using University.Indexer.Elasticsearch;

namespace University.Indexer.Services;

public class OfferIndexService : IService
{
    private readonly JinagaClient jinagaClient;
    private readonly IndexQueue indexQueue;
    private readonly ILogger logger;
    private readonly Counter<long> offeringsIndexedCounter;
    private readonly Semester currentSemester;
    private readonly ActivitySource activitySource = new ActivitySource("University.Indexer");
    private dynamic? subscription;

    public OfferIndexService(
        JinagaClient jinagaClient,
        IndexQueue indexQueue,
        ILogger logger,
        Counter<long> offeringsIndexedCounter,
        Semester currentSemester)
    {
        this.jinagaClient = jinagaClient;
        this.indexQueue = indexQueue;
        this.logger = logger;
        this.offeringsIndexedCounter = offeringsIndexedCounter;
        this.currentSemester = currentSemester;
    }

    public Task Start()
    {
        var offeringsToIndex = Given<Semester>.Match((semester, facts) =>
            from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
            where offering.Successors().No<OfferingDelete>(deleted => deleted.offering)
            where offering.Successors().No<SearchIndexRecord>(record => record.offering)
            select new
            {
                Offering = offering,
                Times = facts.Observable(offering.Times),
                Locations = facts.Observable(offering.Locations),
                Instructors = facts.Observable(
                    from offeringInstructor in offering.Successors().OfType<OfferingInstructor>(instructor => instructor.offering)
                    where offeringInstructor.Successors().No<OfferingInstructor>(next => next.prior)
                    select offeringInstructor
                )
            });

        subscription = jinagaClient.Subscribe(offeringsToIndex, currentSemester, projection =>
        {
            indexQueue.PushOffering(projection.Offering);
            projection.Times.OnAdded(time => indexQueue.PushOfferingTime(time));
            projection.Locations.OnAdded(location => indexQueue.PushOfferingLocation(location));
            projection.Instructors.OnAdded(instructor => indexQueue.PushOfferingInstructor(instructor));

            return () => indexQueue.RemoveOffering(projection.Offering);
        });
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        subscription?.Stop();
        return Task.CompletedTask;
    }
}
