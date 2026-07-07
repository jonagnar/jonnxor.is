using Jonnxor.Admin.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Jonnxor.Admin.Tests;

public class RepoPathsTests
{
    [Fact]
    public void ExplicitRepoRoot_WinsOverWalkUp()
    {
        using var temp = new TempDir();
        temp.WriteFile("walkup/jonnxor.sln", "");
        var explicitRoot = temp.CreateDir("explicit");
        var startDir = temp.CreateDir("walkup", "nested");

        var paths = new RepoPaths(new AdminOptions { RepoRoot = explicitRoot }, startDir);

        Assert.Equal(explicitRoot, paths.RepoRoot);
    }

    [Fact]
    public void WalkUp_FindsDirectoryContainingSln()
    {
        using var temp = new TempDir();
        temp.WriteFile("repo/jonnxor.sln", "");
        var startDir = temp.CreateDir("repo", "admin", "bin", "Debug");

        var paths = new RepoPaths(new AdminOptions(), startDir);

        Assert.Equal(Path.Combine(temp.Path, "repo"), paths.RepoRoot);
    }

    [Fact]
    public void WalkUp_AcceptsStartDirectoryItself()
    {
        using var temp = new TempDir();
        temp.WriteFile("repo/jonnxor.sln", "");
        var startDir = Path.Combine(temp.Path, "repo");

        var paths = new RepoPaths(new AdminOptions(), startDir);

        Assert.Equal(startDir, paths.RepoRoot);
    }

    [Fact]
    public void WalkUp_NearestSlnWinsWhenNested()
    {
        using var temp = new TempDir();
        temp.WriteFile("outer/jonnxor.sln", "");
        temp.WriteFile("outer/inner/jonnxor.sln", "");
        var startDir = temp.CreateDir("outer", "inner", "deep");

        var paths = new RepoPaths(new AdminOptions(), startDir);

        Assert.Equal(Path.Combine(temp.Path, "outer", "inner"), paths.RepoRoot);
    }

    [Fact]
    public void WalkUp_ThrowsNamingSlnAndStartDirectoryWhenNotFound()
    {
        using var temp = new TempDir();
        var startDir = temp.CreateDir("nowhere");

        var ex = Assert.Throws<InvalidOperationException>(
            () => new RepoPaths(new AdminOptions(), startDir));

        Assert.Contains("jonnxor.sln", ex.Message);
        Assert.Contains(startDir, ex.Message);
        Assert.Contains("Admin:RepoRoot", ex.Message);
    }

    [Fact]
    public void DerivedPaths_AreAbsoluteUnderRepoRoot()
    {
        using var temp = new TempDir();
        temp.WriteFile("repo/jonnxor.sln", "");
        var root = Path.Combine(temp.Path, "repo");

        var paths = new RepoPaths(new AdminOptions(), root);

        Assert.Equal(Path.Combine(root, "client", "src", "content"), paths.ContentDir);
        Assert.Equal(Path.Combine(root, "client"), paths.ClientDir);
        Assert.Equal(Path.Combine(root, "directus", ".env"), paths.DirectusEnvPath);
    }

    [Fact]
    public void DerivedPaths_HonorConfiguredRelativePaths()
    {
        using var temp = new TempDir();
        var root = temp.CreateDir("repo");
        var options = new AdminOptions
        {
            RepoRoot = root,
            ContentDir = "content/snapshot",
            DirectusEnvPath = "env/directus.env",
        };

        var paths = new RepoPaths(options, startDirectory: null);

        Assert.Equal(Path.Combine(root, "content", "snapshot"), paths.ContentDir);
        Assert.Equal(Path.Combine(root, "env", "directus.env"), paths.DirectusEnvPath);
    }

    [Fact]
    public void ExplicitRepoRoot_RelativePathIsRefused()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => new RepoPaths(new AdminOptions { RepoRoot = "relative/root" }));

        Assert.Contains("absolute", ex.Message);
        Assert.Contains("relative/root", ex.Message);
    }

    [Fact]
    public void DefaultStartDirectory_ResolvesTheRealRepoRoot()
    {
        // AppContext.BaseDirectory is the test bin dir inside the repo, so the
        // walk-up must land on the actual checkout's jonnxor.sln.
        var paths = new RepoPaths(new AdminOptions());

        Assert.True(File.Exists(Path.Combine(paths.RepoRoot, "jonnxor.sln")));
    }

    [Fact]
    public void DefaultContainer_ConstructsRepoPathsDespiteTheOptionalStartDirectory()
    {
        // Program.cs registers RepoPaths as a bare singleton; the container must
        // fill the optional startDirectory parameter from its default value.
        var services = new ServiceCollection();
        services.AddSingleton(new AdminOptions());
        services.AddSingleton<RepoPaths>();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<RepoPaths>());
    }
}
