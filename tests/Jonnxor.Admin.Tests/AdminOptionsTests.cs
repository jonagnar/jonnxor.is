using Jonnxor.Admin.Services;
using Microsoft.Extensions.Configuration;

namespace Jonnxor.Admin.Tests;

public class AdminOptionsTests
{
    [Fact]
    public void Bind_EmptySectionYieldsDefaults()
    {
        var options = Bind();

        Assert.Null(options.RepoRoot);
        Assert.Equal("directus/.env", options.DirectusEnvPath);
        Assert.Equal("http://localhost:3000", options.ForgejoBaseUrl);
        Assert.Equal("WAAAGH/jonnxor.is", options.ForgejoRepo);
        Assert.Equal("client/src/content", options.ContentDir);
    }

    [Fact]
    public void Bind_ConfiguredValuesOverrideDefaults()
    {
        var options = Bind(
            ("Admin:RepoRoot", "/srv/jonnxor.is"),
            ("Admin:DirectusEnvPath", "custom/.env"),
            ("Admin:ForgejoBaseUrl", "http://forgejo.local:3300"),
            ("Admin:ForgejoRepo", "someone/else"),
            ("Admin:ContentDir", "client/src/other"));

        Assert.Equal("/srv/jonnxor.is", options.RepoRoot);
        Assert.Equal("custom/.env", options.DirectusEnvPath);
        Assert.Equal("http://forgejo.local:3300", options.ForgejoBaseUrl);
        Assert.Equal("someone/else", options.ForgejoRepo);
        Assert.Equal("client/src/other", options.ContentDir);
    }

    private static AdminOptions Bind(params (string Key, string Value)[] pairs)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => (string?)p.Value))
            .Build();

        var options = new AdminOptions();
        configuration.GetSection(AdminOptions.SectionName).Bind(options);
        return options;
    }
}
