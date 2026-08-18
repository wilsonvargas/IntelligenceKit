using IntelligenceKit.Server.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace IntelligenceKit.Dashboard.Services;

/// <summary>
/// Maintains a single SignalR connection to the server's events hub and raises
/// <see cref="EventReceived"/> whenever a new event is ingested anywhere. Shared
/// (singleton) so navigating between pages doesn't churn connections.
/// </summary>
public sealed class LiveEventsClient : IAsyncDisposable
{
    private readonly string _hubUrl;
    private HubConnection? _connection;

    public LiveEventsClient(IConfiguration configuration)
    {
        var baseUrl = configuration["ApiBaseUrl"] ?? "http://localhost:7099";
        _hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/events";
    }

    /// <summary>Raised on the SignalR thread; handlers should marshal to the UI.</summary>
    public event Action<EventSummary>? EventReceived;

    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    /// <summary>Connects if not already connected. Safe to call repeatedly.</summary>
    public async Task EnsureStartedAsync()
    {
        if (_connection is not null)
            return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<EventSummary>("eventReceived", summary => EventReceived?.Invoke(summary));

        await _connection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
