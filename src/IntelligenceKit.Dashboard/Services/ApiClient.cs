using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using IntelligenceKit.Server.Contracts;

namespace IntelligenceKit.Dashboard.Services;

/// <summary>
/// Thin typed wrapper over the IntelligenceKit server API. Every call returns
/// the shared contract types so the pages stay free of serialization concerns.
/// </summary>
public class ApiClient(HttpClient http)
{
    /// <summary>API root (ends with '/'), for building direct asset URLs like screenshots.</summary>
    public Uri? BaseAddress => http.BaseAddress;

    // The server emits enums (e.g. breadcrumb SeverityLevel) as strings, so the
    // client must accept strings too — the web defaults don't do this on their own.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<IReadOnlyList<ProjectSummary>>("/projects", JsonOptions, ct)
           ?? Array.Empty<ProjectSummary>();

    public async Task<PagedResult<EventSummary>> GetEventsAsync(
        string? projectId = null,
        string? eventType = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        var query = new List<string> { $"skip={skip}", $"take={take}" };
        if (!string.IsNullOrWhiteSpace(projectId))
            query.Add($"projectId={Uri.EscapeDataString(projectId)}");
        if (!string.IsNullOrWhiteSpace(eventType))
            query.Add($"eventType={Uri.EscapeDataString(eventType)}");

        var url = $"/events?{string.Join('&', query)}";
        return await http.GetFromJsonAsync<PagedResult<EventSummary>>(url, JsonOptions, ct)
               ?? new PagedResult<EventSummary>(0, skip, take, Array.Empty<EventSummary>());
    }

    public async Task<IReadOnlyList<TimeBucket>> GetEventsPerHourAsync(
        string? projectId = null, int hours = 24, CancellationToken ct = default)
    {
        var url = $"/stats/events-per-hour?hours={hours}";
        if (!string.IsNullOrWhiteSpace(projectId))
            url += $"&projectId={Uri.EscapeDataString(projectId)}";

        return await http.GetFromJsonAsync<IReadOnlyList<TimeBucket>>(url, JsonOptions, ct)
               ?? Array.Empty<TimeBucket>();
    }

    public async Task<EventDetail?> GetEventAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/events/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EventDetail>(JsonOptions, ct);
    }
}
