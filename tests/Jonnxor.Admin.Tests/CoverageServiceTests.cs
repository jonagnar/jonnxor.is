using Jonnxor.Admin.Services;

namespace Jonnxor.Admin.Tests;

/// <summary>
/// Coverage over the <c>mixed-locale</c> fixture:
/// <list type="bullet">
/// <item><c>blog</c>: alpha (is+en), beta (en only; its ja file is a deliberate
/// parse error and must not count as present) → 2 slugs, is 1/2, en 2/2, ja 0/2.</item>
/// <item><c>grimoire</c>: gamma (is+en+ja) → 1 slug, everything 1/1.</item>
/// </list>
/// </summary>
public class CoverageServiceTests
{
    private static CoverageService ServiceFor(string contentDir)
        => new(new RepoPaths(new AdminOptions
        {
            RepoRoot = Path.GetTempPath(),
            ContentDir = contentDir,
        }));

    private static CoverageService MixedLocaleService()
        => ServiceFor(FixturePath.For("mixed-locale"));

    [Fact]
    public async Task BuildAsync_MixedLocaleFixture_MatchesReportVerbNumbers()
    {
        var service = MixedLocaleService();

        var result = await service.BuildAsync();

        Assert.Null(result.Detail);
        Assert.Equal(["blog", "grimoire"], result.Report.Collections.Select(c => c.Name));

        var blog = result.Report.Collections[0];
        Assert.Equal(2, blog.Slugs);
        Assert.Equal(1, blog.Locales["is"].Present);
        Assert.Equal(0.5, blog.Locales["is"].Coverage);
        Assert.Equal(2, blog.Locales["en"].Present);
        Assert.Equal(1.0, blog.Locales["en"].Coverage);
        // beta.ja.md exists on disk but is a parse error — never "present".
        Assert.Equal(0, blog.Locales["ja"].Present);
        Assert.Equal(0.0, blog.Locales["ja"].Coverage);

        var grimoire = result.Report.Collections[1];
        Assert.Equal(1, grimoire.Slugs);
        Assert.All(["is", "en", "ja"], locale =>
        {
            Assert.Equal(1, grimoire.Locales[locale].Present);
            Assert.Equal(1.0, grimoire.Locales[locale].Coverage);
        });
    }

    [Fact]
    public async Task MissingBySlug_AfterBuild_ListsSlugsLackingThatLocale()
    {
        var service = MixedLocaleService();
        await service.BuildAsync();

        Assert.Equal(["alpha", "beta"], service.MissingBySlug("blog", "ja"));
        Assert.Equal(["beta"], service.MissingBySlug("blog", "is"));
        Assert.Empty(service.MissingBySlug("blog", "en"));
        Assert.Empty(service.MissingBySlug("grimoire", "ja"));
    }

    [Fact]
    public void MissingBySlug_BeforeAnyBuild_IsEmptyNotACrash()
    {
        // The drill-down cache is populated by BuildAsync (the page always builds
        // first); before that there is nothing to drill into.
        var service = MixedLocaleService();

        Assert.Empty(service.MissingBySlug("blog", "ja"));
    }

    [Fact]
    public async Task MissingBySlug_UnknownCollectionOrLocale_IsEmpty()
    {
        var service = MixedLocaleService();
        await service.BuildAsync();

        Assert.Empty(service.MissingBySlug("no-such-collection", "ja"));
        Assert.Empty(service.MissingBySlug("blog", "fr"));
    }

    [Fact]
    public async Task BuildAsync_MissingContentDir_DegradesToEmptyReportWithDetail()
    {
        using var temp = new TempDir();
        var service = ServiceFor(Path.Combine(temp.Path, "does-not-exist"));

        var result = await service.BuildAsync();

        Assert.NotNull(result.Detail);
        Assert.Contains("does-not-exist", result.Detail);
        Assert.Empty(result.Report.Collections);
        // The drill-down cache degrades in step: empty, never stale or throwing.
        Assert.Empty(service.MissingBySlug("blog", "ja"));
    }

    [Fact]
    public async Task BuildAsync_Rebuild_RefreshesTheDrillDownCache()
    {
        using var temp = new TempDir();
        var contentDir = temp.CreateDir("content");
        temp.WriteFile(Path.Combine("content", "blog", "solo.en.md"),
            "---\nslug: solo\nlocale: en\n---\nbody\n");
        var service = ServiceFor(contentDir);

        await service.BuildAsync();
        Assert.Equal(["solo"], service.MissingBySlug("blog", "ja"));

        temp.WriteFile(Path.Combine("content", "blog", "solo.ja.md"),
            "---\nslug: solo\nlocale: ja\n---\nbody\n");
        await service.BuildAsync();

        // MissingBySlug reflects the LAST BuildAsync, by design: the drill-down
        // always matches the table the page currently shows.
        Assert.Empty(service.MissingBySlug("blog", "ja"));
    }
}
