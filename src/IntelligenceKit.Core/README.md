# IntelligenceKit.Core

Framework-agnostic core of **IntelligenceKit**, a self-hosted crash-reporting &
observability stack for .NET (a Sentry/Crashlytics alternative for the .NET
ecosystem).

This package holds the domain model (`IntelligenceEvent`, `ExceptionInfo`,
`Breadcrumb`, `DeviceRuntime`), the enums, and the interface-driven service layer
(`IntelligenceKitService`) — the single funnel that enriches every event and does
store-and-forward. It has **no MAUI dependency** and is meant to be referenced by
the platform SDKs.

👉 If you're building a **.NET MAUI** app, install [`IntelligenceKit.Maui`](https://www.nuget.org/packages/IntelligenceKit.Maui) instead — it pulls this in.

See the [project on GitHub](https://github.com/wilsonvargas/IntelligenceKit) for
the server, dashboard and full docs.

> Status: early / pre-release (alpha). APIs may change.
