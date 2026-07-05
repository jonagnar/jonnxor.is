using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jonnxor.Api.Directus;

/// <summary>
/// Read-only Directus REST client for `verify --live`. Deliberately carries no
/// create/update/delete members — the seam validator must never be able to mutate the
/// authoring database it is checking. Login via `/auth/login`, then plain GETs against
/// `/items/{collection}`.
/// </summary>
public sealed class DirectusClient : IDisposable
{
    private readonly HttpClient _http;
    private string? _accessToken;

    public DirectusClient(string baseUrl, TimeSpan? timeout = null)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };
    }

    /// <summary>
    /// Logs in with the admin email/password and stores the access token for subsequent
    /// requests. Throws <see cref="DirectusUnreachableException"/> with a clear message on
    /// any network failure, timeout, or non-success/unexpected-shape response — callers
    /// should not need to inspect raw HttpRequestException/TaskCanceledException details.
    /// </summary>
    public async Task LoginAsync(string email, string password, CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("auth/login", new { email, password }, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new DirectusUnreachableException(
                $"Could not reach Directus at {_http.BaseAddress}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new DirectusUnreachableException(
                $"Directus login timed out after {_http.Timeout.TotalSeconds}s against {_http.BaseAddress}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadBody(response, ct);
            throw new DirectusUnreachableException(
                $"Directus login failed ({(int)response.StatusCode} {response.ReasonPhrase}) at {_http.BaseAddress}: {body}");
        }

        JsonNode? root;
        try
        {
            root = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            throw new DirectusUnreachableException(
                $"Directus login response was not valid JSON: {ex.Message}", ex);
        }

        var token = root?["data"]?["access_token"]?.GetValue<string>();
        if (string.IsNullOrEmpty(token))
        {
            throw new DirectusUnreachableException(
                "Directus login response did not contain data.access_token");
        }

        _accessToken = token;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
    }

    /// <summary>
    /// GET `/items/{collection}?limit=-1&amp;fields=*,translations.*` — every item with every
    /// base field and every translation field, as raw <see cref="JsonElement"/> so the
    /// equivalence comparator can walk arbitrary collection shapes without this client
    /// knowing any collection-specific schema.
    /// </summary>
    public async Task<IReadOnlyList<JsonElement>> GetItemsAsync(string collection, CancellationToken ct = default)
    {
        var path = $"items/{Uri.EscapeDataString(collection)}?limit=-1&fields=*,translations.*";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(path, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new DirectusUnreachableException(
                $"Could not reach Directus at {_http.BaseAddress}{path}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new DirectusUnreachableException(
                $"Directus request timed out after {_http.Timeout.TotalSeconds}s: {path}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadBody(response, ct);
            throw new DirectusUnreachableException(
                $"Directus GET {path} failed ({(int)response.StatusCode} {response.ReasonPhrase}): {body}");
        }

        JsonDocument doc;
        try
        {
            doc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                  ?? throw new DirectusUnreachableException($"Directus GET {path} returned an empty body");
        }
        catch (JsonException ex)
        {
            throw new DirectusUnreachableException($"Directus GET {path} response was not valid JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                throw new DirectusUnreachableException($"Directus GET {path} response had no 'data' array");
            }

            // Clone each element: it must outlive the JsonDocument, which is disposed here.
            return data.EnumerateArray().Select(e => e.Clone()).ToList();
        }
    }

    private static async Task<string> SafeReadBody(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception)
        {
            return "<unreadable body>";
        }
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>Raised when Directus cannot be reached or responds unexpectedly. Carries a message
/// suitable for direct display — callers should not need to unwrap inner exceptions.</summary>
public sealed class DirectusUnreachableException : Exception
{
    public DirectusUnreachableException(string message) : base(message)
    {
    }

    public DirectusUnreachableException(string message, Exception inner) : base(message, inner)
    {
    }
}
