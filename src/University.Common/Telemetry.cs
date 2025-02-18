using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.Logs;
using Serilog;
using OpenTelemetry.Metrics;

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

        public static ILogger SetupLogging(string serviceName, string otlpEndpoint)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("service.name", serviceName)
                .WriteTo.OpenTelemetry()
                .CreateLogger();

            return Log.Logger;
        }

        public static MeterProvider SetupMetrics(string serviceName, string otlpEndpoint)
        {
            return Sdk.CreateMeterProviderBuilder()
                .AddMeter(serviceName)
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                })
                .Build();
        }
    }
}
