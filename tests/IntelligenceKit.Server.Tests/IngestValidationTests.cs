using System.Net;
using System.Net.Http.Json;

namespace IntelligenceKit.Server.Tests;

public class IngestValidationTests
{
    private static async Task<HttpResponseMessage> PostEvent(
        HttpClient client, string projectId, string? projectKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/events")
        {
            Content = JsonContent.Create(TestEvents.Exception(projectId: projectId)),
        };
        if (projectKey is not null)
            req.Headers.Add("X-IntelligenceKit-Key", projectKey);
        return await client.SendAsync(req);
    }

    [Fact]
    public async Task Ingest_UnknownProject_Is404()
    {
        using var factory = ServerAppFactory.CreateWithIngestValidation();
        var res = await PostEvent(factory.CreateClient(), "ghost", projectKey: "ikp_whatever");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Ingest_RegisteredProject_WithMatchingKey_Is201()
    {
        using var factory = ServerAppFactory.CreateWithIngestValidation();
        var created = await ProjectApi.CreateAsync(factory.CreateAuthorizedClient(), "registered");

        var res = await PostEvent(factory.CreateClient(), "registered", created.ProjectKey);

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Ingest_RegisteredProject_WithWrongKey_Is404()
    {
        using var factory = ServerAppFactory.CreateWithIngestValidation();
        await ProjectApi.CreateAsync(factory.CreateAuthorizedClient(), "strict");

        // Right projectId, wrong projectKey → the pair doesn't match.
        var res = await PostEvent(factory.CreateClient(), "strict", projectKey: "ikp_wrong");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
