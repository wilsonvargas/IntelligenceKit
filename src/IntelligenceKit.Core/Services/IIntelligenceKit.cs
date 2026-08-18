using IntelligenceKit.Core.Enums;
using IntelligenceKit.Core.Models;

namespace IntelligenceKit.Core.Services;

public interface IIntelligenceKit
{
    Task TrackAsync(IntelligenceEvent intelligenceEvent);

    Task TrackExceptionAsync(Exception exception);

    Task TrackExceptionAsync(ExceptionInfo exception);

    /// <summary>
    /// Captures a log message as an event (Sentry's "capture message"). Also
    /// records it as a breadcrumb so it shows up in the trail of later events.
    /// </summary>
    Task TrackLogAsync(SeverityLevel level, string message, IDictionary<string, string>? data = null);

    /// <summary>
    /// Adds a breadcrumb to the local trail. Breadcrumbs are NOT sent on their
    /// own — they ride along with the next event that is captured.
    /// </summary>
    void AddBreadcrumb(string message, string category = BreadcrumbCategories.Custom,
        SeverityLevel level = SeverityLevel.Information, IDictionary<string, string>? data = null);

    /// <summary>Associates subsequent events with a user id (opt-in). Pass null to clear.</summary>
    void SetUser(string? userId);

    /// <summary>Sets an indexable tag on subsequent events. Pass null value to remove.</summary>
    void SetTag(string key, string? value);

    /// <summary>
    /// Persists a fatal exception to the offline store WITHOUT attempting a
    /// network send. Used from crash handlers, where the process is about to
    /// die: a local write is fast and reliable, and the event is uploaded on the
    /// next launch. Avoids blocking the crashing thread on a network call.
    /// </summary>
    Task CaptureCrashAsync(ExceptionInfo exception);
}
