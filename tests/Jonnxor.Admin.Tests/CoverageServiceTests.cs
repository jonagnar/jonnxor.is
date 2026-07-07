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
    public async Task MissingBySlug_OnTheResult_ListsSlugsLackingThatLocale()
    {
        var result = await MixedLocaleService().BuildAsync();

        Assert.Equal(["alpha", "beta"], result.MissingBySlug("blog", "ja"));
        Assert.Equal(["beta"], result.MissingBySlug("blog", "is"));
        Assert.Empty(result.MissingBySlug("blog", "en"));
        Assert.Empty(result.MissingBySlug("grimoire", "ja"));
    }

    [Fact]
    public async Task MissingBySlug_UnknownCollectionOrLocale_IsEmpty()
    {
        var result = await MixedLocaleService().BuildAsync();

        Assert.Empty(result.MissingBySlug("no-such-collection", "ja"));
        Assert.Empty(result.MissingBySlug("blog", "fr"));
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
        // The drill-down degrades in step: empty, never stale or throwing.
        Assert.Empty(result.MissingBySlug("blog", "ja"));
    }

    [Fact]
    public async Task BuildAsync_ResultsAreSelfContained_RebuildNeverMutatesAnEarlierResult()
    {
        using var temp = new TempDir();
        var contentDir = temp.CreateDir("content");
        temp.WriteFile(Path.Combine("content", "blog", "solo.en.md"),
            "---\nslug: solo\nlocale: en\n---\nbody\n");
        var service = ServiceFor(contentDir);

        var first = await service.BuildAsync();
        Assert.Equal(["solo"], first.MissingBySlug("blog", "ja"));

        temp.WriteFile(Path.Combine("content", "blog", "solo.ja.md"),
            "---\nslug: solo\nlocale: ja\n---\nbody\n");
        var second = await service.BuildAsync();

        // Each result carries its own drill-down data: the rebuild sees the new
        // file, and the earlier result still answers for the table IT described —
        // tab B rebuilding can never change what tab A's cell clicks say.
        Assert.Empty(second.MissingBySlug("blog", "ja"));
        Assert.Equal(["solo"], first.MissingBySlug("blog", "ja"));
    }
}
