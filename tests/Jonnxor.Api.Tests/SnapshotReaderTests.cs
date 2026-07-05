using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Tests;

public class SnapshotReaderTests
{
    [Fact]
    public void ReadAll_EnumeratesEveryCollectionDirectory()
    {
        var root = FixturePath.For("valid");

        var entries = SnapshotReader.ReadAll(root).ToList();

        // Collections present in the valid fixture: blog, games, countdowns, pages,
        // wallpapers, projects, grimoire — each with 2 files (all .en for now).
        Assert.Equal(14, entries.Count);
        Assert.Contains(entries, e => e.Collection == "blog");
        Assert.Contains(entries, e => e.Collection == "games");
        Assert.Contains(entries, e => e.Collection == "countdowns");
        Assert.Contains(entries, e => e.Collection == "pages");
        Assert.Contains(entries, e => e.Collection == "wallpapers");
        Assert.Contains(entries, e => e.Collection == "projects");
        Assert.Contains(entries, e => e.Collection == "grimoire");
    }

    [Fact]
    public void ReadAll_ParsesSlugAndLocaleFromFilename()
    {
        var root = FixturePath.For("valid");

        var entries = SnapshotReader.ReadAll(root).ToList();
        var fable = entries.Single(e => e.Collection == "countdowns" && e.Slug == "fable-release");

        Assert.Equal("en", fable.Locale);
        Assert.EndsWith("fable-release.en.yaml", fable.FilePath);
    }

    [Fact]
    public void ReadAll_ParsesYamlFrontmatterIntoFieldMap()
    {
        var root = FixturePath.For("valid");

        var entries = SnapshotReader.ReadAll(root).ToList();
        var fable = entries.Single(e => e.Collection == "countdowns" && e.Slug == "fable-release");

        Assert.Equal("countdown", fable.Fields["kind"]);
        Assert.Equal("fable-release", fable.Fields["slug"]);
    }

    [Fact]
    public void ReadAll_BlogMarkdown_ExtractsFrontmatterBetweenFences()
    {
        var root = FixturePath.For("valid");

        var entries = SnapshotReader.ReadAll(root).ToList();
        var post = entries.Single(e => e.Collection == "blog" && e.Slug == "a-masala-chai-recipe-for-debugging-sessions");

        Assert.Equal("en", post.Locale);
        Assert.True(post.Fields.ContainsKey("title"));
        Assert.False(post.Fields.ContainsKey("body"));
        // Raw text exposed for the comment scan covers only the frontmatter fence body,
        // not the markdown prose below it.
        Assert.DoesNotContain("Placeholder post", post.RawFrontmatter);
    }

    [Fact]
    public void ReadAll_ExposesRawTextForCommentScanning()
    {
        var root = FixturePath.For("comment-node");

        var entries = SnapshotReader.ReadAll(root).ToList();
        var entry = entries.Single();

        Assert.Contains("#", entry.RawFrontmatter);
    }

    [Fact]
    public void ReadAll_GroupsBySlugAcrossLocales()
    {
        var root = FixturePath.For("missing-en");

        var entries = SnapshotReader.ReadAll(root).ToList();

        Assert.Single(entries);
        Assert.Equal("is", entries[0].Locale);
        Assert.Equal("summer-solstice", entries[0].Slug);
    }
}
