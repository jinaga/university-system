# Logging and Telemetry

## Importance of Using Injected Services

In this project, we identified and fixed an issue where several injected services were declared but not used:
- `logger` (Serilog) for structured logging
- `offeringsIndexedCounter` for metrics collection
- `activitySource` for distributed tracing

## Best Practices

### Logging

- Use structured logging with contextual information: `logger.Information("Indexing offering {OfferingId}", offeringId)`
- Log at appropriate levels (Information, Warning, Error)
- Include identifiers in log messages to aid troubleshooting

### Metrics

- Track operations with counters: `offeringsIndexedCounter.Add(1, new KeyValuePair<string, object?>("offering_id", offeringId))`
- Add dimensions to metrics for better analysis
- Count significant events (items processed, operations completed)

### Distributed Tracing

- Create activity spans for operations: `using var activity = activitySource.StartActivity("IndexOffering")`
- Properly nest activities to create a trace hierarchy
- Ensure spans are properly closed with `using` statements

## Common Gotchas

- Not using injected logging services
- Returning values from lambdas that expect void
- Using the wrong property names (`guid` vs `identifier`)
- Not closing or disposing activity spans properly