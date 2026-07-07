using System.Diagnostics;
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

    [Fact]
    public async Task RunAsync_ThrowingOnLineKillsTheProcessAndSurfacesTheCallbackException()
    {
        // A chatty child that would run ~30s if not killed: without the latch-and-
        // drain defense, the faulted pump stops reading, the child blocks on the
        // full pipe buffer, and RunAsync deadlocks.
        using var temp = new TempDir();
        var runner = new ProcessRunner();
        var boom = new InvalidDataException("boom");

        var stopwatch = Stopwatch.StartNew();
        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => runner.RunAsync(
                "bash",
                ["-c", "for i in $(seq 1 200000); do echo line $i; done; sleep 30"],
                temp.Path,
                _ => throw boom,
                CancellationToken.None));
        stopwatch.Stop();

        Assert.Same(boom, ex);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            $"RunAsync took {stopwatch.Elapsed} — the process was not killed promptly.");
    }

    [Fact]
    public async Task RunAsync_CancellationKillsTheProcessTreeAndSurfacesOperationCanceled()
    {
        using var temp = new TempDir();
        var runner = new ProcessRunner();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runner.RunAsync(
                "bash", ["-c", "sleep 30"], temp.Path, _ => { }, cts.Token));
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            $"RunAsync took {stopwatch.Elapsed} — cancellation did not return promptly.");
    }
}
