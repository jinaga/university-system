using Jinaga;
using Jinaga.Extensions;
using System.Diagnostics.Metrics;
using University.Common;
using University.Model;
using ILogger = Serilog.ILogger;

namespace University.Indexer.Services;

public class OfferLocationUpdateService : IService
{
    private readonly JinagaClient jinagaClient;
    private readonly ElasticsearchClientProxy elasticsearchClient;
    private readonly ILogger logger;
    private readonly Counter<long> offeringsUpdatedCounter;
    private readonly Semester currentSemester;
    private dynamic? subscription;

    public OfferLocationUpdateService(
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
        var offeringsToUpdateLocation = Given<Semester>.Match(semester =>
            from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
            where offering.Successors().No<OfferingDelete>(deleted => deleted.offering)
            from location in offering.Successors().OfType<OfferingLocation>(location => location.offering)
            where location.Successors().No<OfferingLocation>(next => next.prior)
            from record in offering.Successors().OfType<SearchIndexRecord>(record => record.offering)
            where !(
                from update in record.Successors().OfType<SearchIndexRecordLocationUpdate>(update => update.record)
                where update.location == location
                select update
            ).Any()
            select new
            {
                record,
                location
            });

        subscription = jinagaClient.Subscribe(offeringsToUpdateLocation, currentSemester, async update =>
        {
            var record = update.record;
            var location = update.location;
            bool indexed = await elasticsearchClient.UpdateRecordLocation(record.recordId, location.building, location.room);

            if (indexed)
            {
                await jinagaClient.Fact(new SearchIndexRecordLocationUpdate(record, location));
                offeringsUpdatedCounter.Add(1,
                    new KeyValuePair<string, object?>("courseCode", record.offering.course.code),
                    new KeyValuePair<string, object?>("courseName", record.offering.course.name));
                logger.Information("Updated location of {CourseCode} {CourseName}", record.offering.course.code, record.offering.course.name);
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
