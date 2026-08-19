using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using IntelligenceKit.Core.Diagnostics;
using IntelligenceKit.Core.Models;
using IntelligenceKit.Server;
using IntelligenceKit.Server.Auth;
using IntelligenceKit.Server.Contracts;
using IntelligenceKit.Server.Data;
using IntelligenceKit.Server.Projects;
using IntelligenceKit.Server.Retention;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

const string DashboardCors = "dashboard";
const string IngestRateLimit = "ingest";
const string AdminOnly = "admin-only";

var builder = WebApplication.CreateBuilder(args);

// Accept (and emit) enums as strings so third-party clients can post
// "eventType": "Exception" as well as the numeric form. Reading also still
// accepts numbers, so existing clients keep working.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Persistence: EF Core. Provider is config-driven ("Database:Provider" =
// Sqlite | PostgreSql | SqlServer), defaulting to SQLite (zero-config).
// Each provider has its own migrations assembly, selected by name here.
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("Events");

builder.Services.AddDbContext<IntelligenceDbContext>(options =>
{
    switch (provider.ToLowerInvariant())
    {
        case "postgres":
        case "postgresql":
            options.UseNpgsql(
                connectionString ?? throw new InvalidOperationException("ConnectionStrings:Events is required for PostgreSql."),
                x => x.MigrationsAssembly("IntelligenceKit.Server.Migrations.PostgreSql"));
            break;

        case "sqlserver":
        case "mssql":
            options.UseSqlServer(
                connectionString ?? throw new InvalidOperationException("ConnectionStrings:Events is required for SqlServer."),
                x => x.MigrationsAssembly("IntelligenceKit.Server.Migrations.SqlServer"));
            break;

        default:
            options.UseSqlite(
                connectionString ?? "Data Source=intelligencekit.db",
                x => x.MigrationsAssembly("IntelligenceKit.Server.Migrations.Sqlite"));
            break;
    }
});

builder.Services.AddOpenApi();
builder.Services.AddSignalR();

// Data retention: a background service prunes events/screenshots/issues older
// than Retention:Days on a Retention:SweepHours cadence. Off by default (opt in
// via Retention:Enabled). The sweeper is scoped (it needs the DbContext).
builder.Services.AddScoped<RetentionSweeper>();
builder.Services.AddHostedService<RetentionService>();

// Read-side auth: a single shared admin token gates every query endpoint and the
// SignalR hub. Ingest (POST /events) stays open by design — the client's project
// key is a public routing id, not a secret. See ReadTokenAuthHandler.
builder.Services
    .AddAuthentication(ReadTokenDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ReadTokenAuthHandler>(ReadTokenDefaults.Scheme, null);
builder.Services.AddAuthorization(options =>
{
    // Project management is admin-only; a project-scoped read key can't manage tenants.
    options.AddPolicy(AdminOnly, policy =>
        policy.RequireClaim(ReadTokenDefaults.RoleClaim, ReadTokenDefaults.RoleAdmin));
});

// The Blazor WASM dashboard is served from its own origin, so it needs CORS.
// Auth is via bearer token (no cookies/credentials), so any origin may call.
builder.Services.AddCors(options =>
{
    options.AddPolicy(DashboardCors, policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Ingest is unauthenticated by design, so it gets a per-client-IP rate limit to
// blunt floods/abuse. Only the POST ingest endpoints opt in (via
// RequireRateLimiting); the token-gated read side is left untouched. Config:
// RateLimit:Ingest:{Enabled,PermitLimit,WindowSeconds,QueueLimit} is read per
// request (so it can be overridden without a rebuild). A throttled client gets
// 429 + Retry-After; the SDK's store-and-forward defers those events (429 is
// treated as transient), so nothing is lost.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        return ValueTask.CompletedTask;
    };

    options.AddPolicy(IngestRateLimit, httpContext =>
    {
        var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!config.GetValue("RateLimit:Ingest:Enabled", true))
            return RateLimitPartition.GetNoLimiter("disabled");

        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = config.GetValue("RateLimit:Ingest:PermitLimit", 300),
            Window = TimeSpan.FromSeconds(config.GetValue("RateLimit:Ingest:WindowSeconds", 60)),
            QueueLimit = config.GetValue("RateLimit:Ingest:QueueLimit", 0),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    });
});

