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
/// trend arrow. <c>Status</c> is Unresolved | Resolved | Ignored; <c>IsRegression</c>
/// flags a resolved issue that came back.
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
    int PreviousCount,
    string Status = "Unresolved",
    bool IsRegression = false,
    string? AssignedTo = null,
    DateTime? ResolvedAt = null,
    string? ResolvedInRelease = null,
    DateTime? RegressedAt = null);

/// <summary>
/// Triage update for an issue (PATCH /issues/{id}). Every field is optional; only
/// the ones present are applied. <c>AssignedTo</c> = "" clears the assignee.
/// <c>ResolvedInRelease</c> only applies together with <c>Status = Resolved</c>.
/// </summary>
public record UpdateIssueRequest(string? Status = null, string? AssignedTo = null, string? ResolvedInRelease = null);

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

/// <summary>An alert rule as returned by the API. <c>HasSecret</c> hides the signing key itself.</summary>
public record AlertRuleInfo(
    Guid Id,
    string? ProjectId,
    string Name,
    string Trigger,
    int ThresholdCount,
    int ThresholdWindowMinutes,
    string Channel,
    string Target,
    bool HasSecret,
    bool Enabled,
    int CooldownMinutes,
    DateTime CreatedAt);

/// <summary>
/// Create/replace an alert rule. <c>Trigger</c>: NewIssue | Regression | Threshold
/// (Threshold also needs <c>ThresholdCount</c> and <c>ThresholdWindowMinutes</c>).
/// <c>Channel</c>: Webhook | Slack | Teams | Discord | Email; <c>Target</c> is the
/// webhook URL or comma-separated email recipients. <c>ProjectId</c> null = all projects.
/// </summary>
public record UpsertAlertRuleRequest(
    string Name,
    string Trigger,
    string Channel,
    string Target,
    string? ProjectId = null,
    int ThresholdCount = 0,
    int ThresholdWindowMinutes = 0,
    string? Secret = null,
    bool Enabled = true,
    int CooldownMinutes = 60);

/// <summary>One entry in the alert delivery history. <c>Success</c> null = still pending.</summary>
public record AlertNotificationInfo(
    Guid Id,
    Guid RuleId,
    string RuleName,
    string ProjectId,
    Guid? IssueId,
    string? IssueTitle,
    string Trigger,
    string Channel,
    DateTime CreatedAt,
    bool? Success,
    string? Error,
    DateTime? DeliveredAt);
