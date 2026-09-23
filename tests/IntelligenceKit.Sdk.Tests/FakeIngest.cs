using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntelligenceKit.Core.Models;

namespace IntelligenceKit.Sdk.Tests;

/// <summary>Stands in for the IntelligenceKit server: records every event the SDK posts.</summary>
public sealed class FakeIngest : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ConcurrentQueue<IntelligenceEvent> Events { get; } = new();

    public HttpStatusCode Status { get; set; } = HttpStatusCode.Created;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Status == HttpStatusCode.Created && request.RequestUri!.AbsolutePath == "/events" && request.Content is not null)
        {
            var e = JsonSerializer.Deserialize<IntelligenceEvent>(await request.Content.ReadAsStringAsync(cancellationToken), Json);
            if (e is not null)
                Events.Enqueue(e);
        }
        return new HttpResponseMessage(Status);
    }

    public async Task<IntelligenceEvent> WaitForAsync(Func<IntelligenceEvent, bool> predicate, string what)
    {
        for (var i = 0; i < 100; i++)
        {
            var hit = Events.FirstOrDefault(predicate);
            if (hit is not null)
                return hit;
            await Task.Delay(50);
        }
        throw new TimeoutException($"Never received: {what}. Got {Events.Count} event(s).");
    }
}

internal static class TempDir
{
    public static string Create()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ik-sdk-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
