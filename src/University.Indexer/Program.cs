﻿using System.Diagnostics;
using System.Diagnostics.Metrics;

using Jinaga;

using Serilog;

using University.Common;
using University.Indexer;
using University.Indexer.Elasticsearch;
using University.Indexer.Services;
using University.Model;

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

var logger = Telemetry.SetupLogging("University.Indexer", OTEL_EXPORTER_OTLP_ENDPOINT);

try
{
    using var tracerProvider = Telemetry.SetupTracing("University.Indexer", OTEL_EXPORTER_OTLP_ENDPOINT);
    var activitySource = new ActivitySource("University.Indexer");
    
    using var meterProvider = Telemetry.SetupMetrics("University.Indexer", OTEL_EXPORTER_OTLP_ENDPOINT);
    
    var meter = new Meter("University.Indexer", "1.0.0");
    var offeringsIndexedCounter = meter.CreateCounter<long>("offerings_indexed");
    var offeringsUpdatedCounter = meter.CreateCounter<long>("offerings_updated");
    
    logger.Information("Starting University.Indexer...");
    
    var consoleApp = new ConsoleApplication(logger, tracerProvider);
    
    await consoleApp.RunAsync(async () =>
    {
        var elasticsearchClient = new ElasticsearchClientProxy(ELASTICSEARCH_URL, logger, activitySource);
    
        await elasticsearchClient.Initialize();
    
        var j = JinagaClientFactory.CreateClient(REPLICATOR_URL);
    
        var creator = await j.Fact(new User(ENVIRONMENT_PUBLIC_KEY));
        var university = await j.Fact(new Organization(creator, "6003"));
        var currentSemester = await j.Fact(new Semester(university, 2022, "Spring"));
    
        var serviceRunner = new ServiceRunner(logger)
            .WithService(new OfferIndexService(j, elasticsearchClient, logger, offeringsIndexedCounter, currentSemester))
            .WithService(new OfferTimeUpdateService(j, elasticsearchClient, logger, offeringsUpdatedCounter, currentSemester))
            .WithService(new OfferLocationUpdateService(j, elasticsearchClient, logger, offeringsUpdatedCounter, currentSemester))
            .WithService(new OfferInstructorUpdateService(j, elasticsearchClient, logger, offeringsUpdatedCounter, currentSemester));
        await serviceRunner.Start();
    
        return async () =>
        {
            await serviceRunner.Stop();
            await j.DisposeAsync();
            logger.Information("Stopped indexing course offerings.");
        };
    });
    
}
catch (Exception ex)
{
    logger.Error(ex, "An error occurred while running the indexer");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
