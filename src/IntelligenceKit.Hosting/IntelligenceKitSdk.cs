using IntelligenceKit.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace IntelligenceKit.Hosting;

/// <summary>
/// Static entry point for apps without a DI container (classic WPF/WinForms,
/// console tools):
/// <code>
/// using var ik = IntelligenceKitSdk.Init("http://key@host:7099/my-app");
/// ...
/// IntelligenceKitSdk.Current?.TrackExceptionAsync(ex);
/// </code>
/// Disposing the returned instance ends the session and flushes pending events.
/// </summary>
public static class IntelligenceKitSdk
{
    private static IntelligenceKitInstance? _instance;

    /// <summary>The initialized SDK, or null before <see cref="Init"/>.</summary>
    public static IIntelligenceKit? Current => _instance?.Kit;

    /// <summary>The initialized instance, or null before <see cref="Init"/>.</summary>
    public static IntelligenceKitInstance? Instance => _instance;

    /// <summary>
    /// Builds and starts IntelligenceKit. Calling it again returns the existing
    /// instance. <paramref name="configureServices"/> lets platform SDKs add their
    /// own registrations (UI dispatcher, crash hooks) before the container is built.
    /// </summary>
    public static IntelligenceKitInstance Init(string dsn, Action<IntelligenceKitHostOptions>? configure = null,
        Action<IServiceCollection, IntelligenceKitHostOptions>? configureServices = null)
    {
        if (_instance is { } existing)
            return existing;

        var services = new ServiceCollection();
        var options = services.AddIntelligenceKitCore(dsn, configure);
        configureServices?.Invoke(services, options);

        var instance = new IntelligenceKitInstance(services.BuildServiceProvider());
        if (Interlocked.CompareExchange(ref _instance, instance, null) is { } raced)
        {
            instance.Dispose();
            return raced;
        }

        instance.Lifecycle.Start();
        return instance;
    }

    internal static void Clear(IntelligenceKitInstance instance)
        => Interlocked.CompareExchange(ref _instance, null, instance);
}

/// <summary>An initialized SDK and its services. Dispose on app exit.</summary>
public sealed class IntelligenceKitInstance : IDisposable, IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    internal IntelligenceKitInstance(ServiceProvider provider)
    {
        _provider = provider;
        Kit = provider.GetRequiredService<IIntelligenceKit>();
        Lifecycle = provider.GetRequiredService<IntelligenceKitLifecycle>();
    }

    public IIntelligenceKit Kit { get; }

    public IServiceProvider Services => _provider;

    internal IntelligenceKitLifecycle Lifecycle { get; }

    public async ValueTask DisposeAsync()
    {
        await Lifecycle.StopAsync().ConfigureAwait(false);
        IntelligenceKitSdk.Clear(this);
        await _provider.DisposeAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        try
        {
            // Bounded by ShutdownFlushTimeout; run off any UI context to avoid deadlocks.
            Task.Run(() => Lifecycle.StopAsync()).GetAwaiter().GetResult();
        }
        catch
        {
        }
        IntelligenceKitSdk.Clear(this);
        _provider.Dispose();
    }
}
