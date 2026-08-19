# Changelog

All notable changes to IntelligenceKit are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

The public API of the `IntelligenceKit.Core` and `IntelligenceKit.Maui` NuGet packages
is what SemVer applies to (see [Versioning](README.md#versioning-and-api-stability)).

## [1.0.0] - 2026-08-19

First stable release. The API is now frozen under SemVer.

### Added

- **MAUI SDK** — automatic crash reporting on Android & iOS (typed, nested
  `ExceptionInfo`), offline store-and-forward queue, rich context (breadcrumbs,
  device runtime snapshot, tags/user/environment/release), opt-in last-screen
  capture, and `TrackLogAsync`. One-line setup: `builder.UseIntelligenceKit(dsn)`.
- **Backend** — minimal-API ingest/query server on EF Core with **SQLite,
  PostgreSQL and SQL Server** providers; idempotent ingest; **issue grouping** by
  fingerprint; real-time **SignalR** push; errors-per-hour and per-project stats.
- **Per-project scoping (multi-tenant)** — a `Project` registry with a per-project
  read key that sees only its own data, plus an admin-only management API
  (`/admin/projects` create/list/rotate-key/delete). The global admin token still
  sees everything. Read endpoints and the SignalR hub filter by the caller's scope.
- **Ingest hardening** — per-client-IP rate limiting (`429` + `Retry-After`, config
  under `RateLimit:Ingest`) and known-project validation (config
  `Ingest:RequireKnownProject`).
- **Data retention** — an opt-in background sweep prunes events/screenshots and
  stale issues older than `Retention:Days`.
- **Dashboard** — Blazor WebAssembly live feed + charts.
- **Ops** — one-command Docker Compose stack (PostgreSQL + server + dashboard),
  GitHub Actions CI, and a 102-test suite (Core unit + Server integration).

### Changed

- **BREAKING (since `0.1.0-alpha`): ingest now validates the project by default.**
  `POST /events` only accepts events whose `(projectId, projectKey)` pair matches a
  project registered via `POST /admin/projects`; unknown projects get `404`. Set
  `Ingest:RequireKnownProject: false` to keep ingest fully open as before.
- **Read-side auth** is no longer a single shared token only: a presented credential
  is resolved to either the admin token (sees all) or a per-project read key (scoped).
  The existing admin-token flow is unchanged.
- The SDK now treats HTTP `429`/`408` as transient (store-and-forward retries them)
  instead of dropping the event, so ingest rate limiting never loses data.

### Security

- Read API, SignalR hub and dashboard remain gated (admin token or project read key);
  open in Development when unset, fail-closed in Production.
- Project read keys are stored only as a SHA-256 hash and shown once at
  creation/rotation.

## [0.1.0-alpha.1] - 2026-08-18

- Initial alpha packages (`IntelligenceKit.Core`, `IntelligenceKit.Maui`) published to
  NuGet. End-to-end SDK → server → dashboard flow with crash reporting, offline
  store-and-forward, rich context, last-screen capture, multi-provider backend,
  real-time dashboard, read-side auth and issue grouping.

[1.0.0]: https://github.com/wilsonvargas/IntelligenceKit/releases/tag/v1.0.0
[0.1.0-alpha.1]: https://github.com/wilsonvargas/IntelligenceKit/releases/tag/v0.1.0-alpha.1
