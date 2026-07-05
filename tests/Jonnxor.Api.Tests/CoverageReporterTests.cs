using Jonnxor.Api.Report;
using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Tests;

public class CoverageReporterTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);

    private static SnapshotEntry Entry(string collection, string slug, string locale, string? parseError = null) =>
        new()
        {
            Collection = collection,
            Slug = slug,
            Locale = locale,
            FilePath = $"{collection}/{slug}.{locale}.yaml",
            RawFrontmatter = string.Empty,
            Fields = new Dictionary<string, object?>(),
            ParseError = parseError,
        };

    [Fact]
    public void Build_MixedLocaleFixture_ComputesPerLocaleCoverage()
    {
        // One slug has en+is, one slug is en-only -> is coverage should read 50% (1 of 2).
        using var temp = new TempDir();
        temp.WriteFile("games", "astro-bot.en.yaml", "slug: astro-bot\n");
        temp.WriteFile("games", "astro-bot.is.yaml", "slug: astro-bot\n");
        temp.WriteFile("games", "baldurs-gate-3.en.yaml", "slug: baldurs-gate-3\n");

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();

        var report = CoverageReporter.Build(entries, FixedNow);

        var games = Assert.Single(report.Collections);
        Assert.Equal("games", games.Name);
        Assert.Equal(2, games.Slugs);

        Assert.Equal(2, games.Locales["en"].Present);
        Assert.Equal(1.0, games.Locales["en"].Coverage);

        Assert.Equal(1, games.Locales["is"].Present);
        Assert.Equal(0.5, games.Locales["is"].Coverage);

        Assert.Equal(0, games.Locales["ja"].Present);
        Assert.Equal(0.0, games.Locales["ja"].Coverage);
    }

    [Fact]
    public void Build_LocaleColumnsMatchSnapshotReaderLocalesExactly()
    {
        // Reuse convention: the report's per-collection locale keys must be exactly
        // SnapshotReader.Locales — not re-derived, not a subset/superset.
        var entries = new List<SnapshotEntry> { Entry("games", "astro-bot", "en") };

        var report = CoverageReporter.Build(entries, FixedNow);

        var games = Assert.Single(report.Collections);
        Assert.Equal(SnapshotReader.Locales.OrderBy(l => l), games.Locales.Keys.OrderBy(l => l));
    }

    [Fact]
    public void Build_MultipleCollections_OrderedAlphabeticallyByName()
    {
        var entries = new List<SnapshotEntry>
        {
            Entry("wallpapers", "arcade-dusk", "en"),
            Entry("blog", "some-post", "en"),
            Entry("games", "astro-bot", "en"),
        };

        var report = CoverageReporter.Build(entries, FixedNow);

        Assert.Equal(["blog", "games", "wallpapers"], report.Collections.Select(c => c.Name));
    }

    [Fact]
    public void Build_ParseErrorEntries_ExcludedFromSlugAndCoverageCounts()
    {
        // Matches the offline verifier's convention: a file that failed to parse is not
        // counted as a real slug/locale in either direction.
        var entries = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en"),
            Entry("games", "broken", "en", parseError: "boom"),
        };

        var report = CoverageReporter.Build(entries, FixedNow);

        var games = Assert.Single(report.Collections);
        Assert.Equal(1, games.Slugs);
        Assert.Equal(1, games.Locales["en"].Present);
        Assert.Equal(1.0, games.Locales["en"].Coverage);
    }

    [Fact]
    public void Build_RealSnapshotFixture_AllEnglishOnlyReportsEnFullCoverageOthersZero()
    {
        var root = FixturePath.For("valid");
        var entries = SnapshotReader.ReadAll(root).ToList();

        var report = CoverageReporter.Build(entries, FixedNow);

        Assert.Equal(7, report.Collections.Count);
        foreach (var collection in report.Collections)
        {
            Assert.Equal(1.0, collection.Locales["en"].Coverage);
            Assert.Equal(0.0, collection.Locales["is"].Coverage);
            Assert.Equal(0.0, collection.Locales["ja"].Coverage);
        }
    }

    [Fact]
    public void Build_StampsGeneratedAtFromCaller()
    {
        var entries = new List<SnapshotEntry> { Entry("games", "astro-bot", "en") };

        var report = CoverageReporter.Build(entries, FixedNow);

        Assert.Equal(FixedNow, report.GeneratedAt);
    }
}
