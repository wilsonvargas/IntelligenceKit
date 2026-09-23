# IntelligenceKit.Extensions.Logging

`Microsoft.Extensions.Logging` provider for [IntelligenceKit](https://github.com/wilsonvargas/IntelligenceKit).

```csharp
builder.Logging.AddIntelligenceKit(o =>
{
    o.MinimumBreadcrumbLevel = LogLevel.Information; // trail attached to later events
    o.MinimumEventLevel = LogLevel.Error;            // sent as grouped events
});
```

- Entries at or above `MinimumBreadcrumbLevel` become `log` breadcrumbs.
- Entries at or above `MinimumEventLevel` become events: an exception event when an
  exception is attached, otherwise a log event grouped by its **message template**
  (`"Order {Id} failed"` is one issue, not one per id). Structured properties land in
  the event data.

Requires an `IIntelligenceKit` registered by a platform SDK (`IntelligenceKit.Maui`
registers this provider for you; `IntelligenceKit.AspNetCore` too).
