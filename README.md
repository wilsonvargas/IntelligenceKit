# IntelligenceKit

**Self-hosted observability & crash reporting for .NET and MAUI apps — a Sentry/Crashlytics alternative for the .NET ecosystem.**

IntelligenceKit captures crashes, logs and rich runtime context from your app, ships them to a backend you control, and shows them on a real-time dashboard. Add one line to your MAUI app and it starts working — no per-capture-site code.

> **Status: early / pre-release (alpha).** The SDK and backend work end-to-end, but the API is still moving, NuGet packages are not published yet, and the read API/dashboard are currently unauthenticated (see [Security](#security)). Not production-ready. Feedback and contributions welcome.

---

## Why

The .NET/MAUI ecosystem lacks a lightweight, self-hostable crash + observability stack. Commercial options (Sentry, App Center, Firebase Crashlytics) are either shutting down for .NET, priced per-seat, or not tailored to MAUI. IntelligenceKit aims to be:

- **MAUI-native** — proper crash capture on Android and iOS, device context, last-screen capture.
- **Self-hosted** — your data, your server, your database.
- **One-line integration** — `builder.UseIntelligenceKit(dsn)` and you're done.
- **Clean & extensible** — the core is framework-agnostic; MAUI is just the first client.

## Features

**Client SDK (MAUI)**
- 🧨 **Crash reporting** on Android & iOS with a typed, nested `ExceptionInfo` (inner-exception chain preserved).
- 📴 **Offline store-and-forward** — events persist to a local SQLite queue first, then upload; nothing is lost if the app crashes or is offline. The queue drains on next launch and when connectivity returns.
- 🧭 **Rich context** — breadcrumbs ("what led here"), a device runtime snapshot (memory, battery, network, current screen), plus `Environment`, `Release`, `User` (anonymous, opt-in), `Tags` and severity levels.
- 🖼️ **Last-screen capture** (opt-in) — a downscaled JPEG of the screen before the crash, captured proactively and stored apart from the event payload. Privacy-first: off by default, per-page exclusion list, no full-res images.
- 📊 **Logs** — `TrackLogAsync(level, message, data)` doubles as a breadcrumb.

**Backend & dashboard**
- 🗄️ **Persistent backend** on EF Core with **SQLite (default), PostgreSQL, or SQL Server** — selectable by config.
- ⚡ **Real-time dashboard** (Blazor WebAssembly) — new events stream in live over SignalR, no refresh.
- 📈 **Errors-per-hour chart** and per-project overview.

## Architecture

```
 ┌──────────────┐     HTTP POST /events        ┌────────────────────────┐
 │  MAUI app    │ ───────────────────────────► │  IntelligenceKit.Server │
 │  + SDK       │     (+ screenshot blob)       │  (Minimal API + EF Core)│
 └──────────────┘                               └───────────┬────────────┘
        ▲                                                    │ SignalR
        │ store-and-forward (offline SQLite queue)           ▼
        │                                        ┌────────────────────────┐
        └── crash / log / context                │  Dashboard (Blazor WASM)│
                                                  │  live feed + charts     │
                                                  └────────────────────────┘
```

Dependency direction (nothing depends on MAUI except the MAUI SDK):

```
Sample.Maui ─► IntelligenceKit.Maui ─► IntelligenceKit.Core
IntelligenceKit.Server    ─► Core, Server.Contracts, Server.Data, Server.Migrations.*
IntelligenceKit.Dashboard ─► Server.Contracts
```

Every event flows through a single funnel (`IntelligenceKitService.Enrich()` → persist → upload), so app/device/runtime/scope context is attached in one place and no capture site fills it in by hand.

## Repository layout

```
src/        Product code (the library + backend + dashboard)
samples/    Sample.Maui — a demo app that consumes the SDK
tests/      (reserved)
```

Everything targets **.NET 10**. The solution is `IntelligenceKit.slnx` (the XML solution format).

## Quick start

### 1. Run the backend

```bash
dotnet run --project src/IntelligenceKit.Server
# listens on http://0.0.0.0:7099 (SQLite by default; schema auto-migrates on first run)
```

### 2. Run the dashboard

```bash
dotnet run --project src/IntelligenceKit.Dashboard
# http://localhost:5292 — point it at the API via wwwroot/appsettings.json ("ApiBaseUrl")
```

### 3. Add the SDK to your MAUI app

> NuGet packages are not published yet. For now, reference the projects directly
> (`src/IntelligenceKit.Maui`) or build from source. The published flow will be:
>
> ```bash
> dotnet add package IntelligenceKit.Maui
> ```

In `MauiProgram.cs`:

```csharp
builder
    .UseMauiApp<App>()
    .UseIntelligenceKit("http://demo-key@your-server:7099/my-project");
```

That single call registers crash capture, offline storage, the uploader, context/breadcrumb tracking, and (opt-in) screen capture. Application name and version are auto-detected.

**Using it in code:**

```csharp
// Injected as IIntelligenceKit
kit.SetUser("anon-123");                       // optional, anonymous
kit.SetTag("tenant", "acme");
kit.AddBreadcrumb("Tapped Checkout");
await kit.TrackLogAsync(SeverityLevel.Warning, "Cart total mismatch");
await kit.TrackExceptionAsync(ex);             // manual capture; crashes are automatic
```

## Configuration

### DSN

Configuration is a single connection string bundling server + project identity:

```
http://{projectKey}@{host}:{port}/{projectId}
```

`projectKey` is a **public** routing identifier that ships inside the client app — it separates projects on a shared self-hosted server. **It is not a secret** and does not authenticate anything (yet — see roadmap).

### Database provider (server)

Set in `src/IntelligenceKit.Server/appsettings.json`:

```jsonc
{
  "Database": { "Provider": "Sqlite" },          // Sqlite | PostgreSql | SqlServer
  "ConnectionStrings": { "Events": "Data Source=intelligencekit.db" }
}
```

`ConnectionStrings:Events` is optional for SQLite (defaults to a local file) and required for PostgreSQL / SQL Server. Migrations are applied automatically on startup.

Because EF migrations are provider-specific, each provider has its own migrations assembly. To add one:

```bash
dotnet ef migrations add <Name> \
  --project src/IntelligenceKit.Server.Migrations.Sqlite \
  --startup-project src/IntelligenceKit.Server
```

A schema change means regenerating the migration in all three provider projects.

## Security

The **read side** — every query endpoint plus the SignalR hub and the dashboard — is gated by a single shared admin token. Set it on the server:

```jsonc
// src/IntelligenceKit.Server/appsettings.json
"Auth": { "ReadToken": "ik_admin_<a-long-random-string>" }
```

Callers present it as `Authorization: Bearer <token>` (the dashboard prompts for it once and stores it in the browser). Behaviour when the token is **not** set: reads are **open in Development** (zero-config local runs) and **locked in Production** (fail-closed), so a misconfigured deployment never serves data unprotected.

**Ingest** (`POST /events`) is intentionally open: the client's project key is a public routing identifier that ships inside the app, not a secret — the same model Sentry uses.

> Still evolving: there are no per-user accounts or per-project scoping yet (one shared token). Put the server behind TLS in production.

## Roadmap

Done: crash reporting (Android/iOS) · offline store-and-forward · background uploader · rich context (breadcrumbs, device snapshot, tags/user/env) · last-screen capture · persistent multi-provider backend · real-time SignalR dashboard · errors-per-hour chart.

Next:
- [x] LICENSE · README · **read-side auth** (shared admin token)
- [ ] **NuGet packaging** · **CI**
- [ ] Webhook alerts (Slack/Discord/Teams)
- [ ] Issue grouping (fingerprint → deduplicated issues with counts & trends)
- [ ] AI-assisted diagnosis over grouped issues (opt-in, provider-agnostic, PII-scrubbed)

## Building from source

```bash
dotnet build IntelligenceKit.slnx
```

Requires the .NET 10 SDK, and the MAUI workloads (`dotnet workload install maui`) to build the MAUI/sample projects.

## License

Licensed under the [MIT License](LICENSE).
