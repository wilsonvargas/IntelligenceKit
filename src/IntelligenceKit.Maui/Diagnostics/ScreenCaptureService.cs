using System.Linq;
using IntelligenceKit.Core.Configuration;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Media;

namespace IntelligenceKit.Maui.Diagnostics;

/// <summary>
/// Proactively captures a downscaled screenshot and keeps only the latest frame
/// in <see cref="LastScreenBuffer"/>, so a crash can carry "the last screen the
/// user saw" without ever capturing on the dying thread. Opt-in and throttled;
/// sensitive pages are skipped.
/// </summary>
public sealed class ScreenCaptureService
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(800);

    private readonly LastScreenBuffer _buffer;
    private readonly IntelligenceOptions _options;

    private DateTime _lastCaptureUtc = DateTime.MinValue;
    private bool _started;

    public ScreenCaptureService(LastScreenBuffer buffer, IntelligenceOptions options)
    {
        _buffer = buffer;
        _options = options;
    }

    public void Start()
    {
        if (_started || !_options.EnableScreenCapture)
            return;

        _started = true;

        // Initialize() runs during MauiApp build, BEFORE the App is created, so
        // Application.Current is null here. Defer subscription until it exists.
        _ = SubscribeWhenReadyAsync();
    }

    private async Task SubscribeWhenReadyAsync()
    {
        var app = await WaitForApplicationAsync();
        if (app is null)
            return;

        app.PageAppearing += OnPageAppearing;
        app.PageDisappearing += OnPageDisappearing;

        // The first page has usually already appeared by now, and a screenshot
        // needs a rendered frame, so seed the buffer with a few retried attempts.
        await PrimeAsync();
    }

    private static async Task<Application?> WaitForApplicationAsync()
    {
        for (var i = 0; i < 100 && Application.Current is null; i++)
            await Task.Delay(100);

        return Application.Current;
    }

    private async void OnPageAppearing(object? sender, Page page)
    {
        // async void: must never let an exception escape.
        try
        {
            // Give the page a moment to render before grabbing it.
            await Task.Delay(350);
            await CaptureAsync(page.GetType().Name, force: false);
        }
        catch
        {
            // ignored — capture is best-effort
        }
    }

    private async void OnPageDisappearing(object? sender, Page page)
    {
        try
        {
            // Capture the screen the user is leaving too.
            await CaptureAsync(page.GetType().Name, force: false);
        }
        catch
        {
            // ignored — capture is best-effort
        }
    }

    private async Task PrimeAsync()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(700));
            await CaptureAsync(CurrentPageName(), force: true);

            if (_buffer.GetLastScreenshot() is { Length: > 0 })
                return;
        }
    }

    private async Task CaptureAsync(string screen, bool force)
    {
        if (!_options.EnableScreenCapture)
            return;

        // Never keep a frame of a sensitive screen.
        if (!string.IsNullOrEmpty(screen) && _options.ScreenCaptureExcludedPages.Contains(screen))
        {
            _buffer.Clear();
            return;
        }

        if (!force && DateTime.UtcNow - _lastCaptureUtc < MinInterval)
            return;

        // The capture runs on the UI thread. CRUCIAL: the try/catch must live
        // INSIDE the main-thread delegate and return null on failure — an
        // exception thrown here would otherwise escape across the thread boundary
        // and crash the app (a screenshot must never do that). IsCaptureSupported
        // is intentionally not a hard gate (false negatives on emulators).
        string? error = null;
        byte[]? jpeg = null;

        try
        {
            jpeg = await MainThread.InvokeOnMainThreadAsync<byte[]?>(async () =>
            {
                try
                {
                    var result = await Screenshot.Default.CaptureAsync();
                    if (result is null)
                        return null;

                    await using var raw = await result.OpenReadAsync(ScreenshotFormat.Png);
                    using var original = new MemoryStream();
                    await raw.CopyToAsync(original);
                    var bytes = original.ToArray();

                    try
                    {
                        return Downscale(bytes, _options.ScreenCaptureMaxDimension, _options.ScreenCaptureJpegQuality);
                    }
                    catch
                    {
                        // Graphics resize/encode unavailable — keep the full-size PNG.
                        return bytes;
                    }
                }
                catch (Exception inner)
                {
                    error = $"{inner.GetType().Name}: {inner.Message}";
                    return null;
                }
            });
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
        }

        if (jpeg is { Length: > 0 })
        {
            _buffer.Update(jpeg);
            _lastCaptureUtc = DateTime.UtcNow;
            Log($"screenshot captured: {jpeg.Length} bytes on {screen}");
        }
        else
        {
            Log($"screenshot capture failed on {screen}: {error ?? "no data"}");
        }
    }

    // Internal SDK diagnostics go to the platform log only — never to the event's
    // breadcrumb trail, which is reserved for what actually happened in the app.
    private static void Log(string message)
        => System.Diagnostics.Debug.WriteLine($"[IntelligenceKit] {message}");

    private static byte[] Downscale(byte[] png, int maxDimension, float quality)
    {
        using var input = new MemoryStream(png);
        var image = PlatformImage.FromStream(input);
        using var resized = image.Downsize(maxDimension, disposeOriginal: true);

        using var output = new MemoryStream();
        resized.Save(output, ImageFormat.Jpeg, quality);
        return output.ToArray();
    }

    /// <summary>Best-effort name of the page currently visible to the user.</summary>
    private static string CurrentPageName()
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;

        page = page switch
        {
            Shell shell => shell.CurrentPage,
            NavigationPage nav => nav.CurrentPage,
            TabbedPage tab => tab.CurrentPage,
            FlyoutPage flyout => flyout.Detail,
            _ => page
        };

        if (page is NavigationPage inner)
            page = inner.CurrentPage;

        return page?.GetType().Name ?? string.Empty;
    }
}
