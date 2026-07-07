using System.Text.Json;

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
        // Shared resolver (also feeds the Config page's effective-config view), so
        // the tile and /config can never disagree about the URL or which source won.
        var resolution = DirectusUrlResolver.Resolve(_paths.DirectusEnvPath, _getEnv);
        if (resolution.Url is not { } url)
        {
            return new DirectusHealth(false, null, resolution.Problem, string.Empty);
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
