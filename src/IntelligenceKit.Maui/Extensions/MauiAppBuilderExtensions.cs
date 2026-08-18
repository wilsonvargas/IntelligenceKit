using IntelligenceKit.Core.Configuration;
using IntelligenceKit.Core.Diagnostics;
using IntelligenceKit.Core.Providers;
using IntelligenceKit.Core.Services;
using IntelligenceKit.Core.Storage;
using IntelligenceKit.Maui.CrashReporting;
using IntelligenceKit.Maui.Diagnostics;
using IntelligenceKit.Maui.Providers;
using IntelligenceKit.Maui.Services;
using IntelligenceKit.Maui.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace IntelligenceKit.Maui.Extensions;

public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers IntelligenceKit using a single DSN string, e.g.
    /// <c>http://projectKey@host:5000/projectId</c>. Application name and
    /// version are auto-detected from <see cref="AppInfo"/>. Pass
    /// <paramref name="configure"/> only for advanced overrides.
    /// </summary>
    public static MauiAppBuilder UseIntelligenceKit(this MauiAppBuilder builder, string dsn, Action<IntelligenceOptions>? configure = null)
    {
        var parsed = IntelligenceDsn.Parse(dsn);

        var options = new IntelligenceOptions
        {
            ServerUrl = parsed.ServerUrl,
            ProjectKey = parsed.ProjectKey,
            ProjectId = parsed.ProjectId,
            ApplicationName = AppInfo.Current.Name,
            ApplicationVersion = AppInfo.Current.VersionString
        };

        configure?.Invoke(options);

        builder.Services.AddSingleton(options);

        builder.Services.AddHttpClient<IIntelligenceClient, HttpIntelligenceClient>();
        builder.Services.AddSingleton<IIntelligenceKit, IntelligenceKitService>();
        builder.Services.AddSingleton<IDeviceContextProvider, MauiDeviceContextProvider>();

        // Rich context: breadcrumb ring buffer, navigation trail, runtime snapshot.
        builder.Services.AddSingleton<IBreadcrumbBuffer, BreadcrumbBuffer>();
        builder.Services.AddSingleton<NavigationTracker>();
        builder.Services.AddSingleton<IRuntimeContextProvider, MauiRuntimeContextProvider>();

        // Screen capture: proactive "last screen" buffer + capture service.
        builder.Services.AddSingleton<LastScreenBuffer>();
        builder.Services.AddSingleton<ILastScreenProvider>(sp => sp.GetRequiredService<LastScreenBuffer>());
        builder.Services.AddSingleton<ScreenCaptureService>();

        // Offline store-and-forward: persist events to SQLite, drain via the uploader.
        var databasePath = Path.Combine(FileSystem.Current.AppDataDirectory, "intelligencekit.db3");
        builder.Services.AddSingleton<IEventStore>(_ => new SqliteEventStore(databasePath));
        builder.Services.AddSingleton<IScreenshotStore>(_ => new SqliteScreenshotStore(databasePath));
        builder.Services.AddSingleton<IEventUploader, EventUploader>();

        // Crash reporting + startup work (register handlers, initial flush,
        // flush on reconnect). Activated automatically; no host-app code needed.
        builder.Services.AddSingleton<ICrashReporter, CrashReporter>();
        builder.Services.AddSingleton<IMauiInitializeService, IntelligenceKitStartup>();

        return builder;
    }
}
