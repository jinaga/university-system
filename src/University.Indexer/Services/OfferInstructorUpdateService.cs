using Jinaga;
using Jinaga.Extensions;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using University.Common;
using University.Model;
using ILogger = Serilog.ILogger;

namespace University.Indexer.Services;

public class OfferInstructorUpdateService : IService
{
    private readonly JinagaClient jinagaClient;
    private readonly ElasticsearchClientProxy elasticsearchClient;
    private readonly ILogger logger;
    private readonly Counter<long> offeringsUpdatedCounter;
    private readonly Semester currentSemester;
    private readonly ActivitySource activitySource = new ActivitySource("University.Indexer");
    private dynamic? subscription;

    public OfferInstructorUpdateService(
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
        var offeringsToUpdateInstructor = Given<Semester>.Match(semester =>
            from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
            where offering.Successors().No<OfferingDelete>(deleted => deleted.offering)
            from offeringInstructor in offering.Successors().OfType<OfferingInstructor>(instructor => instructor.offering)
            where offeringInstructor.Successors().No<OfferingInstructor>(next => next.prior)
            from instructor in offeringInstructor.instructor.Successors().OfType<Instructor>(instructor => instructor)
            from record in offering.Successors().OfType<SearchIndexRecord>(record => record.offering)
            where !(
                from update in record.Successors().OfType<SearchIndexRecordInstructorUpdate>(update => update.record)
                where update.instructor == offeringInstructor
                select update
            ).Any()
            select new
            {
                record,
                offeringInstructor,
                instructor.name
            });

        subscription = jinagaClient.Subscribe(offeringsToUpdateInstructor, currentSemester, async update =>
        {
            using var activity = activitySource.StartActivity("UpdateOfferingInstructor");
            activity?.SetTag("courseCode", update.record.offering.course.code);
            activity?.SetTag("courseName", update.record.offering.course.name);

            var record = update.record;
            var offeringInstructor = update.offeringInstructor;
            bool indexed = await elasticsearchClient.UpdateRecordInstructor(record.recordId, update.name);

            if (indexed)
            {
                await jinagaClient.Fact(new SearchIndexRecordInstructorUpdate(record, offeringInstructor));
                offeringsUpdatedCounter.Add(1,
                    new KeyValuePair<string, object?>("courseCode", record.offering.course.code),
                    new KeyValuePair<string, object?>("courseName", record.offering.course.name));
                logger.Information("Updated instructor of {CourseCode} {CourseName}", record.offering.course.code, record.offering.course.name);
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
