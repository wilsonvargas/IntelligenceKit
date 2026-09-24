using IntelligenceKit.Core.Enums;
using IntelligenceKit.Core.Models;
using IntelligenceKit.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntelligenceKit.Extensions.Logging;

/// <summary>Controls what the IntelligenceKit logger records.</summary>
public sealed class IntelligenceKitLoggerOptions
{
    /// <summary>Entries at or above this level become breadcrumbs (default Information).</summary>
    public LogLevel MinimumBreadcrumbLevel { get; set; } = LogLevel.Information;

    /// <summary>Entries at or above this level are sent as events (default Error).</summary>
    public LogLevel MinimumEventLevel { get; set; } = LogLevel.Error;

    /// <summary>
    /// Category prefixes that are never recorded. Defaults exclude IntelligenceKit
    /// itself and the SDK's own HTTP client, so reporting can't feed back into itself.
    /// </summary>
    public List<string> ExcludedCategoryPrefixes { get; set; } =
    [
        "IntelligenceKit.Core.",
        "IntelligenceKit.Extensions.",
        "IntelligenceKit.Hosting.",
        "IntelligenceKit.Maui.",
        "System.Net.Http.HttpClient.IIntelligenceClient",
    ];
}

/// <summary>
/// <see cref="ILoggerProvider"/> that feeds <c>Microsoft.Extensions.Logging</c> into
/// IntelligenceKit: entries become breadcrumbs, and errors become events (as an
/// exception event when one is attached, otherwise a log event grouped by its
/// message template — so "Order {Id} failed" is one issue, not one per id).
/// <see cref="IIntelligenceKit"/> is resolved lazily from the container, because
/// loggers are created before most services exist.
/// </summary>
[ProviderAlias("IntelligenceKit")]
public sealed class IntelligenceKitLoggerProvider : ILoggerProvider
{
    private readonly IServiceProvider? _services;
    private IIntelligenceKit? _kit;

    public IntelligenceKitLoggerProvider(IServiceProvider services, IntelligenceKitLoggerOptions options)
    {
        _services = services;
        Options = options;
    }

    /// <summary>For hosts without DI: wire a kit directly.</summary>
    public IntelligenceKitLoggerProvider(IIntelligenceKit kit, IntelligenceKitLoggerOptions? options = null)
    {
        _kit = kit;
        Options = options ?? new IntelligenceKitLoggerOptions();
    }

    public IntelligenceKitLoggerOptions Options { get; }

    internal IIntelligenceKit? Kit => _kit ??= _services?.GetService<IIntelligenceKit>();

    public ILogger CreateLogger(string categoryName) => new IntelligenceKitLogger(this, categoryName);

    public void Dispose()
    {
    }
}

internal sealed class IntelligenceKitLogger : ILogger
{
    private const string OriginalFormatKey = "{OriginalFormat}";

    // Guards against feedback loops: anything logged while we are recording a log
    // entry (e.g. by the HTTP stack sending it) is ignored on this thread.
    [ThreadStatic]
    private static bool _recording;

    private readonly IntelligenceKitLoggerProvider _provider;
    private readonly string _category;
    private readonly bool _excluded;

    public IntelligenceKitLogger(IntelligenceKitLoggerProvider provider, string category)
    {
        _provider = provider;
        _category = category;
        _excluded = provider.Options.ExcludedCategoryPrefixes.Any(p => category.StartsWith(p, StringComparison.Ordinal));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
        => !_excluded && logLevel != LogLevel.None &&
           logLevel >= (LogLevel)Math.Min((int)_provider.Options.MinimumBreadcrumbLevel, (int)_provider.Options.MinimumEventLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (_recording || !IsEnabled(logLevel) || _provider.Kit is not { } kit)
            return;

        _recording = true;
        try
        {
            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception is not null)
                message = exception.Message;

            var level = logLevel.ToSeverity();
            var (template, properties) = Structure(state);

            if (logLevel >= _provider.Options.MinimumBreadcrumbLevel)
            {
                var data = new Dictionary<string, string> { ["category"] = _category };
                if (eventId.Id != 0)
                    data["event_id"] = eventId.Id.ToString();
                kit.AddBreadcrumb(message, BreadcrumbCategories.Log, level, data);
            }

            // An exception another integration already reported (e.g. request
            // middleware) is not reported again when it gets logged.
            if (logLevel >= _provider.Options.MinimumEventLevel &&
                (exception is null || !ExceptionCapture.IsCaptured(exception)))
            {
                if (exception is not null)
                    ExceptionCapture.MarkCaptured(exception);
                _ = SafeTrackAsync(kit, BuildEvent(level, message, template, properties, exception, eventId));
            }
        }
        catch
        {
            // A logger must never throw into the caller.
        }
        finally
        {
            _recording = false;
        }
    }

    private IntelligenceEvent BuildEvent(SeverityLevel level, string message, string? template,
        IReadOnlyList<KeyValuePair<string, object?>> properties, Exception? exception, EventId eventId)
    {
        var e = new IntelligenceEvent
        {
            EventType = exception is null ? EventType.Log : EventType.Exception,
            Level = level,
            Message = message,
            Exception = exception is null ? null : ExceptionInfo.FromException(exception),
            Tags = { ["logger"] = _category }
        };

        foreach (var (key, value) in properties)
            e.Data[key] = value?.ToString();
        if (template is not null)
            e.Data["message_template"] = template;
        if (eventId.Id != 0)
            e.Data["event_id"] = eventId.Id;

        // Group plain log events by (category, template) rather than the formatted text.
        if (exception is null && template is not null)
            e.Fingerprint = ["log", _category, template];

        return e;
    }

    private static (string? Template, IReadOnlyList<KeyValuePair<string, object?>> Properties) Structure<TState>(TState state)
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object?>> pairs)
            return (null, []);

        string? template = null;
        var properties = new List<KeyValuePair<string, object?>>(pairs.Count);
        foreach (var pair in pairs)
        {
            if (pair.Key == OriginalFormatKey)
                template = pair.Value as string;
            else
                properties.Add(pair);
        }
        return (template, properties);
    }

    private static async Task SafeTrackAsync(IIntelligenceKit kit, IntelligenceEvent e)
    {
        try
        {
            await kit.TrackAsync(e).ConfigureAwait(false);
        }
        catch
        {
        }
    }
}

internal static class LogLevelMapping
{
    /// <summary>SeverityLevel mirrors LogLevel's names and order.</summary>
    public static SeverityLevel ToSeverity(this LogLevel level) => level switch
    {
        LogLevel.Trace => SeverityLevel.Trace,
        LogLevel.Debug => SeverityLevel.Debug,
        LogLevel.Information => SeverityLevel.Information,
        LogLevel.Warning => SeverityLevel.Warning,
        LogLevel.Error => SeverityLevel.Error,
        _ => SeverityLevel.Critical
    };
}
