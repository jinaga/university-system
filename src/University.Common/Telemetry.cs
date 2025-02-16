using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
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

        public static ILogger SetupLogging(string otlpEndpoint)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .WriteTo.Console(new CompactJsonFormatter())
                .CreateLogger();

            return Log.Logger;
        }
    }
}
