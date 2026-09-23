using IntelligenceKit.Server.Contracts;
using IntelligenceKit.Server.Data;

namespace IntelligenceKit.Server;

/// <summary>Entity → contract projections shared by the endpoints and the live push.</summary>
public static class Mapping
{
    public static IssueSummary ToSummary(this Issue i, int recentCount = 0, int previousCount = 0)
        => new(
            i.Id, i.ProjectId, i.Fingerprint, i.Title, i.Culprit, i.EventType, i.Level,
            i.EventCount, i.FirstSeen, i.LastSeen, i.LastEventId, recentCount, previousCount,
            i.Status, i.IsRegression, i.AssignedTo, i.ResolvedAt, i.ResolvedInRelease, i.RegressedAt);

    public static EventSummary ToSummary(this StoredEvent e)
        => new(
            e.Id, e.ProjectId, e.ApplicationName, e.ApplicationVersion,
            e.Environment, e.Platform, e.DeviceName, e.EventType,
            e.Level, e.UserId,
            e.ExceptionType, e.ExceptionMessage, e.Message,
            e.Timestamp, e.ReceivedAt);
}