var app = builder.Build();

app.UseCors(DashboardCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Apply pending migrations on startup (creates the schema on first run).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IntelligenceDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Ingest -----------------------------------------------------------------
app.MapPost("/events", async (IntelligenceEvent intelligenceEvent, HttpRequest request, IntelligenceDbContext db, IHubContext<EventsHub> hub, IConfiguration config) =>
{
    var eventId = intelligenceEvent.Id == Guid.Empty ? Guid.NewGuid() : intelligenceEvent.Id;

    // Reject events for unknown projects: the (projectId, projectKey) pair from the
    // DSN must match a registered project. Opt-out via Ingest:RequireKnownProject.
    if (config.GetValue("Ingest:RequireKnownProject", true))
    {
        var projectKey = request.Headers["X-IntelligenceKit-Key"].ToString();
        var known = await db.Projects.AsNoTracking().AnyAsync(p =>
            p.ProjectId == intelligenceEvent.ProjectId && p.ProjectKey == projectKey);
        if (!known)
            return Results.NotFound(new { error = "Unknown project. Register it via POST /admin/projects." });
    }

    // Idempotent ingest: a client may re-send an already-delivered event while
    // retrying its screenshot upload. Treat a duplicate as success, don't insert
    // a second row, and don't re-broadcast.
    if (await db.Events.AnyAsync(e => e.Id == eventId))
        return Results.Ok(new { Id = eventId });

    var fingerprint = EventFingerprint.Compute(intelligenceEvent);

    var stored = new StoredEvent
    {
        Id = eventId,
        Fingerprint = fingerprint.Fingerprint,
        ProjectId = intelligenceEvent.ProjectId,
        ProjectKey = request.Headers["X-IntelligenceKit-Key"].ToString(),
        ApplicationName = intelligenceEvent.ApplicationName,
        ApplicationVersion = intelligenceEvent.ApplicationVersion,
        Platform = intelligenceEvent.Platform,
        DeviceName = intelligenceEvent.DeviceName,
        DeviceModel = intelligenceEvent.DeviceModel,
        Manufacturer = intelligenceEvent.Manufacturer,
        OperatingSystem = intelligenceEvent.OperatingSystem,
        Environment = intelligenceEvent.Environment,
        Release = intelligenceEvent.Release,
        UserId = intelligenceEvent.UserId,
        EventType = intelligenceEvent.EventType.ToString(),
        Level = intelligenceEvent.Level?.ToString(),
        Message = intelligenceEvent.Message,
        ExceptionType = intelligenceEvent.Exception?.Type,
        ExceptionMessage = intelligenceEvent.Exception?.Message,
        ExceptionJson = intelligenceEvent.Exception is null
            ? null
            : JsonSerializer.Serialize(intelligenceEvent.Exception),
        DeviceRuntimeJson = intelligenceEvent.DeviceRuntime is null
            ? null
            : JsonSerializer.Serialize(intelligenceEvent.DeviceRuntime),
        BreadcrumbsJson = intelligenceEvent.Breadcrumbs.Count == 0
            ? null
            : JsonSerializer.Serialize(intelligenceEvent.Breadcrumbs),
        TagsJson = intelligenceEvent.Tags.Count == 0
            ? null
            : JsonSerializer.Serialize(intelligenceEvent.Tags),
        DataJson = intelligenceEvent.Data.Count == 0
            ? null
            : JsonSerializer.Serialize(intelligenceEvent.Data),
        Timestamp = intelligenceEvent.Timestamp,
        ReceivedAt = DateTime.UtcNow
    };

    db.Events.Add(stored);
    await db.SaveChangesAsync();

    // Push the new event to any live dashboards.
    var summary = new EventSummary(
        stored.Id, stored.ProjectId, stored.ApplicationName, stored.ApplicationVersion,
        stored.Environment, stored.Platform, stored.DeviceName, stored.EventType,
        stored.Level, stored.UserId,
        stored.ExceptionType, stored.ExceptionMessage, stored.Message,
        stored.Timestamp, stored.ReceivedAt);
    // Only admins and dashboards scoped to this project receive the push.
    await hub.Clients.Groups("admins", $"project:{stored.ProjectId}").SendAsync("eventReceived", summary);

    // Group the event into its issue: create it on first sighting, otherwise bump
    // the count and move LastSeen forward.
    var issue = await db.Issues
        .FirstOrDefaultAsync(i => i.ProjectId == stored.ProjectId && i.Fingerprint == stored.Fingerprint);

    if (issue is null)
    {
        issue = new Issue
        {
            Id = Guid.NewGuid(),
            ProjectId = stored.ProjectId,
            Fingerprint = stored.Fingerprint,
            Title = fingerprint.Title,
            Culprit = fingerprint.Culprit,
            EventType = stored.EventType,
            Level = stored.Level,
            EventCount = 1,
            FirstSeen = stored.ReceivedAt,
            LastSeen = stored.ReceivedAt,
            LastEventId = stored.Id
        };
        db.Issues.Add(issue);
    }
    else
    {
        issue.EventCount += 1;
        issue.LastSeen = stored.ReceivedAt;
        issue.LastEventId = stored.Id;
        issue.Level = stored.Level;
        issue.Title = fingerprint.Title;
        issue.Culprit = fingerprint.Culprit;
    }

    await db.SaveChangesAsync();

    // Push the updated issue to any live dashboards (trend is recomputed on read).
    var issueSummary = new IssueSummary(
        issue.Id, issue.ProjectId, issue.Fingerprint, issue.Title, issue.Culprit,
        issue.EventType, issue.Level, issue.EventCount,
        issue.FirstSeen, issue.LastSeen, issue.LastEventId, 0, 0);
    await hub.Clients.Groups("admins", $"project:{issue.ProjectId}").SendAsync("issueUpserted", issueSummary);

    return Results.Created($"/events/{stored.Id}", new { stored.Id });
}).RequireRateLimiting(IngestRateLimit);

// Query ------------------------------------------------------------------
app.MapGet("/events", async (IntelligenceDbContext db, ClaimsPrincipal user, string? projectId, string? eventType, int skip = 0, int take = 50) =>
{
    take = Math.Clamp(take, 1, 200);

    var query = db.Events.AsNoTracking().AsQueryable();

    // A scoped caller is pinned to its own project; the query param can't widen it.
    var projectFilter = user.ProjectScope() ?? projectId;
    if (!string.IsNullOrWhiteSpace(projectFilter))
        query = query.Where(e => e.ProjectId == projectFilter);

    if (!string.IsNullOrWhiteSpace(eventType))
        query = query.Where(e => e.EventType == eventType);

    var total = await query.CountAsync();

    var items = await query
        .OrderByDescending(e => e.ReceivedAt)
        .Skip(skip)
        .Take(take)
        .Select(e => new EventSummary(
            e.Id, e.ProjectId, e.ApplicationName, e.ApplicationVersion,
            e.Environment, e.Platform, e.DeviceName, e.EventType,
            e.Level, e.UserId,
            e.ExceptionType, e.ExceptionMessage, e.Message, e.Timestamp, e.ReceivedAt))
        .ToListAsync();

    return Results.Ok(new PagedResult<EventSummary>(total, skip, take, items));
}).RequireAuthorization();

app.MapGet("/events/{id:guid}", async (Guid id, IntelligenceDbContext db, ClaimsPrincipal user) =>
{
    var e = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (e is null)
        return Results.NotFound();

    // Don't leak another project's event to a scoped caller (404, not 403, so the
    // event's existence isn't disclosed).
    var scope = user.ProjectScope();
    if (scope is not null && e.ProjectId != scope)
        return Results.NotFound();

    var hasScreenshot = await db.Screenshots.AsNoTracking().AnyAsync(s => s.EventId == id);

    var detail = new EventDetail(
        e.Id, e.ProjectId, e.ProjectKey,
        e.ApplicationName, e.ApplicationVersion,
        e.Environment, e.Release,
        e.Platform, e.DeviceName, e.DeviceModel, e.Manufacturer, e.OperatingSystem,
        e.UserId,
        e.EventType, e.Level, e.Message,
        e.ExceptionJson is null ? null : JsonSerializer.Deserialize<ExceptionInfo>(e.ExceptionJson),
        e.DeviceRuntimeJson is null ? null : JsonSerializer.Deserialize<DeviceRuntime>(e.DeviceRuntimeJson),
        e.BreadcrumbsJson is null
            ? Array.Empty<Breadcrumb>()
            : JsonSerializer.Deserialize<List<Breadcrumb>>(e.BreadcrumbsJson) ?? new List<Breadcrumb>(),
        e.TagsJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(e.TagsJson),
        e.DataJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(e.DataJson),
        hasScreenshot,
        e.Timestamp, e.ReceivedAt);

    return Results.Ok(detail);
}).RequireAuthorization();

// Screenshot blob for an event (the "last screen"). Uploaded separately from the
// event so image bytes never ride inside the JSON payload.
app.MapPost("/events/{id:guid}/screenshot", async (Guid id, HttpRequest request, IntelligenceDbContext db) =>
{
    const long maxBytes = 2 * 1024 * 1024; // 2 MB cap

    if (request.ContentLength is > maxBytes)
        return Results.BadRequest("Screenshot too large.");

    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer);
    if (buffer.Length == 0 || buffer.Length > maxBytes)
        return Results.BadRequest("Empty or oversized screenshot.");

    var bytes = buffer.ToArray();
    var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "image/jpeg" : request.ContentType;

    var existing = await db.Screenshots.FirstOrDefaultAsync(s => s.EventId == id);
    if (existing is null)
    {
        db.Screenshots.Add(new StoredScreenshot
        {
            EventId = id,
            Jpeg = bytes,
            ContentType = contentType,
            ReceivedAt = DateTime.UtcNow
        });
    }
    else
    {
        existing.Jpeg = bytes;
        existing.ContentType = contentType;
        existing.ReceivedAt = DateTime.UtcNow;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { id });
}).RequireRateLimiting(IngestRateLimit);

