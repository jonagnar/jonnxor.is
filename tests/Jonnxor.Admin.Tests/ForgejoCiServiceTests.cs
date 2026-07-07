using System.Net;
using Jonnxor.Admin.Services;

namespace Jonnxor.Admin.Tests;

public class ForgejoCiServiceTests
{
    // Recorded from the running Forgejo 13 instance's
    // /api/v1/repos/{owner}/{repo}/actions/tasks response shape (extra fields kept
    // so parsing is proven lenient against the real payload).
    private const string RecordedBody = """
        {
          "total_count": 4,
          "workflow_runs": [
            {
              "id": 301, "name": "CI", "head_branch": "preview", "head_sha": "abc123",
              "run_number": 52, "event": "push", "display_title": "feat(admin): health services",
              "status": "success", "workflow_id": "ci.yml",
              "url": "http://localhost:3000/WAAAGH/jonnxor.is/actions/runs/52",
              "created_at": "2026-07-07T12:55:35Z", "updated_at": "2026-07-07T12:58:01Z",
              "run_started_at": "2026-07-07T12:55:40Z"
            },
            {
              "id": 300, "name": "CI", "head_branch": "main", "head_sha": "def456",
              "run_number": 51, "event": "push", "display_title": "Merge pull request 'preview'",
              "status": "success", "workflow_id": "ci.yml",
              "url": "http://localhost:3000/WAAAGH/jonnxor.is/actions/runs/51",
              "created_at": "2026-07-07T10:00:00Z", "updated_at": "2026-07-07T10:03:00Z",
              "run_started_at": "2026-07-07T10:00:05Z"
            },
            {
              "id": 299, "name": "CI", "head_branch": "preview", "head_sha": "ghi789",
              "run_number": 50, "event": "push", "display_title": "older preview run",
              "status": "failure", "workflow_id": "ci.yml",
              "url": "http://localhost:3000/WAAAGH/jonnxor.is/actions/runs/50",
              "created_at": "2026-07-06T09:00:00Z", "updated_at": "2026-07-06T09:04:00Z",
              "run_started_at": "2026-07-06T09:00:04Z"
            },
            {
              "id": 298, "name": "CI", "head_branch": "claude/great-nobel-7ac806", "head_sha": "jkl012",
              "run_number": 49, "event": "push", "display_title": "feat(admin): scaffold",
              "status": "running", "workflow_id": "ci.yml",
              "url": "http://localhost:3000/WAAAGH/jonnxor.is/actions/runs/49",
              "created_at": "2026-07-05T09:00:00Z", "updated_at": "2026-07-05T09:00:10Z",
              "run_started_at": "2026-07-05T09:00:03Z"
            }
          ]
        }
        """;

    private static readonly AdminOptions Options = new()
    {
        ForgejoBaseUrl = "http://forgejo.test:3000",
        ForgejoRepo = "WAAAGH/jonnxor.is",
    };

    private static string? WithToken(string key) => key == "FORGEJO_TOKEN" ? "secret-token" : null;

    private static string? NoToken(string _) => null;

    [Fact]
    public async Task GetLatestPerBranchAsync_RecordedBody_NewestPerBranchOrderedMainPreviewRest()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, RecordedBody);
        var service = new ForgejoCiService(Options, handler, WithToken);

        var status = await service.GetLatestPerBranchAsync();

        Assert.True(status.Available);
        Assert.Null(status.Detail);
        Assert.Equal(3, status.Runs.Count);

        Assert.Equal("main", status.Runs[0].HeadBranch);
        Assert.Equal(51, status.Runs[0].RunNumber);
        Assert.Equal("success", status.Runs[0].Status);
        Assert.Equal("Merge pull request 'preview'", status.Runs[0].DisplayTitle);
        Assert.Equal("CI", status.Runs[0].Name);
        Assert.Equal(DateTimeOffset.Parse("2026-07-07T10:00:00Z"), status.Runs[0].CreatedAt);

        // Newest preview run (52) wins over the older one (50).
        Assert.Equal("preview", status.Runs[1].HeadBranch);
        Assert.Equal(52, status.Runs[1].RunNumber);

        Assert.Equal("claude/great-nobel-7ac806", status.Runs[2].HeadBranch);
        Assert.Equal("running", status.Runs[2].Status);
    }

    [Fact]
    public async Task GetLatestPerBranchAsync_SendsTokenHeaderToRunsEndpoint()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, RecordedBody);
        var service = new ForgejoCiService(Options, handler, WithToken);

        await service.GetLatestPerBranchAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "http://forgejo.test:3000/api/v1/repos/WAAAGH/jonnxor.is/actions/tasks?limit=30",
            request.RequestUri!.ToString());
        Assert.Equal("token secret-token", request.Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public async Task GetLatestPerBranchAsync_NoToken_DegradedResultWithoutRequest()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, RecordedBody);
        var service = new ForgejoCiService(Options, handler, NoToken);

        var status = await service.GetLatestPerBranchAsync();

        Assert.False(status.Available);
        Assert.Equal("no FORGEJO_TOKEN configured", status.Detail);
        Assert.Empty(status.Runs);
        Assert.Empty(handler.Requests); // never even tries without a token
    }

    [Fact]
    public async Task GetLatestPerBranchAsync_Unauthorized_DegradedResult()
    {
        var handler = FakeHttpMessageHandler.Json(
            HttpStatusCode.Unauthorized, """{"message":"token is required"}""");
        var service = new ForgejoCiService(Options, handler, WithToken);

        var status = await service.GetLatestPerBranchAsync();

        Assert.False(status.Available);
        Assert.NotNull(status.Detail);
        Assert.Contains("401", status.Detail);
        Assert.Empty(status.Runs);
    }

    [Fact]
    public async Task GetLatestPerBranchAsync_EmptyRuns_AvailableWithNoRuns()
    {
        var handler = FakeHttpMessageHandler.Json(
            HttpStatusCode.OK, """{"total_count":0,"workflow_runs":[]}""");
        var service = new ForgejoCiService(Options, handler, WithToken);

        var status = await service.GetLatestPerBranchAsync();

        Assert.True(status.Available);
        Assert.Empty(status.Runs);
    }

    [Fact]
    public async Task GetLatestPerBranchAsync_MalformedBody_DegradedResultNotException()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, "this is not json {");
        var service = new ForgejoCiService(Options, handler, WithToken);

        var status = await service.GetLatestPerBranchAsync();

        Assert.False(status.Available);
        Assert.NotNull(status.Detail);
        Assert.Empty(status.Runs);
    }

    [Fact]
    public async Task GetLatestPerBranchAsync_MalformedRunEntry_SkippedNotFatal()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, """
            {
              "workflow_runs": [
                "not-an-object",
                {"name":"CI","head_branch":"main","display_title":"ok run","status":"success",
                 "created_at":"2026-07-07T10:00:00Z","run_number":51}
              ]
            }
            """);
        var service = new ForgejoCiService(Options, handler, WithToken);

        var status = await service.GetLatestPerBranchAsync();

        Assert.True(status.Available);
        var run = Assert.Single(status.Runs);
        Assert.Equal("main", run.HeadBranch);
    }

    [Fact]
    public async Task GetLatestPerBranchAsync_ForgejoDown_DegradedResult()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var service = new ForgejoCiService(Options, handler, WithToken);

        var status = await service.GetLatestPerBranchAsync();

        Assert.False(status.Available);
        Assert.NotNull(status.Detail);
        Assert.Contains("connection refused", status.Detail);
    }
}
