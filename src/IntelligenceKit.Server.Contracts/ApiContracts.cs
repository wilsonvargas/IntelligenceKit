using System.Text.Json;
using IntelligenceKit.Core.Models;

namespace IntelligenceKit.Server.Contracts;

/// <summary>A page of results plus the total count, so clients can paginate.</summary>
public record PagedResult<T>(int Total, int Skip, int Take, IReadOnlyList<T> Items);

/// <summary>Lightweight row for the events list / tables.</summary>
public record EventSummary(
    Guid Id,
    string ProjectId,
    string ApplicationName,
    string ApplicationVersion,
    string Environment,
    string Platform,
    string DeviceName,
    string EventType,
    string? Level,
    string? UserId,
    string? ExceptionType,
    string? ExceptionMessage,
    string? Message,
    DateTime Timestamp,
    DateTime ReceivedAt);

/// <summary>One hour of the events-per-hour time series.</summary>
public record TimeBucket(DateTime Start, int Total, int Exceptions);

/// <summary>
/// A grouped problem for the Issues view. <c>RecentCount</c>/<c>PreviousCount</c>
/// are occurrences in the last hour vs the hour before, so the client can draw a
/// trend arrow.
/// </summary>
public record IssueSummary(
    Guid Id,
    string ProjectId,
    string Fingerprint,
    string Title,
    string? Culprit,
    string EventType,
    string? Level,
    long EventCount,
    DateTime FirstSeen,
    DateTime LastSeen,
    Guid LastEventId,
    int RecentCount,
    int PreviousCount);

/// <summary>Per-project rollup for the projects overview.</summary>
public record ProjectSummary(
    string ProjectId,
    int EventCount,
    int ExceptionCount,
    DateTime LastEventAt);

/// <summary>Body for creating a project. ProjectKey is optional — generated when omitted.</summary>
public record CreateProjectRequest(string ProjectId, string Name, string? ProjectKey);

/// <summary>A registered project as returned by the admin API (never includes the read key).</summary>
public record ProjectInfo(
    Guid Id,
    string ProjectId,
    string ProjectKey,
    string Name,
    DateTime CreatedAt);

/// <summary>
/// Returned once when a project is created or its key is rotated — the only time
/// the plaintext <c>ReadKey</c> is ever exposed (only its hash is stored).
/// </summary>
public record ProjectCredentials(
    Guid Id,
    string ProjectId,
    string ProjectKey,
    string Name,
    DateTime CreatedAt,
    string ReadKey);

/// <summary>Full event, with the exception tree, context and trail already parsed.</summary>
public record EventDetail(
    Guid Id,
    string ProjectId,
    string ProjectKey,
    string ApplicationName,
    string ApplicationVersion,
    string Environment,
    string Release,
    string Platform,
    string DeviceName,
    string DeviceModel,
    string Manufacturer,
    string OperatingSystem,
    string? UserId,
    string EventType,
    string? Level,
    string? Message,
    ExceptionInfo? Exception,
    DeviceRuntime? DeviceRuntime,
    IReadOnlyList<Breadcrumb> Breadcrumbs,
    Dictionary<string, string>? Tags,
    Dictionary<string, JsonElement>? Data,
    bool HasScreenshot,
    DateTime Timestamp,
    DateTime ReceivedAt);
