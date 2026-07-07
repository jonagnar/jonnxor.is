using Jonnxor.Api.Snapshot;
using Jonnxor.Api.Verification;

namespace Jonnxor.Api.Tests;

public class VerifierTests
{
    [Fact]
    public void Valid_ProducesZeroFindings()
    {
        var root = FixturePath.For("valid");
        var entries = SnapshotReader.ReadAll(root).ToList();

        var findings = Verifier.Run(entries);

        Assert.Empty(findings);
    }

    [Fact]
    public void MissingEn_ProducesEnBasePresentFinding()
    {
        var root = FixturePath.For("missing-en");
        var entries = SnapshotReader.ReadAll(root).ToList();

        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(EnBasePresentRule), finding.Rule);
        Assert.Contains("summer-solstice.is.yaml", finding.File);
        Assert.Contains("countdowns", finding.Detail);
        Assert.Contains("summer-solstice", finding.Detail);
        Assert.Contains("en", finding.Detail);
    }

    [Fact]
    public void CommentNode_ProducesNoCommentNodesFinding()
    {
        var root = FixturePath.For("comment-node");
        var entries = SnapshotReader.ReadAll(root).ToList();

        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(NoCommentNodesRule), finding.Rule);
        Assert.Contains("jol-christmas-eve.en.yaml", finding.File);
    }

    [Fact]
    public void KindViolation_ProducesCountdownKindCoherenceFinding()
    {
        var root = FixturePath.For("kind-violation");
        var entries = SnapshotReader.ReadAll(root).ToList();

        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(CountdownKindCoherenceRule), finding.Rule);
        Assert.Contains("gta-vi-release.en.yaml", finding.File);
        Assert.Contains("rate", finding.Detail);
    }

    [Fact]
    public void GoldOnCountup_ProducesCountdownKindCoherenceFinding()
    {
        // Proves the typed-scalar fix: `gold: true` must arrive as a bool, not the string
        // "true", or this check silently never fires (the review-caught false negative).
        using var temp = new TempDir();
        temp.WriteFile("countdowns", "studying.en.yaml", """
            slug: studying
            locale: en
            order: 9
            kind: countup
            gold: true
            icon: X
            start: "2010-09-01T09:00:00"
            rate: 1.2
            what: Studying
            note: Never stops.
            """);

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();
        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(CountdownKindCoherenceRule), finding.Rule);
        Assert.Contains("studying.en.yaml", finding.File);
        Assert.Contains("gold", finding.Detail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kind: sideways\n")]
    public void MissingOrUnknownKind_ProducesCountdownKindCoherenceFinding(string kindLine)
    {
        using var temp = new TempDir();
        temp.WriteFile("countdowns", "mystery.en.yaml",
            $"slug: mystery\nlocale: en\norder: 1\n{kindLine}what: Mystery\nnote: No kind to speak of.\n");

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();
        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(CountdownKindCoherenceRule), finding.Rule);
        Assert.Contains("mystery.en.yaml", finding.File);
        Assert.Contains("kind", finding.Detail);
    }

    [Fact]
    public void MalformedYaml_ProducesParseErrorFinding_InsteadOfCrashing()
    {
        using var temp = new TempDir();
        temp.WriteFile("countdowns", "broken.en.yaml", "slug: [unclosed\n");

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();
        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(ParseErrorRule), finding.Rule);
        Assert.Contains("broken.en.yaml", finding.File);
    }

    [Fact]
    public void MarkdownWithoutFrontmatterFences_ProducesParseErrorFinding()
    {
        using var temp = new TempDir();
        temp.WriteFile("blog", "fenceless.en.md", "Just prose, no frontmatter fences at all.\n");

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();
        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(ParseErrorRule), finding.Rule);
        Assert.Contains("fenceless.en.md", finding.File);
        Assert.Contains("fence", finding.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkdownWithUnterminatedFence_ProducesParseErrorFinding()
    {
        using var temp = new TempDir();
        temp.WriteFile("blog", "unterminated.en.md", "---\ntitle: Oops\nlocale: en\nslug: unterminated\n");

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();
        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(ParseErrorRule), finding.Rule);
        Assert.Contains("unterminated.en.md", finding.File);
    }

    [Fact]
    public void MarkdownWithEmptyFrontmatter_ProducesParseErrorFinding()
    {
        // `---\n---` — fences adjacent with nothing between them. Before the fix, closeIndex
        // == afterOpen made the slice `[afterOpen+1)..closeIndex)` a negative-length range,
        // which either threw or silently produced an empty-but-"valid" frontmatter. Either
        // way it must surface as a named parse-error finding, not crash the run.
        using var temp = new TempDir();
        temp.WriteFile("blog", "empty-frontmatter.en.md", "---\n---\nJust prose below.\n");

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();
        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(ParseErrorRule), finding.Rule);
        Assert.Contains("empty-frontmatter.en.md", finding.File);
    }

    [Fact]
    public void PagesSectionsMissing_ProducesPagesSectionsPresentFinding()
    {
        using var temp = new TempDir();
        temp.WriteFile("pages", "about.en.yaml", """
            slug: about
            locale: en
            kicker: About
            title: About
            lede: Lede text.
            """);

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();
        var findings = Verifier.Run(entries);

        var finding = Assert.Single(findings);
        Assert.Equal(nameof(PagesSectionsPresentRule), finding.Rule);
        Assert.Contains("about.en.yaml", finding.File);
        Assert.Contains("sections", finding.Detail);
    }

    [Fact]
    public void PagesSectionsPresent_ForNonGatedSlug_DoesNotRequireSections()
    {
        using var temp = new TempDir();
        temp.WriteFile("pages", "portfolio.en.yaml", """
            slug: portfolio
            locale: en
            kicker: Portfolio
            title: Portfolio
            lede: Lede text.
            """);

        var entries = SnapshotReader.ReadAll(temp.Path).ToList();
        var findings = Verifier.Run(entries);

        Assert.Empty(findings);
    }
}
