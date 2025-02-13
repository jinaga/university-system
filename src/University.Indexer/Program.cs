﻿using Jinaga;
using Jinaga.Extensions;
using University.Indexer;
using University.Model;
using University.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;

var REPLICATOR_URL = Environment.GetEnvironmentVariable("REPLICATOR_URL");
var ENVIRONMENT_PUBLIC_KEY = Environment.GetEnvironmentVariable("ENVIRONMENT_PUBLIC_KEY");
var ELASTICSEARCH_URL = Environment.GetEnvironmentVariable("ELASTICSEARCH_URL");
var OTEL_EXPORTER_OTLP_ENDPOINT = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

if (REPLICATOR_URL == null || ENVIRONMENT_PUBLIC_KEY == null || ELASTICSEARCH_URL == null || OTEL_EXPORTER_OTLP_ENDPOINT == null)
{
    if (REPLICATOR_URL == null)
    {
        Console.WriteLine("Please set the environment variable REPLICATOR_URL.");
    }
    if (ENVIRONMENT_PUBLIC_KEY == null)
    {
        Console.WriteLine("Please set the environment variable ENVIRONMENT_PUBLIC_KEY.");
    }
    if (ELASTICSEARCH_URL == null)
    {
        Console.WriteLine("Please set the environment variable ELASTICSEARCH_URL.");
    }
    if (OTEL_EXPORTER_OTLP_ENDPOINT == null)
    {
        Console.WriteLine("Please set the environment variable OTEL_EXPORTER_OTLP_ENDPOINT.");
    }
    return;
}

using var tracerProvider = Telemetry.SetupTracing("University.Indexer", OTEL_EXPORTER_OTLP_ENDPOINT);
var logger = Telemetry.SetupLogging(OTEL_EXPORTER_OTLP_ENDPOINT);
var activitySource = new ActivitySource("University.Indexer");

using var meterProvider = Telemetry.SetupMetrics("University.Indexer", OTEL_EXPORTER_OTLP_ENDPOINT);

var meter = new Meter("University.Indexer", "1.0.0");
var offeringsIndexedCounter = meter.CreateCounter<long>("offerings_indexed");
var offeringsUpdatedCounter = meter.CreateCounter<long>("offerings_updated");

logger.Information("Starting University.Indexer...");

var consoleApp = new ConsoleApplication(logger, tracerProvider);

await consoleApp.RunAsync(async () =>
{
    var elasticsearchClient = new ElasticsearchClientProxy(ELASTICSEARCH_URL);

    await elasticsearchClient.Initialize();

    var j = JinagaClient.Create(options =>
    {
        options.HttpEndpoint = new Uri(REPLICATOR_URL);
    });

    var creator = await j.Fact(new User(ENVIRONMENT_PUBLIC_KEY));
    var university = await j.Fact(new Organization(creator, "6003"));
    var currentSemester = await j.Fact(new Semester(university, 2022, "Spring"));

    var offeringsToIndex = Given<Semester>.Match(semester =>
        from offering in semester.Successors().OfType<Offering>(offering => offering.semester)
        where offering.Successors().No<OfferingDelete>(deleted => deleted.offering)
        where offering.Successors().No<SearchIndexRecord>(record => record.offering)
        select offering);
    var indexInsertSubscription = j.Subscribe(offeringsToIndex, currentSemester, async offering =>
    {
        // Start a new activity for each offering
        using var activity = activitySource.StartActivity("IndexOffering");
        activity?.SetTag("courseCode", offering.course.code);
        activity?.SetTag("courseName", offering.course.name);

        // Create and index a record for the offering
        var recordId = j.Hash(offering);
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
            await j.Fact(new SearchIndexRecord(offering, recordId));
            offeringsIndexedCounter.Add(1,
                new KeyValuePair<string, object?>("courseCode", offering.course.code),
                new KeyValuePair<string, object?>("courseName", offering.course.name));
            logger.Information("Indexed course {CourseCode} {CourseName}", offering.course.code, offering.course.name);
        }
    });

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
    var timeUpdateSubscription = j.Subscribe(offeringsToUpdateTime, currentSemester, async update =>
    {
        var record = update.record;
        var time = update.time;
        bool indexed = await elasticsearchClient.UpdateRecordTime(record.recordId, time.days, time.time);

        if (indexed)
        {
            await j.Fact(new SearchIndexRecordTimeUpdate(record, time));
            offeringsUpdatedCounter.Add(1,
                new KeyValuePair<string, object?>("courseCode", record.offering.course.code),
                new KeyValuePair<string, object?>("courseName", record.offering.course.name));
            logger.Information("Updated time of {CourseCode} {CourseName}", record.offering.course.code, record.offering.course.name);
        }
    });

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
    var locationUpdateSubscription = j.Subscribe(offeringsToUpdateLocation, currentSemester, async update =>
    {
        var record = update.record;
        var location = update.location;
        bool indexed = await elasticsearchClient.UpdateRecordLocation(record.recordId, location.building, location.room);

        if (indexed)
        {
            await j.Fact(new SearchIndexRecordLocationUpdate(record, location));
            offeringsUpdatedCounter.Add(1,
                new KeyValuePair<string, object?>("courseCode", record.offering.course.code),
                new KeyValuePair<string, object?>("courseName", record.offering.course.name));
            logger.Information("Updated location of {CourseCode} {CourseName}", record.offering.course.code, record.offering.course.name);
        }
    });

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
    var instructorUpdateSubscription = j.Subscribe(offeringsToUpdateInstructor, currentSemester, async update =>
    {
        var record = update.record;
        var offeringInstructor = update.offeringInstructor;
        bool indexed = await elasticsearchClient.UpdateRecordInstructor(record.recordId, update.name);

        if (indexed)
        {
            await j.Fact(new SearchIndexRecordInstructorUpdate(record, offeringInstructor));
            offeringsUpdatedCounter.Add(1,
                new KeyValuePair<string, object?>("courseCode", record.offering.course.code),
                new KeyValuePair<string, object?>("courseName", record.offering.course.name));
            logger.Information("Updated instructor of {CourseCode} {CourseName}", record.offering.course.code, record.offering.course.name);
        }
    });

    return async () =>
    {
        indexInsertSubscription.Stop();
        timeUpdateSubscription.Stop();
        locationUpdateSubscription.Stop();
        instructorUpdateSubscription.Stop();
        await j.DisposeAsync();
        logger.Information("Stopped indexing course offerings.");
    };
});
