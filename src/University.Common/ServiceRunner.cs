using System.Collections.Immutable;
using Serilog;

namespace University.Common;

public class ServiceRunner
{
    private readonly ILogger _logger;
    private readonly ImmutableList<IService> _services;

    public ServiceRunner(ILogger logger)
    {
        _logger = logger;
        _services = ImmutableList<IService>.Empty;
    }

    private ServiceRunner(ILogger logger, ImmutableList<IService> services)
    {
        _logger = logger;
        _services = services;
    }

    public ServiceRunner WithService(IService service)
    {
        return new ServiceRunner(_logger, _services.Add(service));
    }

    public async Task Start()
    {
        _logger.Information("Starting services...");
        var tasks = _services.Select(service => service.Start()).ToArray();
        await Task.WhenAll(tasks);
        _logger.Information("Services started.");
    }

    public async Task Stop()
    {
        _logger.Information("Stopping services...");
        var tasks = _services.Select(service => service.Stop()).ToArray();
        await Task.WhenAll(tasks);
        _logger.Information("Services stopped.");
    }
}