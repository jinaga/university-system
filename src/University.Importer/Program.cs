﻿using University.Importer;
using Serilog;
using University.Common;
using System.Diagnostics.Metrics;

var REPLICATOR_URL = Environment.GetEnvironmentVariable("REPLICATOR_URL");
var ENVIRONMENT_PUBLIC_KEY = Environment.GetEnvironmentVariable("ENVIRONMENT_PUBLIC_KEY");
var IMPORT_DATA_PATH = Environment.GetEnvironmentVariable("IMPORT_DATA_PATH");
var PROCESSED_DATA_PATH = Environment.GetEnvironmentVariable("PROCESSED_DATA_PATH");
var ERROR_DATA_PATH = Environment.GetEnvironmentVariable("ERROR_DATA_PATH");
var OTEL_EXPORTER_OTLP_ENDPOINT = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

if (REPLICATOR_URL == null || ENVIRONMENT_PUBLIC_KEY == null || IMPORT_DATA_PATH == null || PROCESSED_DATA_PATH == null || ERROR_DATA_PATH == null || OTEL_EXPORTER_OTLP_ENDPOINT == null)
{
    if (REPLICATOR_URL == null)
    {
        Console.WriteLine("Please set the environment variable REPLICATOR_URL.");
    }
    if (ENVIRONMENT_PUBLIC_KEY == null)
    {
        Console.WriteLine("Please set the environment variable ENVIRONMENT_PUBLIC_KEY.");
    }
    if (IMPORT_DATA_PATH == null)
    {
        Console.WriteLine("Please set the environment variable IMPORT_DATA_PATH.");
    }
    if (PROCESSED_DATA_PATH == null)
    {
        Console.WriteLine("Please set the environment variable PROCESSED_DATA_PATH.");
    }
    if (ERROR_DATA_PATH == null)
    {
        Console.WriteLine("Please set the environment variable ERROR_DATA_PATH.");
    }
    if (OTEL_EXPORTER_OTLP_ENDPOINT == null)
    {
        Console.WriteLine("Please set the environment variable OTEL_EXPORTER_OTLP_ENDPOINT.");
    }
    return;
}

using var tracerProvider = Telemetry.SetupTracing("University.Importer", OTEL_EXPORTER_OTLP_ENDPOINT);
var logger = Telemetry.SetupLogging(OTEL_EXPORTER_OTLP_ENDPOINT);
using var meterProvider = Telemetry.SetupMetrics("University.Importer", OTEL_EXPORTER_OTLP_ENDPOINT);

try
{
    logger.Information("Starting University.Importer...");

    var consoleApp = new ConsoleApplication(logger, tracerProvider);

    await consoleApp.RunAsync(async () =>
    {
        var j = JinagaClientFactory.CreateClient(REPLICATOR_URL);

        logger.Information("Importing courses...");

        var university = await UniversityDataSeeder.SeedData(j, ENVIRONMENT_PUBLIC_KEY);

        var meter = new Meter("University.Importer", "1.0.0");
        var watcher = new CsvFileWatcher(j, university, IMPORT_DATA_PATH, PROCESSED_DATA_PATH, ERROR_DATA_PATH, meter);
        watcher.StartWatching();

        var exitEvent = consoleApp.SetupShutdown();
        await exitEvent.Task;

        watcher.StopWatching();
        await j.DisposeAsync();
    });
}
catch (Exception ex)
{
    logger.Error(ex, "An error occurred while running the importer");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
