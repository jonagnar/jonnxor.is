using System.Net;
using Jonnxor.Admin.Services;

namespace Jonnxor.Admin.Tests;

public class DirectusHealthServiceTests
{
    /// <summary>The exact worktree caveat wording the plan mandates.</summary>
    private const string WorktreeCaveat =
        "direnv does not reach worktrees — export DIRECTUS_URL or ensure directus/.env exists "
        + "(decrypted via sops/direnv from the main checkout)";

    private static RepoPaths PathsFor(TempDir temp) => new(new AdminOptions { RepoRoot = temp.Path });

    /// <summary>getEnv double that knows no variables at all.</summary>
    private static string? NoEnv(string _) => null;

    [Fact]
    public async Task CheckAsync_HealthyResponse_ReportsReachableWithStatus()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "DIRECTUS_URL=http://localhost:8055\nADMIN_EMAIL=a@b.c\n");
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"status":"ok"}""");
        var service = new DirectusHealthService(PathsFor(temp), handler, NoEnv);

        var health = await service.CheckAsync();

        Assert.True(health.Reachable);
        Assert.Equal("ok", health.Status);
        Assert.Null(health.Detail);
        Assert.Equal("http://localhost:8055", health.Url);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://localhost:8055/server/health", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CheckAsync_EnvVarWinsOverEnvFile()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "DIRECTUS_URL=http://from-file:9999\n");
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"status":"ok"}""");
        var service = new DirectusHealthService(
            PathsFor(temp), handler, key => key == "DIRECTUS_URL" ? "http://from-env:8055" : null);

        var health = await service.CheckAsync();

        Assert.Equal("http://from-env:8055", health.Url);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://from-env:8055/server/health", request.RequestUri!.ToString());
    }

    [Fact]
    public async Task CheckAsync_ServiceUnavailable_ReportsAnsweredButUnhealthy()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "DIRECTUS_URL=http://localhost:8055\n");
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, """{"status":"error"}""");
        var service = new DirectusHealthService(PathsFor(temp), handler, NoEnv);

        var health = await service.CheckAsync();

        // The server answered, so it is reachable — Status/Detail carry the bad news.
        Assert.True(health.Reachable);
        Assert.Equal("error", health.Status);
        Assert.NotNull(health.Detail);
        Assert.Contains("503", health.Detail);
    }

    [Fact]
    public async Task CheckAsync_Timeout_ReportsUnreachable()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "DIRECTUS_URL=http://localhost:8055\n");
        // HttpClient surfaces its own timeout as a canceled task inside the handler chain.
        var handler = new FakeHttpMessageHandler(_ => throw new TaskCanceledException("simulated timeout"));
        var service = new DirectusHealthService(PathsFor(temp), handler, NoEnv);

        var health = await service.CheckAsync();

        Assert.False(health.Reachable);
        Assert.Null(health.Status);
        Assert.NotNull(health.Detail);
        Assert.Contains("timed out", health.Detail);
    }

    [Fact]
    public async Task CheckAsync_ConnectionRefused_ReportsUnreachable()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "DIRECTUS_URL=http://localhost:8055\n");
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var service = new DirectusHealthService(PathsFor(temp), handler, NoEnv);

        var health = await service.CheckAsync();

        Assert.False(health.Reachable);
        Assert.NotNull(health.Detail);
        Assert.Contains("connection refused", health.Detail);
    }

    [Fact]
    public async Task CheckAsync_MissingEnvFile_DegradesWithWorktreeCaveat()
    {
        using var temp = new TempDir(); // no directus/.env written
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"status":"ok"}""");
        var service = new DirectusHealthService(PathsFor(temp), handler, NoEnv);

        var health = await service.CheckAsync();

        Assert.False(health.Reachable);
        Assert.Null(health.Status);
        Assert.NotNull(health.Detail);
        Assert.Contains(WorktreeCaveat, health.Detail);
        Assert.Empty(handler.Requests); // no HTTP attempted without a URL
    }

    [Fact]
    public async Task CheckAsync_EnvFileWithoutKey_DegradesWithWorktreeCaveat()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "ADMIN_EMAIL=a@b.c\n");
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, """{"status":"ok"}""");
        var service = new DirectusHealthService(PathsFor(temp), handler, NoEnv);

        var health = await service.CheckAsync();

        Assert.False(health.Reachable);
        Assert.NotNull(health.Detail);
        Assert.Contains(WorktreeCaveat, health.Detail);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CheckAsync_UnparseableBody_ReachableWithNullStatus()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "DIRECTUS_URL=http://localhost:8055\n");
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, "not json at all");
        var service = new DirectusHealthService(PathsFor(temp), handler, NoEnv);

        var health = await service.CheckAsync();

        Assert.True(health.Reachable);
        Assert.Null(health.Status);
    }
}
