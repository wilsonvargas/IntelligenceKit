# IntelligenceKit.Core

Framework-agnostic core of **IntelligenceKit**, a self-hosted crash-reporting &
observability stack for .NET (a Sentry/Crashlytics alternative for the .NET
ecosystem).

This package holds the domain model (`IntelligenceEvent`, `ExceptionInfo`,
`Breadcrumb`, `DeviceRuntime`, sessions, performance spans, user feedback), the
enums, and the service layer every SDK shares:

- `IntelligenceKitService`: the single funnel that enriches every event, applies
  `BeforeSend`, sampling and PII scrubbing, and does store-and-forward.
- `SessionTracker`, `PerformanceMonitor`, `UiThreadWatchdog` (ANR detection),
  `IntelligenceKitHttpHandler` (HTTP breadcrumbs and timings) and `IntelligenceScope`.
- The MSBuild target that uploads PDBs and Android mapping files after a Release
  build (`IntelligenceKitUploadSymbols=true`).

It has **no UI framework dependency**. You normally get it through a platform package:

| App type | Package |
|---|---|
| .NET MAUI | [`IntelligenceKit.Maui`](https://www.nuget.org/packages/IntelligenceKit.Maui) |
| ASP.NET Core | [`IntelligenceKit.AspNetCore`](https://www.nuget.org/packages/IntelligenceKit.AspNetCore) |
| Blazor WebAssembly | [`IntelligenceKit.Blazor`](https://www.nuget.org/packages/IntelligenceKit.Blazor) |
| WPF / WinForms / Avalonia | [`IntelligenceKit.Wpf`](https://www.nuget.org/packages/IntelligenceKit.Wpf) / [`.WinForms`](https://www.nuget.org/packages/IntelligenceKit.WinForms) / [`.Avalonia`](https://www.nuget.org/packages/IntelligenceKit.Avalonia) |
| Console, workers, services | [`IntelligenceKit.Hosting`](https://www.nuget.org/packages/IntelligenceKit.Hosting) |

See the [documentation](https://wilsonvargas.github.io/IntelligenceKit/) for the
server, dashboard and every SDK.
