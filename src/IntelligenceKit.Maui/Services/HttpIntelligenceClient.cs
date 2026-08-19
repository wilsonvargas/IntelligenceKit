using System.Net.Http.Json;
using IntelligenceKit.Core.Configuration;
using IntelligenceKit.Core.Models;
using IntelligenceKit.Core.Services;

namespace IntelligenceKit.Maui.Services;

public class HttpIntelligenceClient : IIntelligenceClient
{
    private readonly HttpClient _httpClient;
    private readonly IntelligenceOptions _options;

    public HttpIntelligenceClient(HttpClient httpClient, IntelligenceOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<SendResult> SendAsync(IntelligenceEvent intelligenceEvent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerUrl))
            return SendResult.Rejected; // misconfigured; don't retry forever

        var url = $"{_options.ServerUrl}/events";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(intelligenceEvent)
            };

            if (!string.IsNullOrWhiteSpace(_options.ProjectKey))
                request.Headers.Add("X-IntelligenceKit-Key", _options.ProjectKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            return HttpStatusClassifier.Classify((int)response.StatusCode);
        }
        catch (Exception)
        {
            // Network/DNS/timeout: keep the event and retry later.
            return SendResult.TransientFailure;
        }
    }

    public async Task<SendResult> SendScreenshotAsync(Guid eventId, byte[] jpeg, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerUrl))
            return SendResult.Rejected;

        var url = $"{_options.ServerUrl}/events/{eventId}/screenshot";

        try
        {
            using var content = new ByteArrayContent(jpeg);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            if (!string.IsNullOrWhiteSpace(_options.ProjectKey))
                request.Headers.Add("X-IntelligenceKit-Key", _options.ProjectKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return HttpStatusClassifier.Classify((int)response.StatusCode);
        }
        catch (Exception)
        {
            return SendResult.TransientFailure;
        }
    }
}
