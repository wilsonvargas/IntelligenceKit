using Microsoft.AspNetCore.SignalR;

namespace IntelligenceKit.Server;

/// <summary>
/// Server-to-client push channel for live dashboards. The server broadcasts an
/// "eventReceived" message with an <c>EventSummary</c> payload each time an
/// event is ingested; clients don't call anything on the hub.
/// </summary>
public class EventsHub : Hub
{
}
