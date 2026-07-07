using System.Text.Json;
using Jonnxor.Api;

namespace Jonnxor.Admin.Services;

/// <summary>
/// Result of one Directus health probe. <see cref="Reachable"/> means an HTTP
/// response arrived at all — a 503 from an unhealthy Directus is still reachable;
/// <see cref="Status"/> (the response JSON's <c>status</c> property) and
/// <see cref="Detail"/> carry the bad news. <see cref="Url"/> is the resolved
/// Directus base URL, or empty when resolution itself failed.
/// </summary>
public sealed record DirectusHealth(bool Reachable, string? Status, string? Detail, string Url);

/// <summary>
/// Dashboard tile source: pings <c>{DIRECTUS_URL}/server/health</c>. The URL
/// resolves with the same precedence as the API CLI's <c>verify --live</c>
/// (<c>CliRunner</c>): an exported <c>DIRECTUS_URL</c> wins over the value in
/// <see cref="RepoPaths.DirectusEnvPath"/>. Read-only by construction — one GET,
/// never a mutation.
/// </summary>
public sealed class DirectusHealthService
{
    /// <summary>Worktree wording per the v1 plan — rendered verbatim when the URL cannot resolve.</summary>
    private const string WorktreeCaveat =
        "direnv does not reach worktrees — export DIRECTUS_URL or ensure directus/.env exists "
        + "(decrypted via sops/direnv from the main checkout)";

    private const int TimeoutSeconds = 5;

    private readonly RepoPaths _paths;
    private readonly HttpClient _http;
    private readonly Func<string, string?> _getEnv;

    /// <param name="paths">Resolved repo paths (for the Directus env file).</param>
    /// <param name="handler">Injectable transport for tests; null means a real handler.</param>
    /// <param name="getEnv">Injectable env lookup for tests; null means the process environment.</param>
    public DirectusHealthService(
        RepoPaths paths, HttpMessageHandler? handler = null, Func<string, string?>? getEnv = null)
    {
        _paths = paths;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
        _getEnv = getEnv ?? Environment.GetEnvironmentVariable;
    }

    public async Task<DirectusHealth> CheckAsync(CancellationToken ct = default)
    {
        var (url, problem) = ResolveUrl();
        if (url is null)
        {
            return new DirectusHealth(false, null, problem, string.Empty);
        }

        if (!Uri.TryCreate($"{url.TrimEnd('/')}/server/health", UriKind.Absolute, out var healthUri)
            || (healthUri.Scheme != Uri.UriSchemeHttp && healthUri.Scheme != Uri.UriSchemeHttps))
        {
            return new DirectusHealth(false, null, $"DIRECTUS_URL '{url}' is not a valid http(s) URL", url);
        }

        try
        {
            using var response = await _http.GetAsync(healthUri, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var status = TryReadStatus(body);
            return response.IsSuccessStatusCode
                ? new DirectusHealth(true, status, null, url)
                : new DirectusHealth(true, status, $"HTTP {(int)response.StatusCode} from {healthUri}", url);
        }
        catch (HttpRequestException ex)
        {
            return new DirectusHealth(false, null, $"unreachable: {ex.Message}", url);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // HttpClient's own 5s timeout, not caller cancellation (which rethrows).
            return new DirectusHealth(
                false, null, $"timed out after {TimeoutSeconds}s waiting for {healthUri}", url);
        }
    }

    /// <summary>Env var wins over the env file — the CliRunner precedence.</summary>
    private (string? Url, string? Problem) ResolveUrl()
    {
        if (_getEnv("DIRECTUS_URL") is { Length: > 0 } fromEnv)
        {
            return (fromEnv, null);
        }

        var envPath = _paths.DirectusEnvPath;
        if (!File.Exists(envPath))
        {
            return (null, $"DIRECTUS_URL is not exported and '{envPath}' does not exist; {WorktreeCaveat}");
        }

        IReadOnlyDictionary<string, string> fileValues;
        try
        {
            fileValues = EnvFile.Load(envPath);
        }
        catch (IOException ex)
        {
            return (null, $"could not read '{envPath}': {ex.Message}; {WorktreeCaveat}");
        }

        return fileValues.GetValueOrDefault("DIRECTUS_URL") is { Length: > 0 } fromFile
            ? (fromFile, null)
            : (null, $"DIRECTUS_URL is not exported and missing from '{envPath}'; {WorktreeCaveat}");
    }

    private static string? TryReadStatus(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("status", out var status)
                   && status.ValueKind == JsonValueKind.String
                ? status.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null; // lenient: an unparseable body just means "no status"
        }
    }
}
