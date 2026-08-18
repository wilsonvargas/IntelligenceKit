using IntelligenceKit.Core.Configuration;
using IntelligenceKit.Core.Diagnostics;
using IntelligenceKit.Core.Enums;
using IntelligenceKit.Core.Models;
using Microsoft.Maui.Controls;

namespace IntelligenceKit.Maui.Diagnostics;

/// <summary>
/// Records a navigation breadcrumb every time a page appears and remembers the
/// current screen so the runtime snapshot can report it. This is what produces
/// the "Previous Actions" trail leading up to a crash.
/// </summary>
public sealed class NavigationTracker
{
    private readonly IBreadcrumbBuffer _breadcrumbs;
    private readonly IntelligenceOptions _options;
    private bool _started;

    public NavigationTracker(IBreadcrumbBuffer breadcrumbs, IntelligenceOptions options)
    {
        _breadcrumbs = breadcrumbs;
        _options = options;
    }

    /// <summary>The page the user is currently on, if known.</summary>
    public string? CurrentScreen { get; private set; }

    public void Start()
    {
        if (_started)
            return;

        _started = true;

        // Initialize() runs before the App exists (Application.Current is null),
        // so defer the subscription until it becomes available.
        _ = SubscribeWhenReadyAsync();
    }

    private async Task SubscribeWhenReadyAsync()
    {
        for (var i = 0; i < 100 && Application.Current is null; i++)
            await Task.Delay(100);

        var app = Application.Current;
        if (app is null)
            return;

        app.PageAppearing += OnPageAppearing;
    }

    private void OnPageAppearing(object? sender, Page page)
    {
        var screen = page.GetType().Name;
        CurrentScreen = screen;

        if (_options.EnableNavigationBreadcrumbs)
        {
            _breadcrumbs.Add(new Breadcrumb
            {
                Category = BreadcrumbCategories.Navigation,
                Message = screen,
                Level = SeverityLevel.Information
            });
        }
    }
}
