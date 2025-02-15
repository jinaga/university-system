using University.Importer;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using Microsoft.Extensions.Logging;

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

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("University.Importer")
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("University.Importer"))
    .AddHttpClientInstrumentation()
    .AddAspNetCoreInstrumentation()
    .AddOtlpExporter(options => options.Endpoint = new Uri(OTEL_EXPORTER_OTLP_ENDPOINT))
    .AddConsoleExporter() // Add console exporter for debugging
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.AddConsoleExporter();
    });
});

var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("Starting University.Importer...");

var j = JinagaClientFactory.CreateClient(REPLICATOR_URL);

Console.WriteLine("Importing courses...");

var university = await UniversityDataSeeder.SeedData(j, ENVIRONMENT_PUBLIC_KEY);

var watcher = new CsvFileWatcher(j, university, IMPORT_DATA_PATH, PROCESSED_DATA_PATH, ERROR_DATA_PATH);
watcher.StartWatching();

Console.WriteLine("Press Ctrl+C to exit.");
var exitEvent = new TaskCompletionSource<bool>();

Console.CancelKeyPress += (sender, eventArgs) => {
    eventArgs.Cancel = true;
    exitEvent.SetResult(true);
};

AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => {
    exitEvent.SetResult(true);
};

await exitEvent.Task;

watcher.StopWatching();
await j.DisposeAsync();
