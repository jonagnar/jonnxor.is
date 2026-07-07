using Jonnxor.Admin.Services;
using Microsoft.Extensions.Configuration;

namespace Jonnxor.Admin.Tests;

public class LoopbackGuardTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5170")]
    [InlineData("http://localhost:5170")]
    [InlineData("http://LOCALHOST:5170")]
    [InlineData("http://[::1]:5170")]
    public void EnsureLoopback_AcceptsLoopbackUrl(string url)
    {
        LoopbackGuard.EnsureLoopback(new[] { url });
    }

    [Theory]
    [InlineData("http://0.0.0.0:5170")]
    [InlineData("http://+:80")]
    [InlineData("http://*:80")]
    [InlineData("http://[::]:5170")]
    [InlineData("http://192.168.1.10:5170")]
    [InlineData("http://example.com:5170")]
    public void EnsureLoopback_RejectsNonLoopbackUrl(string url)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => LoopbackGuard.EnsureLoopback(new[] { url }));

        Assert.Contains(url, ex.Message);
    }

    [Fact]
    public void EnsureLoopback_AcceptsMultipleLoopbackUrls()
    {
        LoopbackGuard.EnsureLoopback(new[]
        {
            "http://127.0.0.1:5170",
            "http://localhost:5171",
        });
    }

    [Fact]
    public void EnsureLoopback_NamesTheOffendingUrlInAMixedList()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => LoopbackGuard.EnsureLoopback(new[]
            {
                "http://127.0.0.1:5170",
                "http://0.0.0.0:5171",
            }));

        Assert.Contains("http://0.0.0.0:5171", ex.Message);
        Assert.DoesNotContain("http://127.0.0.1:5170", ex.Message);
    }

    [Fact]
    public void EnsureLoopback_AcceptsEmptyUrlList()
    {
        LoopbackGuard.EnsureLoopback(Array.Empty<string>());
    }

    [Fact]
    public void CollectConfiguredUrls_ReadsAndSplitsTheUrlsKey()
    {
        var configuration = BuildConfiguration(
            ("urls", "http://127.0.0.1:5170;http://localhost:5171"));

        var urls = LoopbackGuard.CollectConfiguredUrls(configuration);

        Assert.Equal(new[] { "http://127.0.0.1:5170", "http://localhost:5171" }, urls);
    }

    [Fact]
    public void CollectConfiguredUrls_ReadsKestrelEndpointUrls()
    {
        var configuration = BuildConfiguration(
            ("Kestrel:Endpoints:Http:Url", "http://0.0.0.0:5173"),
            ("Kestrel:Endpoints:Other:Url", "http://127.0.0.1:5174"));

        var urls = LoopbackGuard.CollectConfiguredUrls(configuration);

        Assert.Contains("http://0.0.0.0:5173", urls);
        Assert.Contains("http://127.0.0.1:5174", urls);
    }

    [Fact]
    public void CollectConfiguredUrls_IsEmptyWhenNothingIsConfigured()
    {
        var configuration = BuildConfiguration();

        Assert.Empty(LoopbackGuard.CollectConfiguredUrls(configuration));
    }

    [Fact]
    public void EnsureLoopback_RejectsWildcardKestrelEndpointFromConfiguration()
    {
        var configuration = BuildConfiguration(
            ("urls", "http://127.0.0.1:5170"),
            ("Kestrel:Endpoints:Http:Url", "http://0.0.0.0:5173"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => LoopbackGuard.EnsureLoopback(LoopbackGuard.CollectConfiguredUrls(configuration)));

        Assert.Contains("http://0.0.0.0:5173", ex.Message);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] pairs)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => (string?)p.Value))
            .Build();
    }
}
