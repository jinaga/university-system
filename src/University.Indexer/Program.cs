﻿﻿using Jinaga;
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

    return async () =>
    {
        indexInsertSubscription.Stop();
        await j.DisposeAsync();
        logger.Information("Stopped indexing course offerings.");
    };
});
