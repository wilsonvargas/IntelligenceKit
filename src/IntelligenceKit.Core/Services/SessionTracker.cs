using IntelligenceKit.Core.Configuration;
using IntelligenceKit.Core.Enums;
using IntelligenceKit.Core.Models;
using IntelligenceKit.Core.Providers;
using IntelligenceKit.Core.Storage;

namespace IntelligenceKit.Core.Services;

/// <summary>
/// Default <see cref="ISessionTracker"/>. Every state change is sent as an
/// <see cref="EventType.Session"/> event through the normal store-and-forward
/// queue, so a crashed session survives the crash and is delivered on the next
/// launch. Going to the background reports the session as <c>Exited</c> (so an
/// app killed while backgrounded still counts as a clean exit); resuming within
/// the timeout reopens it with a newer update.
/// </summary>
public sealed class SessionTracker : ISessionTracker
{
    private readonly IEventStore _store;
    private readonly IEventUploader _uploader;
    private readonly IntelligenceOptions _options;
    private readonly IDeviceContextProvider _device;
    private readonly IInstallationIdProvider _installation;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();

    private SessionInfo? _current;
    private DateTime? _foregroundSince;
    private DateTime? _pausedAt;
    private double _accumulatedSeconds;
    private volatile string? _userId;

    public SessionTracker(
        IEventStore store,
        IEventUploader uploader,
        IntelligenceOptions options,
        IDeviceContextProvider device,
        IInstallationIdProvider installation,
        TimeProvider? clock = null)
    {
        _store = store;
        _uploader = uploader;
        _options = options;
        _device = device;
        _installation = installation;
        _clock = clock ?? TimeProvider.System;
    }

    public SessionInfo? Current
    {
        get { lock (_gate) return _current?.Clone(); }
    }

    public Task StartAsync()
    {
        IntelligenceEvent update;
        lock (_gate)
        {
            if (_current is not null && _pausedAt is null)
                return Task.CompletedTask; // already running

            update = BeginNewSession();
        }

        return SendAsync(update, flush: true);
    }

    public Task PauseAsync()
    {
        IntelligenceEvent update;
        lock (_gate)
        {
            if (_current is null || _pausedAt is not null)
                return Task.CompletedTask;

            var now = Now;
            Accumulate(now);
            _pausedAt = now;
            _current.Status = SessionStatus.Exited;
            update = BuildUpdate();
        }

        return SendAsync(update, flush: true);
    }

    public Task ResumeAsync()
    {
        IntelligenceEvent update;
        lock (_gate)
        {
            if (_current is not null && _pausedAt is null)
                return Task.CompletedTask; // never paused

            var now = Now;
            if (_current is null || now - _pausedAt!.Value > _options.SessionTimeout)
            {
                // No session yet, or away too long: the old one already reported
                // Exited when it was paused, so simply begin a fresh session.
                update = BeginNewSession();
            }
            else
            {
                _pausedAt = null;
                _foregroundSince = now;
                _current.Status = SessionStatus.Ok;
                update = BuildUpdate();
            }
        }

        return SendAsync(update, flush: true);
    }

    public Task EndAsync()
    {
        IntelligenceEvent update;
        lock (_gate)
        {
            if (_current is null)
                return Task.CompletedTask;

            if (_pausedAt is null)
                Accumulate(Now);
            _current.Status = SessionStatus.Exited;
            update = BuildUpdate();
            _current = null;
            _pausedAt = null;
        }

        return SendAsync(update, flush: true);
    }

    public void RecordError()
    {
        lock (_gate)
        {
            if (_current is not null)
                _current.Errors++;
        }
    }

    public void SetUser(string? userId) => _userId = userId;

    public Task CaptureCrashAsync()
    {
        IntelligenceEvent update;
        lock (_gate)
        {
            if (_current is null)
                return Task.CompletedTask;

            if (_pausedAt is null)
                Accumulate(Now);
            _current.Status = SessionStatus.Crashed;
            update = BuildUpdate();
            _current = null;
            _pausedAt = null;
        }

        // Persist only: the process is dying (see IIntelligenceKit.CaptureCrashAsync).
        return SendAsync(update, flush: false);
    }

    /// <summary>Replaces the current session with a new one. Caller holds <see cref="_gate"/>.</summary>
    private IntelligenceEvent BeginNewSession()
    {
        var now = Now;
        _current = new SessionInfo
        {
            DistinctId = SafeInstallationId(),
            Started = now,
            Status = SessionStatus.Ok,
            Init = true
        };
        _foregroundSince = now;
        _pausedAt = null;
        _accumulatedSeconds = 0;

        var update = BuildUpdate();
        _current.Init = false;
        return update;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private void Accumulate(DateTime now)
    {
        if (_foregroundSince is { } since && now > since)
            _accumulatedSeconds += (now - since).TotalSeconds;
        _foregroundSince = null;
    }

    /// <summary>Snapshots the current session as an event. Caller holds <see cref="_gate"/>.</summary>
    private IntelligenceEvent BuildUpdate()
    {
        var session = _current!;
        session.Sequence++;
        var live = _foregroundSince is { } since ? Math.Max(0, (Now - since).TotalSeconds) : 0;
        session.DurationSeconds = Math.Round(_accumulatedSeconds + live, 3);

        return new IntelligenceEvent
        {
            EventType = EventType.Session,
            Session = session.Clone(),
            ProjectId = _options.ProjectId,
            ApplicationName = _options.ApplicationName,
            ApplicationVersion = _options.ApplicationVersion,
            Environment = _options.Environment,
            Release = string.IsNullOrWhiteSpace(_options.Release) ? _options.ApplicationVersion : _options.Release,
            Platform = Safe(() => _device.Platform),
            DeviceName = Safe(() => _device.DeviceName),
            DeviceModel = Safe(() => _device.Model),
            Manufacturer = Safe(() => _device.Manufacturer),
            OperatingSystem = Safe(() => _device.OperatingSystem),
            UserId = _userId,
            Timestamp = Now
        };
    }

    private async Task SendAsync(IntelligenceEvent update, bool flush)
    {
        // Session tracking is best-effort: it must never break the host app.
        try
        {
            await _store.SaveAsync(update).ConfigureAwait(false);
            if (flush)
                await _uploader.FlushAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private string SafeInstallationId()
    {
        try
        {
            return _installation.GetInstallationId();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Safe(Func<string> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return string.Empty;
        }
    }
}
