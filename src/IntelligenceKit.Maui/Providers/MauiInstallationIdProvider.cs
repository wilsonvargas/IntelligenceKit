using IntelligenceKit.Core.Providers;

namespace IntelligenceKit.Maui.Providers;

/// <summary>
/// Anonymous installation id kept in app Preferences: generated on first use,
/// stable across launches, reset only when the app is reinstalled or its data
/// cleared. Used to count affected users for release health.
/// </summary>
public sealed class MauiInstallationIdProvider : IInstallationIdProvider
{
    private const string Key = "intelligencekit.installation_id";
    private string? _cached;

    public string GetInstallationId()
    {
        if (_cached is not null)
            return _cached;

        var id = Preferences.Default.Get<string?>(Key, null);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N");
            Preferences.Default.Set(Key, id);
        }

        return _cached = id;
    }
}
