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
