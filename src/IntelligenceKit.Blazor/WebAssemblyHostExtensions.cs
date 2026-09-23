using IntelligenceKit.Core.Enums;
using IntelligenceKit.Core.Models;
using IntelligenceKit.Core.Services;
using IntelligenceKit.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace IntelligenceKit.Blazor;

public static class WebAssemblyHostExtensions
{
    /// <summary>
    /// Registers IntelligenceKit for a Blazor WebAssembly app:
    /// <code>
    /// builder.UseIntelligenceKit("http://key@host:7099/my-spa");
    /// var host = builder.Build();
    /// host.StartIntelligenceKit();
    /// await host.RunAsync();
    /// </code>
    /// Unhandled component exceptions reach IntelligenceKit through the logger
    /// integration (Blazor logs them as Critical). The browser has no durable
    /// storage here, so the queue is in memory, and sessions are off (a tab can't
    /// report its own end reliably).
    /// </summary>
    public static WebAssemblyHostBuilder UseIntelligenceKit(this WebAssemblyHostBuilder builder, string dsn,
        Action<IntelligenceKitHostOptions>? configure = null)
    {
        builder.Services.AddIntelligenceKitCore(dsn, options =>
        {
            options.UseInMemoryStore = true;
            options.EnableAutoSessionTracking = false;
            options.Environment = builder.HostEnvironment.Environment.ToLowerInvariant();
            configure?.Invoke(options);
        });
        builder.Services.AddSingleton<BlazorNavigationTracker>();
        return builder;
    }

    /// <summary>Starts IntelligenceKit and navigation breadcrumbs. Call after <c>Build()</c>.</summary>
    public static WebAssemblyHost StartIntelligenceKit(this WebAssemblyHost host)
    {
        host.Services.StartIntelligenceKit();
        host.Services.GetService<BlazorNavigationTracker>()?.Start();
        return host;
    }
}

/// <summary>Records a <c>navigation</c> breadcrumb for every client-side route change.</summary>
public sealed class BlazorNavigationTracker
{
    private readonly NavigationManager _navigation;
    private readonly IIntelligenceKit _kit;
    private bool _started;

    public BlazorNavigationTracker(NavigationManager navigation, IIntelligenceKit kit)
    {
        _navigation = navigation;
        _kit = kit;
    }

    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _navigation.LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        try
        {
            // Path only: query strings may carry tokens or personal data.
            var path = "/" + _navigation.ToBaseRelativePath(e.Location).Split('?', '#')[0];
            _kit.AddBreadcrumb(path, BreadcrumbCategories.Navigation, SeverityLevel.Information);
        }
        catch
        {
        }
    }
}
