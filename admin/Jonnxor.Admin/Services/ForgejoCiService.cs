using System.Text.Json;

namespace Jonnxor.Admin.Services;

/// <summary>One CI workflow run, parsed leniently from Forgejo's runs API.</summary>
public sealed record CiRun(
    string Name,
    string HeadBranch,
    string DisplayTitle,
    string Status,
    long RunNumber,
    DateTimeOffset? CreatedAt);

/// <summary>
/// CI tile state. <see cref="Available"/> is false on any degraded outcome (no token,
/// HTTP error, unreachable Forgejo, malformed body) with <see cref="Detail"/> naming
/// the reason; runs are then empty. Degradation is data, never an exception.
/// </summary>
public sealed record CiStatus(bool Available, string? Detail, IReadOnlyList<CiRun> Runs);

/// <summary>
/// Dashboard tile source for the latest CI run per branch, from Forgejo's
/// <c>/api/v1/repos/{repo}/actions/tasks</c> endpoint. The token comes from the
/// <c>FORGEJO_TOKEN</c> environment variable ONLY (sops/direnv-managed, read:repository
/// scope) — never from config or appsettings, so no secret can end up in a committed
/// file. Read-only by construction: one GET, no mutation surface.
/// </summary>
public sealed class ForgejoCiService
{
    /// <summary>Degraded-state detail rendered by the CI tile when no token is set.</summary>
    public const string NoTokenDetail = "no FORGEJO_TOKEN configured";

    private const int RunLimit = 30;
    private const int TimeoutSeconds = 5;

    private readonly AdminOptions _options;
    private readonly HttpClient _http;
    private readonly Func<string, string?> _getEnv;

    /// <param name="options">Bound panel configuration (Forgejo base URL + repo slug).</param>
    /// <param name="handler">Injectable transport for tests; null means a real handler.</param>
    /// <param name="getEnv">Injectable env lookup for tests; null means the process environment.</param>
    public ForgejoCiService(
        AdminOptions options, HttpMessageHandler? handler = null, Func<string, string?>? getEnv = null)
    {
        _options = options;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        _getEnv = getEnv ?? Environment.GetEnvironmentVariable;
    }

    /// <summary>
    /// The newest run per <c>head_branch</c>, ordered main, preview, then the rest
    /// (newest first).
    /// </summary>
    public async Task<CiStatus> GetLatestPerBranchAsync(CancellationToken ct = default)
    {
        if (_getEnv("FORGEJO_TOKEN") is not { Length: > 0 } token)
        {
            return new CiStatus(false, NoTokenDetail, []);
        }

        var url = $"{_options.ForgejoBaseUrl.TrimEnd('/')}/api/v1/repos/{_options.ForgejoRepo}"
                  + $"/actions/tasks?limit={RunLimit}";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            // Malformed config degrades like every other failure mode — the class
            // contract is "degradation is data, never an exception".
            return new CiStatus(
                false, $"ForgejoBaseUrl '{_options.ForgejoBaseUrl}' is not a valid http(s) URL", []);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Authorization", $"token {token}");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return new CiStatus(false, $"Forgejo unreachable: {ex.Message}", []);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new CiStatus(
                false, $"Forgejo request timed out after {TimeoutSeconds}s ({url})", []);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new CiStatus(
                    false, $"HTTP {(int)response.StatusCode} from Forgejo runs API", []);
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseRuns(body);
        }
    }

    private static CiStatus ParseRuns(string body)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            // The whole body is malformed → degraded result, per plan.
            return new CiStatus(false, $"Forgejo response was not valid JSON: {ex.Message}", []);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("workflow_runs", out var runsElement)
                || runsElement.ValueKind != JsonValueKind.Array)
            {
                return new CiStatus(false, "Forgejo response has no workflow_runs array", []);
            }

            var runs = new List<CiRun>();
            foreach (var element in runsElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue; // a malformed entry is skipped, never fatal
                }

                runs.Add(new CiRun(
                    ReadString(element, "name"),
                    ReadString(element, "head_branch"),
                    ReadString(element, "display_title"),
                    ReadString(element, "status"),
                    ReadInt64(element, "run_number"),
                    ReadTimestamp(element, "created_at")));
            }

            var latestPerBranch = runs
                .GroupBy(r => r.HeadBranch, StringComparer.Ordinal)
                .Select(branch => branch
                    .OrderByDescending(r => r.CreatedAt ?? DateTimeOffset.MinValue)
                    .ThenByDescending(r => r.RunNumber)
                    .First())
                .OrderBy(BranchRank)
                .ThenByDescending(r => r.CreatedAt ?? DateTimeOffset.MinValue)
                .ToList();

            return new CiStatus(true, null, latestPerBranch);
        }
    }

    private static int BranchRank(CiRun run) => run.HeadBranch switch
    {
        "main" => 0,
        "preview" => 1,
        _ => 2,
    };

    private static string ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long ReadInt64(JsonElement element, string name)
        => element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.Number
           && value.TryGetInt64(out var number)
            ? number
            : 0;

    private static DateTimeOffset? ReadTimestamp(JsonElement element, string name)
        => element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
           && value.TryGetDateTimeOffset(out var stamp)
            ? stamp
            : null;
}
