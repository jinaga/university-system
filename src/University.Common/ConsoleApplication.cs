using Serilog;
using OpenTelemetry.Trace;
using System.Collections.Immutable;

namespace University.Common
{
    public class ConsoleApplication
    {
        private readonly ILogger _logger;
        private readonly TracerProvider _tracerProvider;

        public ConsoleApplication(ILogger logger, TracerProvider tracerProvider)
        {
            _logger = logger;
            _tracerProvider = tracerProvider;
        }

        public async Task RunAsync(Func<Task<Func<Task>>> run)
        {
            try
            {
                var shutdown = await run();
                var exitEvent = SetupShutdown();
                await exitEvent.Task;
                await shutdown();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An error occurred while running the application");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
                _tracerProvider.Dispose();
            }
        }

        private TaskCompletionSource<bool> SetupShutdown()
        {
            _logger.Information("Press Ctrl+C to exit.");
            var exitEvent = new TaskCompletionSource<bool>();

            Console.CancelKeyPress += (sender, eventArgs) => {
                eventArgs.Cancel = true;
                exitEvent.SetResult(true);
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => {
                exitEvent.SetResult(true);
            };

            return exitEvent;
        }
    }
}
