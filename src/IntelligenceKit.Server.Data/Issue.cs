namespace IntelligenceKit.Server.Data;

/// <summary>
/// A group of events that share a fingerprint — one recurring problem. Repeated
/// occurrences increment <see cref="EventCount"/> and push <see cref="LastSeen"/>
/// forward instead of creating new rows.
/// </summary>
public class Issue
{
    public Guid Id { get; set; }

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Stable grouping key (see <c>EventFingerprint</c>). Unique per project.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>Short label, e.g. the exception's short type name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Where it happens, e.g. "CheckoutPage.OnPay". Null when unknown.</summary>
    public string? Culprit { get; set; }

    /// <summary>Kind of the events in this group (Exception, Log, ...).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Severity of the most recent event, if any.</summary>
    public string? Level { get; set; }

    public long EventCount { get; set; }

    public DateTime FirstSeen { get; set; }

    public DateTime LastSeen { get; set; }

    /// <summary>Id of the most recently ingested event in this group.</summary>
    public Guid LastEventId { get; set; }
}
