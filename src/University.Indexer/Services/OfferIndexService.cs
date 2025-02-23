using Jinaga;
using Jinaga.Extensions;
using System.Diagnostics.Metrics;
using University.Common;
using University.Model;
using ILogger = Serilog.ILogger;
using System.Diagnostics;

namespace University.Indexer.Services;

public class OfferIndexService : IService
{
    private readonly JinagaClient jinagaClient;
    private readonly ElasticsearchClientProxy elasticsearchClient;
    private readonly ILogger logger;
    private readonly Counter<long> offeringsIndexedCounter;
    private readonly Semester currentSemester;
    private readonly ActivitySource activitySource = new ActivitySource("University.Indexer");
    private dynamic? subscription;

    public OfferIndexService(
        JinagaClient jinagaClient,
        ElasticsearchClientProxy elasticsearchClient,
        ILogger logger,
        Counter<long> offeringsIndexedCounter,
        Semester currentSemester)
    {
        this.jinagaClient = jinagaClient;
        this.elasticsearchClient = elasticsearchClient;
        this.logger = logger;
        this.offeringsIndexedCounter = offeringsIndexedCounter;
        this.currentSemester = currentSemester;
    }

    public Task Start()
    {
        var offeringsToIndex = Given<Semester>.Match(semester =>
            from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
            where offering.Successors().No<OfferingDelete>(deleted => deleted.offering)
            where offering.Successors().No<SearchIndexRecord>(record => record.offering)
            select offering);

        subscription = jinagaClient.Subscribe(offeringsToIndex, currentSemester, async offering =>
        {
            using var activity = activitySource.StartActivity("IndexOffering");
            activity?.SetTag("courseCode", offering.course.code);
            activity?.SetTag("courseName", offering.course.name);

            var recordId = jinagaClient.Hash(offering).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var searchRecord = new SearchRecord
            {
                Id = recordId,
                CourseCode = offering.course.code,
                CourseName = offering.course.name,
                Days = "TBA",
                Time = "TBA",
                Instructor = "TBA",
                Location = "TBA"
            };
            bool indexed = await elasticsearchClient.IndexRecord(searchRecord);

            if (indexed)
            {
                await jinagaClient.Fact(new SearchIndexRecord(offering, recordId));
                offeringsIndexedCounter.Add(1,
                    new KeyValuePair<string, object?>("courseCode", offering.course.code),
                    new KeyValuePair<string, object?>("courseName", offering.course.name));
                logger.Information("Indexed course {CourseCode} {CourseName}", offering.course.code, offering.course.name);
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
