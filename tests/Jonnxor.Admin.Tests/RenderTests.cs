using System.Net;
using Jonnxor.Admin.Components.Pages;
using Jonnxor.Admin.Components.Tiles;
using Jonnxor.Admin.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jonnxor.Admin.Tests;

/// <summary>
/// Component render tests via the framework's built-in <see cref="HtmlRenderer"/>
/// (per plan refinement 2 — no bUnit unless it proves insufficient). Static rendering
/// executes <c>OnInitializedAsync</c>, so the Dashboard's single-pass load is exactly
/// what these tests exercise; services are the real classes wired with the same fake
/// seams (handler/getEnv/runner) the service tests use.
/// </summary>
public class RenderTests
{
    private const string HealthyDirectusBody = """{"status":"ok"}""";

    private const string SingleRunCiBody = """
        {
          "workflow_runs": [
            {
              "name": "CI", "head_branch": "main", "display_title": "Merge pull request 'preview'",
              "status": "success", "run_number": 51, "created_at": "2026-07-07T10:00:00Z"
            }
          ]
        }
        """;

    private const string FailedFeatureBranchCiBody = """
        {
          "workflow_runs": [
            {
              "name": "CI", "head_branch": "main", "display_title": "Merge pull request 'preview'",
              "status": "success", "run_number": 51, "created_at": "2026-07-07T10:00:00Z"
            },
            {
              "name": "CI", "head_branch": "preview", "display_title": "feat: something",
              "status": "success", "run_number": 52, "created_at": "2026-07-07T11:00:00Z"
            },
            {
              "name": "CI", "head_branch": "claude/stale-branch", "display_title": "wip",
              "status": "failure", "run_number": 49, "created_at": "2026-07-05T09:00:00Z"
            }
          ]
        }
        """;

    private const string FailedMainCiBody = """
        {
          "workflow_runs": [
            {
              "name": "CI", "head_branch": "main", "display_title": "Merge pull request 'preview'",
              "status": "failure", "run_number": 51, "created_at": "2026-07-07T10:00:00Z"
            }
          ]
        }
        """;

    private static readonly AdminOptions CiOptions = new()
    {
        ForgejoBaseUrl = "http://forgejo.test:3000",
        ForgejoRepo = "WAAAGH/jonnxor.is",
    };

    private static string? DirectusEnv(string key)
        => key == "DIRECTUS_URL" ? "http://directus.test:8055" : null;

    private static string? WithToken(string key) => key == "FORGEJO_TOKEN" ? "secret" : null;

    private static string? NoEnv(string _) => null;

    private static RepoPaths PathsFor(TempDir temp) => new(new AdminOptions { RepoRoot = temp.Path });

    private static FakeProcessRunner CleanGit(RepoPaths paths) => new FakeProcessRunner()
        .Expect("git", ["log", "-1", "--format=%cI", "--", paths.ContentDir],
            [DateTimeOffset.Now.AddHours(-3).ToString("yyyy-MM-dd'T'HH:mm:sszzz")], 0)
        .Expect("git", ["status", "--porcelain", "--", paths.ContentDir], [], 0);

    private static ServiceProvider BuildProvider(
        DirectusHealthService directus, SnapshotHealthService snapshot, ForgejoCiService ci)
    {
        var services = new ServiceCollection();
        services.AddLogging(); // HtmlRenderer requires ILoggerFactory
        services.AddSingleton(directus);
        services.AddSingleton(snapshot);
        services.AddSingleton(ci);
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync<TComponent>(
        IServiceProvider services, ParameterView parameters) where TComponent : IComponent
    {
        await using var renderer = new HtmlRenderer(
            services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(parameters);
            return output.ToHtmlString();
        });
    }

    [Fact]
    public async Task Dashboard_AllHealthy_RendersThreeTruthfulTiles()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        await using var provider = BuildProvider(
            new DirectusHealthService(
                paths, FakeHttpMessageHandler.Json(HttpStatusCode.OK, HealthyDirectusBody), DirectusEnv),
            new SnapshotHealthService(paths, CleanGit(paths)),
            new ForgejoCiService(
                CiOptions, FakeHttpMessageHandler.Json(HttpStatusCode.OK, SingleRunCiBody), WithToken));

        var html = await RenderAsync<Dashboard>(provider, ParameterView.Empty);

        // All three tiles present, each with truthful content.
        Assert.Contains("Directus", html);
        Assert.Contains("Snapshot freshness", html);
        Assert.Contains("Last CI", html);
        Assert.Equal(3, CountOccurrences(html, "tile-head"));

