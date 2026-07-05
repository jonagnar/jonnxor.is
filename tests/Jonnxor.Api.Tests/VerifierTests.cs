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
    public void PagesSectionsMissing_ProducesPagesSectionsPresentFinding()
    {
        using var temp = new TempDir();
        var pagesDir = Directory.CreateDirectory(Path.Combine(temp.Path, "pages"));
        File.WriteAllText(Path.Combine(pagesDir.FullName, "about.en.yaml"), """
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
        var pagesDir = Directory.CreateDirectory(Path.Combine(temp.Path, "pages"));
        File.WriteAllText(Path.Combine(pagesDir.FullName, "portfolio.en.yaml"), """
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

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("jonnxor-api-tests-").FullName;

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // best-effort cleanup
            }
        }
    }
}
