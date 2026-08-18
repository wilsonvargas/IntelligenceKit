using IntelligenceKit.Core.Enums;

namespace IntelligenceKit.Core.Models;

public class IntelligenceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ProjectId { get; set; } = string.Empty;

    public string ApplicationName { get; set; } = string.Empty;

    public string ApplicationVersion { get; set; } = string.Empty;

    /// <summary>Deployment environment, e.g. "production", "staging".</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>Release/build identifier. Defaults to the application version.</summary>
    public string Release { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string DeviceModel { get; set; } = string.Empty;

    public string Manufacturer { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    /// <summary>Optional, opt-in identifier for the affected user (anonymous by default).</summary>
    public string? UserId { get; set; }

    /// <summary>Severity for log events. Null for non-log events.</summary>
    public SeverityLevel? Level { get; set; }

    /// <summary>Human-readable message for log events.</summary>
    public string? Message { get; set; }

    /// <summary>
    /// Typed exception payload. Null for non-exception events.
    /// </summary>
    public ExceptionInfo? Exception { get; set; }

    public EventType EventType { get; set; } = Enums.EventType.Unknown;

    /// <summary>Runtime snapshot (memory/battery/network/screen) at capture time.</summary>
    public DeviceRuntime? DeviceRuntime { get; set; }

    /// <summary>Trail of activity leading up to this event.</summary>
    public List<Breadcrumb> Breadcrumbs { get; set; } = new();

    /// <summary>Free-form indexable labels for filtering/search.</summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Dictionary<string, object?> Data { get; set; } = new();
}
