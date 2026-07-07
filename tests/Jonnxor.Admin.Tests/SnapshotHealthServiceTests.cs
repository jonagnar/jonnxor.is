using Jonnxor.Admin.Services;
using Jonnxor.Api.Verification;

namespace Jonnxor.Admin.Tests;

public class SnapshotHealthServiceTests
{
    private static RepoPaths PathsFor(TempDir temp) => new(new AdminOptions { RepoRoot = temp.Path });

    [Fact]
    public async Task GetFreshnessAsync_FreshAndClean_ParsesCommitStampAndReportsClean()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new FakeProcessRunner()
            .Expect("git", ["log", "-1", "--format=%cI", "--", paths.ContentDir],
                ["2026-07-07T12:55:35+00:00"], 0)
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir], [], 0);
        var service = new SnapshotHealthService(paths, runner);

        var freshness = await service.GetFreshnessAsync();

        Assert.Equal(DateTimeOffset.Parse("2026-07-07T12:55:35+00:00"), freshness.LastCommit);
        Assert.False(freshness.Dirty);
        Assert.Equal(0, freshness.FileCount);
        Assert.All(runner.Invocations, i => Assert.Equal(paths.RepoRoot, i.WorkingDir));
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task GetFreshnessAsync_StaleAndDirty_CountsPorcelainLines()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new FakeProcessRunner()
            .Expect("git", ["log", "-1", "--format=%cI", "--", paths.ContentDir],
                ["2026-06-01T08:00:00+00:00"], 0)
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir],
                [" M client/src/content/blog/foo.en.md", "?? client/src/content/blog/new.ja.md"], 0);
        var service = new SnapshotHealthService(paths, runner);

        var freshness = await service.GetFreshnessAsync();

        Assert.Equal(DateTimeOffset.Parse("2026-06-01T08:00:00+00:00"), freshness.LastCommit);
        Assert.True(freshness.Dirty);
        Assert.Equal(2, freshness.FileCount);
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task GetFreshnessAsync_GitFailure_YieldsNullCommitNotCrash()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new FakeProcessRunner()
            .Expect("git", ["log", "-1", "--format=%cI", "--", paths.ContentDir],
                ["! fatal: not a git repository"], 128)
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir],
                ["! fatal: not a git repository"], 128);
        var service = new SnapshotHealthService(paths, runner);

        var freshness = await service.GetFreshnessAsync();

        Assert.Null(freshness.LastCommit);
        Assert.False(freshness.Dirty);
        Assert.Equal(0, freshness.FileCount);
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task GetFreshnessAsync_NoCommitTouchesContent_YieldsNullCommit()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        // A path no commit ever touched: `git log -1` exits 0 with empty output.
        var runner = new FakeProcessRunner()
            .Expect("git", ["log", "-1", "--format=%cI", "--", paths.ContentDir], [], 0)
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir], [], 0);
        var service = new SnapshotHealthService(paths, runner);

        var freshness = await service.GetFreshnessAsync();

        Assert.Null(freshness.LastCommit);
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task RunOfflineVerifyAsync_ValidFixture_NoFindings()
    {
        var paths = new RepoPaths(new AdminOptions
        {
            RepoRoot = Path.GetTempPath(),
            ContentDir = FixturePath.For("valid"),
        });
        var service = new SnapshotHealthService(paths, new FakeProcessRunner());

        var result = await service.RunOfflineVerifyAsync();

        Assert.Empty(result.Findings);
        Assert.Equal(2, result.FileCount); // fable-release.en.yaml + fable-release.is.yaml
    }

    [Fact]
    public async Task RunOfflineVerifyAsync_MissingEnFixture_SurfacesTypedFinding()
    {
        var paths = new RepoPaths(new AdminOptions
        {
            RepoRoot = Path.GetTempPath(),
            ContentDir = FixturePath.For("missing-en"),
        });
        var service = new SnapshotHealthService(paths, new FakeProcessRunner());

        var result = await service.RunOfflineVerifyAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(nameof(EnBasePresentRule), finding.Rule);
        Assert.Contains("summer-solstice", finding.Detail);
        Assert.Equal(1, result.FileCount);
    }

    [Fact]
    public async Task RunOfflineVerifyAsync_MissingContentDir_SyntheticFindingNotCrash()
    {
        using var temp = new TempDir();
        var paths = new RepoPaths(new AdminOptions
        {
            RepoRoot = temp.Path,
            ContentDir = "does/not/exist",
        });
        var service = new SnapshotHealthService(paths, new FakeProcessRunner());

        var result = await service.RunOfflineVerifyAsync();

        var finding = Assert.Single(result.Findings);
        Assert.Equal(SnapshotHealthService.ContentDirMissingRule, finding.Rule);
        Assert.Equal(paths.ContentDir, finding.File);
        Assert.Equal(0, result.FileCount);
    }
}
