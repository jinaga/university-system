using System.Diagnostics.Metrics;
using Jinaga;
using Serilog;
using University.Common;
using University.Firehose;
using University.Model;

var REPLICATOR_URL = Environment.GetEnvironmentVariable("REPLICATOR_URL");
var ENVIRONMENT_PUBLIC_KEY = Environment.GetEnvironmentVariable("ENVIRONMENT_PUBLIC_KEY");
var OTEL_EXPORTER_OTLP_ENDPOINT = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

if (REPLICATOR_URL == null || ENVIRONMENT_PUBLIC_KEY == null || OTEL_EXPORTER_OTLP_ENDPOINT == null)
{
    if (REPLICATOR_URL == null)
    {
        Console.WriteLine("Please set the environment variable REPLICATOR_URL.");
    }
    if (ENVIRONMENT_PUBLIC_KEY == null)
    {
        Console.WriteLine("Please set the environment variable ENVIRONMENT_PUBLIC_KEY.");
    }
    if (OTEL_EXPORTER_OTLP_ENDPOINT == null)
    {
        Console.WriteLine("Please set the environment variable OTEL_EXPORTER_OTLP_ENDPOINT.");
    }
    return;
}

var logger = Telemetry.SetupLogging("University.Firehose", OTEL_EXPORTER_OTLP_ENDPOINT);

try
{
    using var tracerProvider = Telemetry.SetupTracing("University.Firehose", OTEL_EXPORTER_OTLP_ENDPOINT);
    using var meterProvider = Telemetry.SetupMetrics("University.Firehose", OTEL_EXPORTER_OTLP_ENDPOINT);

    logger.Information("Starting University.Firehose...");

    var consoleApp = new ConsoleApplication(logger, tracerProvider);

    await consoleApp.RunAsync(async () =>
    {
        var j = JinagaClientFactory.CreateClient(REPLICATOR_URL);

        logger.Information("Initializing firehose...");

        var creator = await j.Fact(new User(ENVIRONMENT_PUBLIC_KEY));
        var university = await j.Fact(new Organization(creator, "6003"));

        var meter = new Meter("University.Firehose", "1.0.0");

        var serviceRunner = new ServiceRunner(logger)
            .WithService(new Firehose(j, university, meter, logger));
        await serviceRunner.Start();

        return async () =>
        {
            await serviceRunner.Stop();
            await j.DisposeAsync();
        };
    });
}
catch (Exception ex)
{
    logger.Error(ex, "An error occurred while running the firehose");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
