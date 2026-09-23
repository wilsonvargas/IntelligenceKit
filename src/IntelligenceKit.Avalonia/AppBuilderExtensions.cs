using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using IntelligenceKit.Core.Enums;
using IntelligenceKit.Core.Models;
using IntelligenceKit.Hosting;

namespace IntelligenceKit.Avalonia;

public static class AppBuilderExtensions
{
    /// <summary>
    /// IntelligenceKit for Avalonia desktop apps, in <c>BuildAvaloniaApp()</c>:
    /// <code>
    /// AppBuilder.Configure&lt;App&gt;()
    ///     .UsePlatformDetect()
    ///     .UseIntelligenceKit("http://key@host:7099/my-avalonia-app");
    /// </code>
    /// Captures fatal crashes (persisted, sent on next launch), dispatcher exceptions
    /// the app survives, a frozen UI thread (ANR), window breadcrumbs, logged errors
    /// and one session per app run; flushes on exit.
    /// </summary>
    public static AppBuilder UseIntelligenceKit(this AppBuilder builder, string dsn,
        Action<IntelligenceKitHostOptions>? configure = null)
        => builder.AfterSetup(_ =>
        {
            var instance = IntelligenceKitSdk.Init(dsn, configure, (services, options) =>
                UiHostIntegration.AddUiThreadWatchdog(services, options, new AvaloniaSynchronizationContext()));

            Dispatcher.UIThread.UnhandledException += (_, e) =>
                UiHostIntegration.OnDispatcherException(e.Exception,
                    action => Dispatcher.UIThread.Post(action, DispatcherPriority.Background));

            Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
            {
                try
                {
                    instance.Kit.AddBreadcrumb(window.GetType().Name, BreadcrumbCategories.Navigation, SeverityLevel.Information);
                }
                catch
                {
                }
            });

            if (Application.Current?.ApplicationLifetime is IControlledApplicationLifetime lifetime)
                lifetime.Exit += (_, _) => instance.Dispose();
            else
                AppDomain.CurrentDomain.ProcessExit += (_, _) => instance.Dispose();
        });
}
