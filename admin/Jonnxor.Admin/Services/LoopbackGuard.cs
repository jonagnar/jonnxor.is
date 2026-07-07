using System.Net;

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
