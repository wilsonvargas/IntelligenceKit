using System.Text.Json;
using System.Text.Json.Serialization;
using IntelligenceKit.Core.Models;
using IntelligenceKit.Server;
using IntelligenceKit.Server.Auth;
using IntelligenceKit.Server.Contracts;
using IntelligenceKit.Server.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

const string DashboardCors = "dashboard";

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

// Read-side auth: a single shared admin token gates every query endpoint and the
// SignalR hub. Ingest (POST /events) stays open by design — the client's project
// key is a public routing id, not a secret. See ReadTokenAuthHandler.
builder.Services
    .AddAuthentication(ReadTokenDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ReadTokenAuthHandler>(ReadTokenDefaults.Scheme, null);
builder.Services.AddAuthorization();

// The Blazor WASM dashboard is served from its own origin, so it needs CORS.
// Auth is via bearer token (no cookies/credentials), so any origin may call.
builder.Services.AddCors(options =>
{
    options.AddPolicy(DashboardCors, policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors(DashboardCors);
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
app.MapPost("/events", async (IntelligenceEvent intelligenceEvent, HttpRequest request, IntelligenceDbContext db, IHubContext<EventsHub> hub) =>
{
    var eventId = intelligenceEvent.Id == Guid.Empty ? Guid.NewGuid() : intelligenceEvent.Id;

    // Idempotent ingest: a client may re-send an already-delivered event while
    // retrying its screenshot upload. Treat a duplicate as success, don't insert
    // a second row, and don't re-broadcast.
    if (await db.Events.AnyAsync(e => e.Id == eventId))
        return Results.Ok(new { Id = eventId });

    var stored = new StoredEvent
    {
        Id = eventId,
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
    await hub.Clients.All.SendAsync("eventReceived", summary);

    return Results.Created($"/events/{stored.Id}", new { stored.Id });
});

// Query ------------------------------------------------------------------
app.MapGet("/events", async (IntelligenceDbContext db, string? projectId, string? eventType, int skip = 0, int take = 50) =>
{
    take = Math.Clamp(take, 1, 200);

    var query = db.Events.AsNoTracking().AsQueryable();

    if (!string.IsNullOrWhiteSpace(projectId))
        query = query.Where(e => e.ProjectId == projectId);

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

app.MapGet("/events/{id:guid}", async (Guid id, IntelligenceDbContext db) =>
{
    var e = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    if (e is null)
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
});

app.MapGet("/events/{id:guid}/screenshot", async (Guid id, IntelligenceDbContext db) =>
{
    var shot = await db.Screenshots.AsNoTracking().FirstOrDefaultAsync(s => s.EventId == id);
    return shot is null ? Results.NotFound() : Results.File(shot.Jpeg, shot.ContentType);
}).RequireAuthorization();

app.MapGet("/projects", async (IntelligenceDbContext db) =>
{
    // Pull only the columns needed and group in memory: the SQLite provider
    // doesn't translate this GroupBy-with-filtered-count shape, and the
    // projected set is small (project id / type / timestamp).
    var rows = await db.Events.AsNoTracking()
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
app.MapGet("/stats/events-per-hour", async (IntelligenceDbContext db, string? projectId, int hours = 24) =>
{
    hours = Math.Clamp(hours, 1, 168);

    var now = DateTime.UtcNow;
    var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
    var windowStart = currentHour.AddHours(-(hours - 1));

    var query = db.Events.AsNoTracking().Where(e => e.ReceivedAt >= windowStart);
    if (!string.IsNullOrWhiteSpace(projectId))
        query = query.Where(e => e.ProjectId == projectId);

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

app.MapHub<EventsHub>("/hubs/events").RequireAuthorization();

app.Run();
