using IntelligenceKit.Core.Services;
using IntelligenceKit.Maui.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Networking;

namespace IntelligenceKit.Maui.CrashReporting;

/// <summary>
/// Runs during <c>MauiApp</c> construction and wires up IntelligenceKit's
/// runtime behavior: activates crash capture, uploads anything left over from a
/// previous session, and re-drains the offline queue whenever connectivity is
/// restored. Keeps all of this out of the host app.
/// </summary>
internal sealed class IntelligenceKitStartup : IMauiInitializeService
{
    public void Initialize(IServiceProvider services)
    {
        services.GetRequiredService<ICrashReporter>().Register();

        // Begin recording navigation breadcrumbs (records the current screen too).
        services.GetRequiredService<NavigationTracker>().Start();

        // Begin proactive screen capture (no-op unless EnableScreenCapture is set).
        services.GetRequiredService<ScreenCaptureService>().Start();

        var uploader = services.GetRequiredService<IEventUploader>();

        // Upload events persisted before this launch (e.g. a crash last session).
        _ = uploader.FlushAsync();

        Connectivity.Current.ConnectivityChanged += (_, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
                _ = uploader.FlushAsync();
        };
    }
}
