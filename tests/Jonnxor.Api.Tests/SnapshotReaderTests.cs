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

    [Fact]
    public void ReadAll_ResolvesPlainScalarsToCoreSchemaTypes()
    {
        // gaming.en.yaml: `order: 8` (plain int), `rate: 2.6` (plain float),
        // `start: "2002-05-05T08:00:00"` (double-quoted — stays a string).
        var root = FixturePath.For("valid");

        var entries = SnapshotReader.ReadAll(root).ToList();
        var gaming = entries.Single(e => e.Collection == "countdowns" && e.Slug == "gaming");

        Assert.Equal(8L, gaming.Fields["order"]);
        Assert.Equal(2.6, gaming.Fields["rate"]);
        Assert.Equal("2002-05-05T08:00:00", gaming.Fields["start"]);
    }

    [Fact]
    public void ReadAll_TypedScalars_ResolveBoolAndNullAndKeepQuotedStrings()
    {
        using var temp = new TempDir();
        temp.WriteFile("countdowns", "typed.en.yaml",
            "slug: typed\nlocale: en\ngold: true\ndraft: false\nnothing: null\ntilde: ~\nquoted_bool: \"true\"\nquoted_num: '42'\nplain_num: 42\n");

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();
        var fields = entries.Single().Fields;

        Assert.Equal(true, fields["gold"]);
        Assert.Equal(false, fields["draft"]);
        Assert.Null(fields["nothing"]);
        Assert.Null(fields["tilde"]);
        Assert.Equal("true", fields["quoted_bool"]); // quoting is intent — stays a string
        Assert.Equal("42", fields["quoted_num"]);
        Assert.Equal(42L, fields["plain_num"]);
    }

    [Fact]
    public void ReadAll_TypedScalars_ApplyRecursivelyInsideNestedStructures()
    {
        // drekis-vault.en.yaml: `stops: [55, 160]` (plain ints in a sequence) and
        // `status.kind: gold` (plain string) under a nested mapping.
        var root = FixturePath.For("valid");

        var entries = SnapshotReader.ReadAll(root).ToList();
        var project = entries.Single(e => e.Collection == "projects" && e.Slug == "drekis-vault");

        var stops = Assert.IsType<List<object?>>(project.Fields["stops"]);
        Assert.Equal([55L, 160L], stops);

        var status = Assert.IsType<Dictionary<string, object?>>(project.Fields["status"]);
        Assert.Equal("gold", status["kind"]);
    }

    [Fact]
    public void ReadAll_MalformedYaml_YieldsEntryWithParseError_NotAnException()
    {
        using var temp = new TempDir();
        temp.WriteFile("countdowns", "broken.en.yaml", "slug: [unclosed\n");

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();

        var entry = Assert.Single(entries);
        Assert.NotNull(entry.ParseError);
        Assert.Empty(entry.Fields);
    }
}