        Assert.Contains("status: ok", html);
        Assert.Contains("in sync", html);
        Assert.Contains("last snapshot commit: 3 h ago", html);
        Assert.Contains("main: success (#51", html);
        Assert.Contains("tile-status--ok", html);

        // Refresh is a real, accessible button on every tile, disambiguated per tile.
        Assert.Equal(3, CountOccurrences(html, "class=\"tile-refresh\""));
        Assert.Contains("aria-label=\"Refresh Directus\"", html);
        Assert.Contains("aria-label=\"Refresh Snapshot freshness\"", html);
        Assert.Contains("aria-label=\"Refresh Last CI\"", html);
    }

    [Fact]
    public async Task Dashboard_FailedFeatureBranchGreenDeployBranches_RendersWarnNotFail()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        await using var provider = BuildProvider(
            new DirectusHealthService(
                paths, FakeHttpMessageHandler.Json(HttpStatusCode.OK, HealthyDirectusBody), DirectusEnv),
            new SnapshotHealthService(paths, CleanGit(paths)),
            new ForgejoCiService(
                CiOptions, FakeHttpMessageHandler.Json(HttpStatusCode.OK, FailedFeatureBranchCiBody),
                WithToken));

        var html = await RenderAsync<Dashboard>(provider, ParameterView.Empty);

        // A stale failed feature branch must not red the tile while main/preview
        // are green — warn, with the branch's failure still visible per-branch.
        Assert.Contains("tile-status--warn", html);
        Assert.DoesNotContain("tile-status--fail", html);
        Assert.Contains("claude/stale-branch: failure (#49", html);
        Assert.Contains("main: success (#51", html);
    }

    [Fact]
    public async Task Dashboard_FailedMainRun_RendersFail()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        await using var provider = BuildProvider(
            new DirectusHealthService(
                paths, FakeHttpMessageHandler.Json(HttpStatusCode.OK, HealthyDirectusBody), DirectusEnv),
            new SnapshotHealthService(paths, CleanGit(paths)),
            new ForgejoCiService(
                CiOptions, FakeHttpMessageHandler.Json(HttpStatusCode.OK, FailedMainCiBody), WithToken));

        var html = await RenderAsync<Dashboard>(provider, ParameterView.Empty);

        Assert.Contains("tile-status--fail", html);
        Assert.Contains("main: failure (#51", html);
    }

    [Fact]
    public async Task Dashboard_DirectusOkBodyBehindHttpError_RendersWarnNotOk()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        await using var provider = BuildProvider(
            // A 503 whose body still claims "ok": Detail names the HTTP problem and
            // the tile must warn, never render a false all-clear from Status alone.
            new DirectusHealthService(
                paths,
                FakeHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, HealthyDirectusBody),
                DirectusEnv),
            new SnapshotHealthService(paths, CleanGit(paths)),
            new ForgejoCiService(
                CiOptions, FakeHttpMessageHandler.Json(HttpStatusCode.OK, SingleRunCiBody), WithToken));

        var html = await RenderAsync<Dashboard>(provider, ParameterView.Empty);

        Assert.Contains("tile-status--warn", html);
        Assert.Contains("HTTP 503", html);
    }

    [Fact]
    public async Task Dashboard_NoForgejoToken_RendersDegradedMessageNotException()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        await using var provider = BuildProvider(
            new DirectusHealthService(
                paths, FakeHttpMessageHandler.Json(HttpStatusCode.OK, HealthyDirectusBody), DirectusEnv),
            new SnapshotHealthService(paths, CleanGit(paths)),
            new ForgejoCiService(CiOptions, new FakeHttpMessageHandler(
                _ => throw new InvalidOperationException("must not be called without a token")),
                NoEnv));

        var html = await RenderAsync<Dashboard>(provider, ParameterView.Empty);

        Assert.Contains(ForgejoCiService.NoTokenDetail, html);
        Assert.Contains("tile-status--unknown", html);
        Assert.Equal(3, CountOccurrences(html, "tile-head")); // page intact
    }

    [Fact]
    public async Task Dashboard_ThrowingService_DegradesOneTileNeverBlanksThePage()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        await using var provider = BuildProvider(
            // CheckAsync only catches transport exceptions — this one escapes the
            // service and must be caught by the tile's own isolation wrapper.
            new DirectusHealthService(
                paths, new FakeHttpMessageHandler(_ => throw new InvalidOperationException("boom")),
                DirectusEnv),
            new SnapshotHealthService(paths, CleanGit(paths)),
            new ForgejoCiService(
                CiOptions, FakeHttpMessageHandler.Json(HttpStatusCode.OK, SingleRunCiBody), WithToken));

        var html = await RenderAsync<Dashboard>(provider, ParameterView.Empty);

        Assert.Contains("error: boom", html);
        Assert.Contains("tile-status--unknown", html);
        // The other two tiles still rendered with their real data.
        Assert.Contains("in sync", html);
        Assert.Contains("main: success (#51", html);
        Assert.Equal(3, CountOccurrences(html, "tile-head"));
    }

    [Fact]
    public async Task Dashboard_GitFailure_RendersUnknownNeverInSync()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var failingGit = new FakeProcessRunner()
            .Expect("git", ["log", "-1", "--format=%cI", "--", paths.ContentDir],
                ["! fatal: not a git repository"], 128)
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir],
                ["! fatal: not a git repository"], 128);
        await using var provider = BuildProvider(
            new DirectusHealthService(
                paths, FakeHttpMessageHandler.Json(HttpStatusCode.OK, HealthyDirectusBody), DirectusEnv),
            new SnapshotHealthService(paths, failingGit),
            new ForgejoCiService(
                CiOptions, FakeHttpMessageHandler.Json(HttpStatusCode.OK, SingleRunCiBody), WithToken));

        var html = await RenderAsync<Dashboard>(provider, ParameterView.Empty);

        // Review-mandated: GitOk=false is "unknown", never a false "clean/in sync".
        Assert.Contains("freshness unknown", html);
        Assert.DoesNotContain("in sync", html);
        Assert.Contains("tile-status--unknown", html);
    }

    [Fact]
    public async Task Dashboard_DirtySnapshot_RendersPendingChangeCount()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var dirtyGit = new FakeProcessRunner()
            .Expect("git", ["log", "-1", "--format=%cI", "--", paths.ContentDir],
                [DateTimeOffset.Now.AddMinutes(-5).ToString("yyyy-MM-dd'T'HH:mm:sszzz")], 0)
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir],
                [" M client/src/content/blog/foo.en.md", "?? client/src/content/blog/new.ja.md"], 0);
        await using var provider = BuildProvider(
            new DirectusHealthService(
                paths, FakeHttpMessageHandler.Json(HttpStatusCode.OK, HealthyDirectusBody), DirectusEnv),
            new SnapshotHealthService(paths, dirtyGit),
            new ForgejoCiService(
                CiOptions, FakeHttpMessageHandler.Json(HttpStatusCode.OK, SingleRunCiBody), WithToken));

        var html = await RenderAsync<Dashboard>(provider, ParameterView.Empty);

        Assert.Contains("2 uncommitted change(s) pending", html);
        Assert.Contains("last snapshot commit: 5 min ago", html);
        Assert.DoesNotContain("in sync", html);
        Assert.Contains("tile-status--warn", html);
    }

    [Theory]
    [InlineData(TileStatus.Ok, "tile-status--ok", ">ok<")]
    [InlineData(TileStatus.Warn, "tile-status--warn", ">warn<")]
    [InlineData(TileStatus.Fail, "tile-status--fail", ">fail<")]
    [InlineData(TileStatus.Unknown, "tile-status--unknown", ">unknown<")]
    public async Task HealthTile_MapsEachStatusToItsCssClassAndLabel(
        TileStatus status, string expectedClass, string expectedLabel)
    {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(HealthTile.Title)] = "Probe",
            [nameof(HealthTile.Status)] = status,
            [nameof(HealthTile.DetailLines)] = new List<string> { "detail line" },
            [nameof(HealthTile.RefreshedAt)] = DateTimeOffset.Now,
        });

        var html = await RenderAsync<HealthTile>(provider, parameters);

        Assert.Contains(expectedClass, html);
        Assert.Contains(expectedLabel, html);
        Assert.Contains("Probe", html);
        Assert.Contains("detail line", html);
        Assert.Contains("refreshed", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HealthTile_RefreshButtonDisabledTracksRefreshing(bool refreshing)
    {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(HealthTile.Title)] = "Probe",
            [nameof(HealthTile.Refreshing)] = refreshing,
        });

        var html = await RenderAsync<HealthTile>(provider, parameters);

        if (refreshing)
        {
            Assert.Contains("disabled", html);
        }
        else
        {
            Assert.DoesNotContain("disabled", html);
        }
    }

    [Fact]
    public async Task ContentPull_IdleCleanTree_ShowsThreeEnabledButtonsAndNoCommitCopy()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        // OnInitializedAsync loads the diff panel: clean tree here.
        var runner = new FakeProcessRunner()
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir], [], 0)
            .Expect("git", ["diff", "--", paths.ContentDir], [], 0);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new ContentPipelineService(
            paths, runner, new SnapshotHealthService(paths, runner)));
        await using var provider = services.BuildServiceProvider();

        var html = await RenderAsync<ContentPull>(provider, ParameterView.Empty);

        // Three action buttons, all enabled in the idle state.
        Assert.Contains(">Pull<", StripWhitespace(html));
        Assert.Contains(">Verifyoffline<", StripWhitespace(html));
        Assert.Contains(">Verifylive<", StripWhitespace(html));
        Assert.Equal(3, CountOccurrences(html, "class=\"pull-button\""));
        Assert.DoesNotContain("disabled", html);

        // The page says so explicitly: this panel never commits.
        Assert.Contains("this panel never commits", html);
        Assert.Contains("working tree clean", html);
        Assert.Contains("idle", html);
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task ContentPull_DirtyTree_RendersStatusLinesAndDiffText()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new FakeProcessRunner()
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir],
                [" M client/src/content/blog/foo.en.md"], 0)
            .Expect("git", ["diff", "--", paths.ContentDir],
                ["diff --git a/client/src/content/blog/foo.en.md b/client/src/content/blog/foo.en.md",
                 "+new line"], 0);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new ContentPipelineService(
            paths, runner, new SnapshotHealthService(paths, runner)));
        await using var provider = services.BuildServiceProvider();

        var html = await RenderAsync<ContentPull>(provider, ParameterView.Empty);

        // The renderer entity-encodes text content ("+": &#x2B;) — decode to
        // assert on what the browser will actually display.
        var text = System.Net.WebUtility.HtmlDecode(html);
        Assert.Contains("M client/src/content/blog/foo.en.md", text);
        Assert.Contains("+new line", text);
        Assert.DoesNotContain("working tree clean", text);
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task Coverage_MixedLocaleFixture_RendersZeroPercentJaCellAndDrillDown()
    {
        var coverage = new CoverageService(new RepoPaths(new AdminOptions
        {
            RepoRoot = Path.GetTempPath(),
            ContentDir = FixturePath.For("mixed-locale"),
        }));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(coverage);
        await using var provider = services.BuildServiceProvider();

        var html = await RenderAsync<Coverage>(provider, ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(Coverage.SelectedCollection)] = "blog",
                [nameof(Coverage.SelectedLocale)] = "ja",
            }));

        // The table shows the same numbers as the `report` verb: blog ja is 0/2 (0%).
        Assert.Contains("0/2 (0%)", html);
        Assert.Contains("1/2 (50%)", html);  // blog is
        Assert.Contains("2/2 (100%)", html); // blog en
        Assert.Contains("blog", html);
        Assert.Contains("grimoire", html);
        Assert.Contains("TOTAL", html);

        // Drill-down for (blog, ja) lists the missing slugs by name — beta's ja file
        // is a parse error, so it counts as missing alongside alpha.
        Assert.Contains("slugs missing ja", html);
        Assert.Contains("alpha", html);
        Assert.Contains("beta", html);
    }

    [Fact]
    public async Task Coverage_MissingContentDir_DegradesToDetailNotACrash()
    {
        using var temp = new TempDir();
        var coverage = new CoverageService(new RepoPaths(new AdminOptions
        {
            RepoRoot = temp.Path,
            ContentDir = "does-not-exist",
        }));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(coverage);
        await using var provider = services.BuildServiceProvider();

        // Deep-link parameters set on purpose: a degraded build must IGNORE them —
        // rendering the drill-down's "none missing" copy over an empty report would
        // be a false all-clear.
        var html = await RenderAsync<Coverage>(provider, ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(Coverage.SelectedCollection)] = "blog",
                [nameof(Coverage.SelectedLocale)] = "ja",
            }));

        Assert.Contains("does-not-exist", html);
        Assert.Contains("TOTAL", html); // the (empty) table still renders
        Assert.DoesNotContain("coverage-drill", html); // no drill-down section at all
        Assert.DoesNotContain("none —", html);
    }

    private static string StripWhitespace(string html)
        => string.Concat(html.Where(c => !char.IsWhiteSpace(c)));

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal);
             i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
