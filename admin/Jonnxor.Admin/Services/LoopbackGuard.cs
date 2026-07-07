using System.Net;
using Microsoft.Extensions.Configuration;

namespace Jonnxor.Admin.Services;

/// <summary>
/// Startup guard enforcing the admin panel's loopback-only posture: the panel has no
/// auth in v1, so binding anywhere reachable from the network is a hard startup
/// failure, never a warning.
/// </summary>
public static class LoopbackGuard
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> naming the offending URL unless
    /// every URL's host is a loopback address (<c>127.0.0.1</c>, <c>[::1]</c>, or
    /// <c>localhost</c>). Wildcard binds (<c>0.0.0.0</c>, <c>+</c>, <c>*</c>,
    /// <c>[::]</c>) and anything unparseable are refused.
    /// </summary>
    public static void EnsureLoopback(IEnumerable<string> urls)
    {
        foreach (var url in urls)
        {
            if (!IsLoopback(url))
            {
                throw new InvalidOperationException(
                    $"Jonnxor.Admin is loopback-only: refusing to bind to '{url}'. "
                    + "Bind to 127.0.0.1, [::1], or localhost instead.");
            }
        }
    }

    /// <summary>
    /// Collects every bind URL Kestrel can pick up from configuration: the
    /// <c>urls</c> key (appsettings <c>Urls</c>, <c>ASPNETCORE_URLS</c>, a
    /// <c>--urls</c> override — all flow into it) plus every
    /// <c>Kestrel:Endpoints:*:Url</c> value, which Kestrel binds directly and
    /// would otherwise bypass the guard entirely.
    /// </summary>
    public static IReadOnlyList<string> CollectConfiguredUrls(IConfiguration configuration)
    {
        var urls = new List<string>();

        var serverUrls = configuration["urls"];
        if (serverUrls is not null)
        {
            urls.AddRange(serverUrls.Split(
                ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        foreach (var endpoint in configuration.GetSection("Kestrel:Endpoints").GetChildren())
        {
            if (endpoint["Url"] is { } endpointUrl)
            {
                urls.Add(endpointUrl);
            }
        }

        return urls;
    }

    private static bool IsLoopback(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // Wildcard hosts (`+`, `*`) do not parse as URIs; anything we cannot
            // prove to be loopback is refused.
            return false;
        }

        if (uri.HostNameType == UriHostNameType.Dns)
        {
            return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }
}
