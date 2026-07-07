using Jonnxor.Admin.Services;

namespace Jonnxor.Admin.Tests;

/// <summary>
/// Integration-ish coverage for the real <see cref="ProcessRunner"/>: spawns `git`,
/// which exists everywhere the suite runs (dev WSL and the CI sdk image).
/// </summary>
public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_GitVersionExitsZeroAndStreamsStdout()
    {
        using var temp = new TempDir();
        var runner = new ProcessRunner();
        var lines = new List<string>();

        var exitCode = await runner.RunAsync(
            "git", ["--version"], temp.Path, lines.Add, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains(lines, line => line.StartsWith("git version", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_NonZeroExitIsDataAndStderrIsPrefixed()
    {
        using var temp = new TempDir();
        var runner = new ProcessRunner();
        var lines = new List<string>();

        var exitCode = await runner.RunAsync(
            "git", ["not-a-real-subcommand"], temp.Path, lines.Add, CancellationToken.None);

        Assert.NotEqual(0, exitCode);
        Assert.Contains(lines, line => line.StartsWith("! ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_MissingBinaryThrowsNamingTheFileName()
    {
        using var temp = new TempDir();
        var runner = new ProcessRunner();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync(
                "jonnxor-no-such-binary", [], temp.Path, _ => { }, CancellationToken.None));

        Assert.Contains("jonnxor-no-such-binary", ex.Message);
    }
}
