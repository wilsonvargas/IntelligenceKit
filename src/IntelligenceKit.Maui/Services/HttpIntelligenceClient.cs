using IntelligenceKit.Core.Configuration;

namespace IntelligenceKit.Maui.Services;

/// <summary>
/// Kept for API compatibility with 1.0 — the implementation now lives in
/// <see cref="IntelligenceKit.Core.Services.HttpIntelligenceClient"/>, shared by every SDK.
/// </summary>
public class HttpIntelligenceClient : IntelligenceKit.Core.Services.HttpIntelligenceClient
{
    public HttpIntelligenceClient(HttpClient httpClient, IntelligenceOptions options)
        : base(httpClient, options)
    {
    }
}
