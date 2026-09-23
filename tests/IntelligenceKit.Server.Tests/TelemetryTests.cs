using System.Net;
using System.Net.Http.Json;

namespace IntelligenceKit.Server.Tests;

public class TelemetryTests : IClassFixture<ServerAppFactory>
{
    private readonly ServerAppFactory _factory;

    public TelemetryTests(ServerAppFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthProbes_AreHealthy_WithoutAuth(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Metrics_RequireAdmin_AndExposeIngestCounters()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/metrics")).StatusCode);

        await _factory.CreateClient().PostAsJsonAsync("/events", TestEvents.Exception(projectId: "metrics-project"));

        // The Prometheus exporter caches a scrape briefly; retry until the counter shows.
        var authed = _factory.CreateAuthorizedClient();
        string body = "";
        for (var i = 0; i < 20 && !body.Contains("metrics-project"); i++)
        {
            body = await authed.GetStringAsync("/metrics");
            if (!body.Contains("metrics-project"))
                await Task.Delay(250);
        }

        Assert.Contains("ik_events_ingested", body);
        Assert.Contains("project=\"metrics-project\"", body);
        Assert.Contains("ik_ingest_duration", body);
    }
}
