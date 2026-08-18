# IntelligenceKit.Maui

**One-line, self-hosted crash reporting & observability for .NET MAUI** — a
Sentry/App Center/Crashlytics alternative for the .NET ecosystem. Ships crashes,
logs and rich runtime context to a backend you control, viewable on a real-time
dashboard.

## Install

```bash
dotnet add package IntelligenceKit.Maui
```

## Use

A single line in `MauiProgram.cs` wires up everything:

```csharp
builder
    .UseMauiApp<App>()
    .UseIntelligenceKit("http://demo-key@your-server:7099/my-project");
```

That registers crash capture (Android & iOS), an offline store-and-forward queue,
the uploader, breadcrumb/navigation and device-context tracking, and (opt-in)
last-screen capture. App name and version are auto-detected.

```csharp
// IIntelligenceKit is injected via DI
kit.SetUser("anon-123");
kit.SetTag("plan", "premium");
kit.AddBreadcrumb("Tapped Checkout");
await kit.TrackLogAsync(SeverityLevel.Warning, "Cart total mismatch");
await kit.TrackExceptionAsync(ex);   // manual capture — crashes are automatic
```

> **Android emulator:** use the host alias `10.0.2.2` instead of `localhost` in the DSN.

You'll need the [IntelligenceKit server + dashboard](https://github.com/wilsonvargas/IntelligenceKit)
running to receive and view events.

> Status: early / pre-release (alpha). APIs may change.