app.MapGet("/events/{id:guid}/screenshot", async (Guid id, IntelligenceDbContext db, ClaimsPrincipal user) =>
{
    var scope = user.ProjectScope();
    if (scope is not null)
    {
        var proj = await db.Events.AsNoTracking()
            .Where(e => e.Id == id).Select(e => e.ProjectId).FirstOrDefaultAsync();
        if (proj != scope)
            return Results.NotFound();
    }

    var shot = await db.Screenshots.AsNoTracking().FirstOrDefaultAsync(s => s.EventId == id);
    return shot is null ? Results.NotFound() : Results.File(shot.Jpeg, shot.ContentType);
}).RequireAuthorization();

app.MapGet("/projects", async (IntelligenceDbContext db, ClaimsPrincipal user) =>
{
    // Pull only the columns needed and group in memory: the SQLite provider
    // doesn't translate this GroupBy-with-filtered-count shape, and the
    // projected set is small (project id / type / timestamp).
    var baseQuery = db.Events.AsNoTracking().AsQueryable();
    var scope = user.ProjectScope();
    if (scope is not null)
        baseQuery = baseQuery.Where(e => e.ProjectId == scope);

    var rows = await baseQuery
        .Select(e => new { e.ProjectId, e.EventType, e.ReceivedAt })
        .ToListAsync();

    var projects = rows
        .GroupBy(e => e.ProjectId)
        .Select(g => new ProjectSummary(
            g.Key,
            g.Count(),
            g.Count(e => e.EventType == "Exception"),
            g.Max(e => e.ReceivedAt)))
        .OrderByDescending(p => p.LastEventAt)
        .ToList();

    return Results.Ok(projects);
}).RequireAuthorization();

