using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;

namespace University.Common
{
    public static class Telemetry
    {
        public static TracerProvider SetupTracing(string serviceName, string otlpEndpoint)
        {
            return Sdk.CreateTracerProviderBuilder()
                .AddSource(serviceName)
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint))
                .Build();
        }

        public static ILoggerFactory SetupLogging(string otlpEndpoint)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .WriteTo.Console(new CompactJsonFormatter())
                .CreateLogger();

            return LoggerFactory.Create(builder =>
            {
                builder
                    .AddSerilog(Log.Logger)
                    .AddOpenTelemetry(options =>
                    {
                        options
                            .AddOtlpExporter(otlpOptions => 
                            {
                                otlpOptions.Endpoint = new Uri(otlpEndpoint);
                            });
                    });
            });
        }
    }
}
