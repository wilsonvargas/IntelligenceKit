# IntelligenceKit.Hosting

Generic [IntelligenceKit](https://github.com/wilsonvargas/IntelligenceKit) SDK for any
.NET app: console tools, workers, services, desktop apps. It is also the base of the
`IntelligenceKit.AspNetCore`, `.Wpf`, `.WinForms`, `.Avalonia` and `.Blazor` packages.

With the generic host / DI:

```csharp
builder.Services.AddIntelligenceKit("http://projectKey@host:7099/my-worker");
```

Without DI:

```csharp
using var ik = IntelligenceKitSdk.Init("http://projectKey@host:7099/my-tool");
await IntelligenceKitSdk.Current!.TrackLogAsync(SeverityLevel.Warning, "Low disk space");
```

What you get:

- Fatal crashes (`AppDomain.UnhandledException`) are written to a local queue and
  sent on the next start; unobserved task exceptions are reported as handled errors.
- Offline store-and-forward in `%LOCALAPPDATA%/IntelligenceKit/{projectId}`
  (`OfflineStorePath` to change it; in memory in the browser).
- `ILogger` integration, sessions, and performance spans (`AddIntelligenceKitHandler()` on
  `HttpClient`s).
- `IntelligenceScope.Push()` for per-operation tags/breadcrumbs (per request, per job).
- A flush on shutdown, bounded by `ShutdownFlushTimeout`.