// Events-per-hour time series for the dashboard chart. Buckets are filled with
// zeros for quiet hours so the series is continuous. Grouping is done in memory
// (the window is bounded, and it avoids provider-specific date functions).
app.MapGet("/stats/events-per-hour", async (IntelligenceDbContext db, ClaimsPrincipal user, string? projectId, int hours = 24) =>
{
    hours = Math.Clamp(hours, 1, 168);

    var now = DateTime.UtcNow;
    var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
    var windowStart = currentHour.AddHours(-(hours - 1));

    var query = db.Events.AsNoTracking().Where(e => e.ReceivedAt >= windowStart);
    var projectFilter = user.ProjectScope() ?? projectId;
    if (!string.IsNullOrWhiteSpace(projectFilter))
        query = query.Where(e => e.ProjectId == projectFilter);

    var rows = await query.Select(e => new { e.ReceivedAt, e.EventType }).ToListAsync();

    var totals = new int[hours];
    var exceptions = new int[hours];
    foreach (var r in rows)
    {
        var index = (int)Math.Floor((r.ReceivedAt - windowStart).TotalHours);
        if (index < 0 || index >= hours)
            continue;

        totals[index]++;
        if (r.EventType == "Exception")
            exceptions[index]++;
    }

    var buckets = new TimeBucket[hours];
    for (var i = 0; i < hours; i++)
        buckets[i] = new TimeBucket(windowStart.AddHours(i), totals[i], exceptions[i]);

    return Results.Ok(buckets);
}).RequireAuthorization();

