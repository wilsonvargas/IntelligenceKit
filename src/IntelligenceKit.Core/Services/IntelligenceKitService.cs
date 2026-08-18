using System.Collections.Concurrent;
using IntelligenceKit.Core.Configuration;
using IntelligenceKit.Core.Diagnostics;
using IntelligenceKit.Core.Enums;
using IntelligenceKit.Core.Models;
using IntelligenceKit.Core.Providers;
using IntelligenceKit.Core.Storage;

namespace IntelligenceKit.Core.Services;

public class IntelligenceKitService : IIntelligenceKit
{
    private readonly IEventStore _store;
    private readonly IEventUploader _uploader;
    private readonly IntelligenceOptions _options;
    private readonly IDeviceContextProvider _device;
    private readonly IRuntimeContextProvider _runtime;
    private readonly IBreadcrumbBuffer _breadcrumbs;
    private readonly ILastScreenProvider _lastScreen;
    private readonly IScreenshotStore _screenshots;

    // Mutable per-session scope. This service is a singleton, so these carry
    // across events until changed.
    private volatile string? _userId;
    private readonly ConcurrentDictionary<string, string> _tags = new();

    public IntelligenceKitService(
        IEventStore store,
        IEventUploader uploader,
        IntelligenceOptions options,
        IDeviceContextProvider device,
        IRuntimeContextProvider runtime,
        IBreadcrumbBuffer breadcrumbs,
        ILastScreenProvider lastScreen,
        IScreenshotStore screenshots)
    {
        _store = store;
        _uploader = uploader;
        _options = options;
        _device = device;
        _runtime = runtime;
        _breadcrumbs = breadcrumbs;
        _lastScreen = lastScreen;
        _screenshots = screenshots;
    }

    public async Task TrackAsync(IntelligenceEvent intelligenceEvent)
    {
        Enrich(intelligenceEvent);

        // Store-and-forward: persist first (durable even if the app dies now),
        // then opportunistically drain the queue to the server.
        await _store.SaveAsync(intelligenceEvent);
        await _uploader.FlushAsync();
    }

    public Task TrackExceptionAsync(Exception exception)
    {
        return TrackExceptionAsync(ExceptionInfo.FromException(exception));
    }

    public async Task TrackExceptionAsync(ExceptionInfo exception)
    {
        var exceptionEvent = BuildExceptionEvent(exception);
        await AttachScreenshotAsync(exceptionEvent);
        await TrackAsync(exceptionEvent);
    }

    public Task TrackLogAsync(SeverityLevel level, string message, IDictionary<string, string>? data = null)
    {
        // A captured log is also a breadcrumb for whatever comes after it.
        AddBreadcrumb(message, BreadcrumbCategories.Log, level, data);

        var logEvent = new IntelligenceEvent
        {
            EventType = EventType.Log,
            Level = level,
            Message = message
        };

        if (data is not null)
        {
            foreach (var kv in data)
                logEvent.Data[kv.Key] = kv.Value;
        }

        return TrackAsync(logEvent);
    }

    public void AddBreadcrumb(string message, string category = BreadcrumbCategories.Custom,
        SeverityLevel level = SeverityLevel.Information, IDictionary<string, string>? data = null)
    {
        _breadcrumbs.Add(new Breadcrumb
        {
            Message = message,
            Category = category,
            Level = level,
            Data = data is null ? new() : new Dictionary<string, string>(data)
        });
    }

    public void SetUser(string? userId) => _userId = userId;

    public void SetTag(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        if (value is null)
            _tags.TryRemove(key, out _);
        else
            _tags[key] = value;
    }

    public async Task CaptureCrashAsync(ExceptionInfo exception)
    {
        var intelligenceEvent = BuildExceptionEvent(exception);
        Enrich(intelligenceEvent);

        // Persist only — no flush. The process is dying; the uploader picks this
        // up on the next launch. Both writes are fast local writes; the screenshot
        // bytes were already captured proactively (never on this dying thread).
        await _store.SaveAsync(intelligenceEvent);
        await AttachScreenshotAsync(intelligenceEvent);
    }

    /// <summary>
    /// If screen capture is enabled and a recent frame exists, persist it to the
    /// screenshot store keyed by the event id. The uploader ships it after the
    /// event is delivered.
    /// </summary>
    private async Task AttachScreenshotAsync(IntelligenceEvent intelligenceEvent)
    {
        if (!_options.EnableScreenCapture)
            return;

        try
        {
            var jpeg = _lastScreen.GetLastScreenshot();
            if (jpeg is { Length: > 0 })
                await _screenshots.SaveAsync(intelligenceEvent.Id, jpeg);
        }
        catch
        {
            // Screenshot is best-effort; never let it interfere with the event.
        }
    }

    private static IntelligenceEvent BuildExceptionEvent(ExceptionInfo exception)
    {
        return new IntelligenceEvent
        {
            EventType = EventType.Exception,
            Level = SeverityLevel.Error,
            Exception = exception
        };
    }

    /// <summary>
    /// Single source of truth for application, device, runtime and scope
    /// context. Every event passes through here, so no capture site needs to
    /// remember to fill it in.
    /// </summary>
    private void Enrich(IntelligenceEvent intelligenceEvent)
    {
        intelligenceEvent.ProjectId = _options.ProjectId;
        intelligenceEvent.ApplicationName = _options.ApplicationName;
        intelligenceEvent.ApplicationVersion = _options.ApplicationVersion;
        intelligenceEvent.Environment = _options.Environment;
        intelligenceEvent.Release = string.IsNullOrWhiteSpace(_options.Release)
            ? _options.ApplicationVersion
            : _options.Release;

        intelligenceEvent.Platform = _device.Platform;
        intelligenceEvent.DeviceName = _device.DeviceName;
        intelligenceEvent.DeviceModel = _device.Model;
        intelligenceEvent.Manufacturer = _device.Manufacturer;
        intelligenceEvent.OperatingSystem = _device.OperatingSystem;

        // Runtime snapshot + scope.
        intelligenceEvent.DeviceRuntime = SafeCaptureRuntime();
        intelligenceEvent.UserId ??= _userId;

        foreach (var kv in _tags)
            intelligenceEvent.Tags.TryAdd(kv.Key, kv.Value);

        // Attach the breadcrumb trail (only if the caller didn't supply one).
        if (intelligenceEvent.Breadcrumbs.Count == 0)
            intelligenceEvent.Breadcrumbs = _breadcrumbs.Snapshot().ToList();
    }

    private DeviceRuntime? SafeCaptureRuntime()
    {
        // Never let context capture take down the actual event.
        try
        {
            return _runtime.Capture();
        }
        catch
        {
            return null;
        }
    }
}
