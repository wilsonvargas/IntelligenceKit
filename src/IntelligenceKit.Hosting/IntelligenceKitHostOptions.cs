using IntelligenceKit.Core.Configuration;

namespace IntelligenceKit.Hosting;

/// <summary>
/// <see cref="IntelligenceOptions"/> plus the settings that only matter to
/// generic hosts. Registered as the <see cref="IntelligenceOptions"/> singleton.
/// </summary>
public class IntelligenceKitHostOptions : IntelligenceOptions
{
    /// <summary>
    /// Directory of the offline queue. Defaults to
    /// <c>%LOCALAPPDATA%/IntelligenceKit/{projectId}/queue</c> (or the platform equivalent).
    /// </summary>
    public string? OfflineStorePath { get; set; }

    /// <summary>
    /// Keep the offline queue in memory only (automatic in the browser). Pending
    /// events are lost when the process exits, including crashes.
    /// </summary>
    public bool UseInMemoryStore { get; set; } = OperatingSystem.IsBrowser();

    /// <summary>Hook <c>AppDomain.UnhandledException</c> / unobserved task exceptions.</summary>
    public bool EnableCrashHandlers { get; set; } = true;

    /// <summary>How long shutdown waits for pending events to upload.</summary>
    public TimeSpan ShutdownFlushTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