// Issues -----------------------------------------------------------------
// Grouped problems: one row per (project, fingerprint), newest activity first.
app.MapGet("/issues", async (IntelligenceDbContext db, ClaimsPrincipal user, string? projectId, int skip = 0, int take = 50) =>
{
    take = Math.Clamp(take, 1, 200);

    var projectFilter = user.ProjectScope() ?? projectId;

    var query = db.Issues.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(projectFilter))
        query = query.Where(i => i.ProjectId == projectFilter);

    var total = await query.CountAsync();

    var issues = await query
        .OrderByDescending(i => i.LastSeen)
        .Skip(skip)
        .Take(take)
        .ToListAsync();

    // Trend: occurrences in the last hour vs the hour before, per fingerprint.
    // Bounded to a 2-hour window and grouped in memory (small, provider-neutral).
    var now = DateTime.UtcNow;
    var recentStart = now.AddHours(-1);
    var previousStart = now.AddHours(-2);

    var trendQuery = db.Events.AsNoTracking().Where(e => e.ReceivedAt >= previousStart);
    if (!string.IsNullOrWhiteSpace(projectFilter))
        trendQuery = trendQuery.Where(e => e.ProjectId == projectFilter);

    var trendRows = await trendQuery.Select(e => new { e.Fingerprint, e.ReceivedAt }).ToListAsync();

    var recent = new Dictionary<string, int>();
    var previous = new Dictionary<string, int>();
    foreach (var r in trendRows)
    {
        var bucket = r.ReceivedAt >= recentStart ? recent : previous;
        bucket[r.Fingerprint] = bucket.GetValueOrDefault(r.Fingerprint) + 1;
    }

    var items = issues.Select(i => new IssueSummary(
        i.Id, i.ProjectId, i.Fingerprint, i.Title, i.Culprit, i.EventType, i.Level,
        i.EventCount, i.FirstSeen, i.LastSeen, i.LastEventId,
        recent.GetValueOrDefault(i.Fingerprint), previous.GetValueOrDefault(i.Fingerprint))).ToList();

    return Results.Ok(new PagedResult<IssueSummary>(total, skip, take, items));
}).RequireAuthorization();

app.MapGet("/issues/{id:guid}", async (Guid id, IntelligenceDbContext db, ClaimsPrincipal user) =>
{
    var i = await db.Issues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (i is null)
        return Results.NotFound();

    var scope = user.ProjectScope();
    if (scope is not null && i.ProjectId != scope)
        return Results.NotFound();

    var now = DateTime.UtcNow;
    var recentStart = now.AddHours(-1);
    var previousStart = now.AddHours(-2);

    var recent = await db.Events.AsNoTracking().CountAsync(e =>
        e.ProjectId == i.ProjectId && e.Fingerprint == i.Fingerprint && e.ReceivedAt >= recentStart);
    var previous = await db.Events.AsNoTracking().CountAsync(e =>
        e.ProjectId == i.ProjectId && e.Fingerprint == i.Fingerprint &&
        e.ReceivedAt >= previousStart && e.ReceivedAt < recentStart);

    return Results.Ok(new IssueSummary(
        i.Id, i.ProjectId, i.Fingerprint, i.Title, i.Culprit, i.EventType, i.Level,
        i.EventCount, i.FirstSeen, i.LastSeen, i.LastEventId, recent, previous));
}).RequireAuthorization();

