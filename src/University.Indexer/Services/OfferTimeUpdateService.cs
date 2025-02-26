using Jinaga;
using Jinaga.Extensions;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using University.Common;
using University.Model;
using ILogger = Serilog.ILogger;
using University.Indexer.Elasticsearch;

namespace University.Indexer.Services;

public class OfferTimeUpdateService : IService
{
    private readonly JinagaClient jinagaClient;
    private readonly ElasticsearchClientProxy elasticsearchClient;
    private readonly ILogger logger;
    private readonly Counter<long> offeringsUpdatedCounter;
    private readonly Semester currentSemester;
    private readonly ActivitySource activitySource = new ActivitySource("University.Indexer");
    private dynamic? subscription;

    public OfferTimeUpdateService(
        JinagaClient jinagaClient,
        ElasticsearchClientProxy elasticsearchClient,
        ILogger logger,
        Counter<long> offeringsUpdatedCounter,
        Semester currentSemester)
    {
        this.jinagaClient = jinagaClient;
        this.elasticsearchClient = elasticsearchClient;
        this.logger = logger;
        this.offeringsUpdatedCounter = offeringsUpdatedCounter;
        this.currentSemester = currentSemester;
    }

    public Task Start()
    {
        var offeringsToUpdateTime = Given<Semester>.Match(semester =>
            from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
            where offering.Successors().No<OfferingDelete>(deleted => deleted.offering)
            from time in offering.Successors().OfType<OfferingTime>(time => time.offering)
            where time.Successors().No<OfferingTime>(next => next.prior)
            from record in offering.Successors().OfType<SearchIndexRecord>(record => record.offering)
            where !(
                from update in record.Successors().OfType<SearchIndexRecordTimeUpdate>(update => update.record)
                where update.time == time
                select update
            ).Any()
            select new
            {
                record,
                time
            });

        subscription = jinagaClient.Subscribe(offeringsToUpdateTime, currentSemester, async update =>
        {
            using var activity = activitySource.StartActivity("UpdateOfferingTime");
            activity?.SetTag("courseCode", update.record.offering.course.code);
            activity?.SetTag("courseName", update.record.offering.course.name);

            var record = update.record;
            var time = update.time;
            bool indexed = await elasticsearchClient.UpdateRecordTime(record.recordId, time.days, time.time);

            if (indexed)
            {
                await jinagaClient.Fact(new SearchIndexRecordTimeUpdate(record, time));
                offeringsUpdatedCounter.Add(1);
                logger.Information("Updated time of {CourseCode} {CourseName}", record.offering.course.code, record.offering.course.name);
            }
        });
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        subscription?.Stop();
        return Task.CompletedTask;
    }
}
