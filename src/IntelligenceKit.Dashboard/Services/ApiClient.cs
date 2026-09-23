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

    public async Task<PagedResult<IssueSummary>> GetIssuesAsync(
        string? projectId = null, int skip = 0, int take = 50, string? status = null, CancellationToken ct = default)
    {
        var url = $"/issues?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(projectId))
            url += $"&projectId={Uri.EscapeDataString(projectId)}";
        if (!string.IsNullOrWhiteSpace(status))
            url += $"&status={Uri.EscapeDataString(status)}";

        return await http.GetFromJsonAsync<PagedResult<IssueSummary>>(url, JsonOptions, ct)
               ?? new PagedResult<IssueSummary>(0, skip, take, Array.Empty<IssueSummary>());
    }

    public async Task<IssueSummary?> GetIssueAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/issues/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IssueSummary>(JsonOptions, ct);
    }

    /// <summary>Triage an issue (status / assignee). Returns the updated issue.</summary>
    public async Task<IssueSummary?> UpdateIssueAsync(Guid id, UpdateIssueRequest request, CancellationToken ct = default)
    {
        var response = await http.PatchAsJsonAsync($"/issues/{id}", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IssueSummary>(JsonOptions, ct);
    }

    public async Task<PagedResult<EventSummary>> GetIssueEventsAsync(
        Guid id, int skip = 0, int take = 50, CancellationToken ct = default)
        => await http.GetFromJsonAsync<PagedResult<EventSummary>>(
               $"/issues/{id}/events?skip={skip}&take={take}", JsonOptions, ct)
           ?? new PagedResult<EventSummary>(0, skip, take, Array.Empty<EventSummary>());

    public async Task<CrashFreeStats?> GetCrashFreeAsync(
        string? projectId = null, string? environment = null, string? release = null, int days = 14, CancellationToken ct = default)
    {
        var url = $"/stats/crash-free?days={days}";
        if (!string.IsNullOrWhiteSpace(projectId))
            url += $"&projectId={Uri.EscapeDataString(projectId)}";
        if (!string.IsNullOrWhiteSpace(environment))
            url += $"&environment={Uri.EscapeDataString(environment)}";
        if (!string.IsNullOrWhiteSpace(release))
            url += $"&release={Uri.EscapeDataString(release)}";
        return await http.GetFromJsonAsync<CrashFreeStats>(url, JsonOptions, ct);
    }

    // Alerts (admin-only) ---------------------------------------------------

    public async Task<IReadOnlyList<AlertRuleInfo>> GetAlertRulesAsync(CancellationToken ct = default)
        => await http.GetFromJsonAsync<IReadOnlyList<AlertRuleInfo>>("/alerts/rules", JsonOptions, ct)
           ?? Array.Empty<AlertRuleInfo>();

    /// <summary>Creates (id null) or replaces a rule. Returns the server's error text on 400.</summary>
    public async Task<(AlertRuleInfo? Rule, string? Error)> SaveAlertRuleAsync(
        Guid? id, UpsertAlertRuleRequest request, CancellationToken ct = default)
    {
        var response = id is null
            ? await http.PostAsJsonAsync("/alerts/rules", request, JsonOptions, ct)
            : await http.PutAsJsonAsync($"/alerts/rules/{id}", request, JsonOptions, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            return (null, (await response.Content.ReadAsStringAsync(ct)).Trim('"'));

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AlertRuleInfo>(JsonOptions, ct), null);
    }

    public async Task DeleteAlertRuleAsync(Guid id, CancellationToken ct = default)
        => (await http.DeleteAsync($"/alerts/rules/{id}", ct)).EnsureSuccessStatusCode();

    /// <summary>Fires a synthetic alert through the rule. Returns null on success, else the error.</summary>
    public async Task<string?> TestAlertRuleAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/alerts/rules/{id}/test", null, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, ct);
        return result.GetProperty("success").GetBoolean() ? null : result.GetProperty("error").GetString();
    }

    public async Task<PagedResult<AlertNotificationInfo>> GetAlertHistoryAsync(
        string? projectId = null, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var url = $"/alerts/history?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(projectId))
            url += $"&projectId={Uri.EscapeDataString(projectId)}";
        return await http.GetFromJsonAsync<PagedResult<AlertNotificationInfo>>(url, JsonOptions, ct)
               ?? new PagedResult<AlertNotificationInfo>(0, skip, take, Array.Empty<AlertNotificationInfo>());
    }
}