app.MapGet("/issues/{id:guid}/events", async (Guid id, IntelligenceDbContext db, ClaimsPrincipal user, int skip = 0, int take = 50) =>
{
    take = Math.Clamp(take, 1, 200);

    var issue = await db.Issues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (issue is null)
        return Results.NotFound();

    var scope = user.ProjectScope();
    if (scope is not null && issue.ProjectId != scope)
        return Results.NotFound();

    var query = db.Events.AsNoTracking()
        .Where(e => e.ProjectId == issue.ProjectId && e.Fingerprint == issue.Fingerprint);

    var total = await query.CountAsync();

    var items = await query
        .OrderByDescending(e => e.ReceivedAt)
        .Skip(skip)
        .Take(take)
        .Select(e => new EventSummary(
            e.Id, e.ProjectId, e.ApplicationName, e.ApplicationVersion,
            e.Environment, e.Platform, e.DeviceName, e.EventType,
            e.Level, e.UserId,
            e.ExceptionType, e.ExceptionMessage, e.Message, e.Timestamp, e.ReceivedAt))
        .ToListAsync();

    return Results.Ok(new PagedResult<EventSummary>(total, skip, take, items));
}).RequireAuthorization();

// Project administration (tenants) --------------------------------------
// Admin-only: create/list/rotate/delete projects. Creating or rotating returns
// the read key ONCE (only its hash is stored).
app.MapPost("/admin/projects", async (CreateProjectRequest req, IntelligenceDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.ProjectId))
        return Results.BadRequest("projectId is required.");

    if (await db.Projects.AnyAsync(p => p.ProjectId == req.ProjectId))
        return Results.Conflict($"Project '{req.ProjectId}' already exists.");

    var readKey = ProjectKeys.NewReadKey();
    var project = new Project
    {
        Id = Guid.NewGuid(),
        ProjectId = req.ProjectId,
        ProjectKey = string.IsNullOrWhiteSpace(req.ProjectKey) ? ProjectKeys.NewProjectKey() : req.ProjectKey!,
        Name = req.Name ?? string.Empty,
        ReadKeyHash = ProjectKeys.Hash(readKey),
        CreatedAt = DateTime.UtcNow,
    };

    db.Projects.Add(project);
    await db.SaveChangesAsync();

    return Results.Created($"/admin/projects/{project.Id}", new ProjectCredentials(
        project.Id, project.ProjectId, project.ProjectKey, project.Name, project.CreatedAt, readKey));
}).RequireAuthorization(AdminOnly);

app.MapGet("/admin/projects", async (IntelligenceDbContext db) =>
{
    var projects = await db.Projects.AsNoTracking()
        .OrderBy(p => p.ProjectId)
        .Select(p => new ProjectInfo(p.Id, p.ProjectId, p.ProjectKey, p.Name, p.CreatedAt))
        .ToListAsync();
    return Results.Ok(projects);
}).RequireAuthorization(AdminOnly);

app.MapPost("/admin/projects/{id:guid}/rotate-key", async (Guid id, IntelligenceDbContext db) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
    if (project is null)
        return Results.NotFound();

    var readKey = ProjectKeys.NewReadKey();
    project.ReadKeyHash = ProjectKeys.Hash(readKey);
    await db.SaveChangesAsync();

    return Results.Ok(new ProjectCredentials(
        project.Id, project.ProjectId, project.ProjectKey, project.Name, project.CreatedAt, readKey));
}).RequireAuthorization(AdminOnly);

app.MapDelete("/admin/projects/{id:guid}", async (Guid id, IntelligenceDbContext db) =>
{
    var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
    if (project is null)
        return Results.NotFound();

    db.Projects.Remove(project);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization(AdminOnly);

app.MapHub<EventsHub>("/hubs/events").RequireAuthorization();

app.Run();

// Exposed so the integration test project can host the app via
// WebApplicationFactory<Program>. Top-level programs otherwise emit an internal
// Program class the test assembly can't reference.
public partial class Program { }
