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

        // Instead of starting the service immediately, run the interactive menu
        await RunInteractiveMenu(j, university, meter, logger);

        return async () =>
        {
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

// Interactive menu for the firehose application
static async Task RunInteractiveMenu(JinagaClient j, Organization university, Meter meter, ILogger logger)
{
    var firehose = new Firehose(j, university, meter, logger);
    bool running = true;
    
    // Display menu once at startup
    DisplayMenu();
    
    // Define the event handler for Ctrl+C at the menu level
    ConsoleCancelEventHandler menuCancelHandler = (s, e) => {
        e.Cancel = true;
        running = false;
    };
    
    // Add the event handler
    Console.CancelKeyPress += menuCancelHandler;
    
    try
    {
        while (running)
        {
            // Wait for user input
            string? input = Console.ReadLine();
            string command = input?.Trim().ToLower() ?? "";
            
            switch (command)
            {
                case "":
                    // Empty line, redisplay menu
                    DisplayMenu();
                    break;
                    
                case "1":
                    // Set target rate
                    Console.Write("Enter target rate (offerings per second): ");
                    string? rateInput = Console.ReadLine();
                    if (rateInput != null && int.TryParse(rateInput, out int rate))
                    {
                        firehose.SetTargetRate(rate);
                        Console.WriteLine($"Target rate set to {rate} offerings per second.");
                    }
                    else
                    {
                        Console.WriteLine("Invalid rate. Please enter a number.");
                    }
                    break;
                    
                case "2":
                    // Start firehose
                    Console.WriteLine("Starting firehose. Press Ctrl+C to stop and return to menu.");
                    
                    // Remove the menu-level Ctrl+C handler
                    Console.CancelKeyPress -= menuCancelHandler;
                    
                    // Setup cancellation for firehose
                    var cts = new CancellationTokenSource();
                    
                    // Add firehose-specific Ctrl+C handler
                    ConsoleCancelEventHandler firehoseCancelHandler = (s, e) => {
                        e.Cancel = true;
                        cts.Cancel();
                    };
                    Console.CancelKeyPress += firehoseCancelHandler;
                    
                    await firehose.Start();
                    
                    try {
                        await Task.Delay(-1, cts.Token); // Wait indefinitely until cancelled
                    }
                    catch (OperationCanceledException) {
                        // Ctrl+C was pressed
                    }
                    finally {
                        await firehose.Stop();
                        Console.WriteLine("Firehose stopped.");
                        
                        // Remove the firehose-specific handler
                        Console.CancelKeyPress -= firehoseCancelHandler;
                        
                        // Restore the menu-level handler
                        Console.CancelKeyPress += menuCancelHandler;
                        
                        // Show menu after stopping firehose
                        DisplayMenu();
                    }
                    break;
                    
                case "exit":
                case "quit":
                    running = false;
                    break;
                    
                default:
                    Console.WriteLine("Unknown command. Please try again.");
                    break;
            }
        }
    }
    finally
    {
        // Ensure we remove the event handler when exiting
        Console.CancelKeyPress -= menuCancelHandler;
    }
}

static void DisplayMenu()
{
    Console.WriteLine("\n=== University.Firehose Menu ===");
    Console.WriteLine("1. Set target rate (offerings per second)");
    Console.WriteLine("2. Start firehose");
    Console.WriteLine("\nEnter command (or press Enter to redisplay menu):");
}
