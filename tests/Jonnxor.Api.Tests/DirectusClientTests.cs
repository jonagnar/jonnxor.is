using Jonnxor.Api.Directus;

namespace Jonnxor.Api.Tests;

public class DirectusClientTests
{
    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://localhost:8055")]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_RejectsMalformedBaseUrl_ThrowsDirectusUnreachableException(string baseUrl)
    {
        // Uri.TryCreate validation happens in the ctor itself — no HTTP attempted, so a
        // malformed DIRECTUS_URL fails fast with the same clean-error type/exit-2 path the
        // rest of --live's failure modes use, rather than throwing a raw UriFormatException.
        var ex = Assert.Throws<DirectusUnreachableException>(() => new DirectusClient(baseUrl));

        Assert.Contains("DIRECTUS_URL", ex.Message);
    }

    [Fact]
    public void Ctor_AcceptsWellFormedHttpUrl()
    {
        using var client = new DirectusClient("http://localhost:8055");
    }

    [Fact]
    public void Ctor_AcceptsWellFormedHttpsUrl()
    {
        using var client = new DirectusClient("https://directus.example.com");
    }
}
