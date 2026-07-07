using Jonnxor.Admin.Services;

namespace Jonnxor.Admin.Tests;

public class ContentPipelineServiceTests
{
    private static RepoPaths PathsFor(TempDir temp) => new(new AdminOptions { RepoRoot = temp.Path });

    private static ContentPipelineService ServiceFor(
        RepoPaths paths, IProcessRunner runner, Func<string[], TextWriter, TextWriter, int>? liveVerify = null)
        => new(paths, runner, new SnapshotHealthService(paths, runner), liveVerify);

    [Fact]
    public async Task RunPullAsync_HappyPath_StreamsIntoBufferAndReportsExitZero()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var transcript = new[] { "pull: blog 4 entries", "pull: grimoire 2 entries", "done" };
        var runner = new FakeProcessRunner()
            .Expect("pnpm", ["content:pull"], transcript, 0);
        var service = ServiceFor(paths, runner);
        var notifications = 0;
        service.OutputChanged += () => notifications++;

        var run = await service.RunPullAsync();

        Assert.Equal(ContentPipelineService.PullKind, run.Kind);
        Assert.False(run.Running);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(transcript, service.OutputSnapshot);
        // pnpm runs in the JS workspace, exactly where the operator would run it.
        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal(paths.ClientDir, invocation.WorkingDir);
        // At least one notification per line plus run start/completion.
        Assert.True(notifications >= transcript.Length + 2);
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task RunPullAsync_Failure_PropagatesExitCodeAsData()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new FakeProcessRunner()
            .Expect("pnpm", ["content:pull"], ["! ERR_PNPM_SOMETHING failed"], 1);
        var service = ServiceFor(paths, runner);

        var run = await service.RunPullAsync();

        Assert.False(run.Running);
        Assert.Equal(1, run.ExitCode);
        Assert.Contains("! ERR_PNPM_SOMETHING failed", service.OutputSnapshot);
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task RunPullAsync_SecondTriggerWhileRunning_RejectedNotQueued()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new BlockingProcessRunner();
        var liveVerifyCalls = 0;
        var service = ServiceFor(paths, runner, (_, _, _) => { liveVerifyCalls++; return 0; });

        var first = service.RunPullAsync();
        Assert.True(service.CurrentRun is { Running: true, Kind: ContentPipelineService.PullKind });

        // A second pull AND a live verify must both bounce off the one global lock,
        // reporting what is running — and neither may queue behind the first.
        var rejectedPull = await service.RunPullAsync();
        var rejectedVerify = await service.RunVerifyLiveAsync();

        Assert.True(rejectedPull.Running);
        Assert.Equal(ContentPipelineService.PullKind, rejectedPull.Kind);
        Assert.Null(rejectedPull.ExitCode);
        Assert.True(rejectedVerify.Running);
        Assert.Equal(ContentPipelineService.PullKind, rejectedVerify.Kind);
        Assert.Equal(0, liveVerifyCalls);

        runner.Complete(0);
        var done = await first;
        Assert.False(done.Running);
        Assert.Equal(0, done.ExitCode);
        // Only the first trigger ever reached the process seam.
        Assert.Equal(1, runner.InvocationCount);
    }

    [Fact]
    public async Task RunPullAsync_BufferCapEnforced_DropsOldestBeyond2000Lines()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var lines = Enumerable.Range(0, 2100).Select(i => $"line {i}").ToArray();
        var runner = new FakeProcessRunner().Expect("pnpm", ["content:pull"], lines, 0);
        var service = ServiceFor(paths, runner);

        await service.RunPullAsync();

        var snapshot = service.OutputSnapshot;
        Assert.Equal(2000, snapshot.Count);
        Assert.Equal("line 100", snapshot[0]);   // oldest 100 dropped
        Assert.Equal("line 2099", snapshot[^1]); // newest kept
    }

    [Fact]
    public async Task RunPullAsync_NewRun_ClearsPreviousRunsOutput()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new FakeProcessRunner()
            .Expect("pnpm", ["content:pull"], ["first run"], 0)
            .Expect("pnpm", ["content:pull"], ["second run"], 0);
        var service = ServiceFor(paths, runner);

        await service.RunPullAsync();
        await service.RunPullAsync();

        Assert.Equal(new[] { "second run" }, service.OutputSnapshot);
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task GetDiffAsync_ParsesStatusAndDiff_ReadOnlyGitAtRepoRoot()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new FakeProcessRunner()
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir],
                [" M client/src/content/blog/foo.en.md", "?? client/src/content/blog/new.ja.md"], 0)
            .Expect("git", ["diff", "--", paths.ContentDir],
                [
                    "diff --git a/client/src/content/blog/foo.en.md b/client/src/content/blog/foo.en.md",
                    "--- a/client/src/content/blog/foo.en.md",
                    "+++ b/client/src/content/blog/foo.en.md",
                    "+new line",
                    "! warning: LF will be replaced by CRLF",
                ], 0);
        var service = ServiceFor(paths, runner);

        var diff = await service.GetDiffAsync();

        Assert.Equal(2, diff.StatusLines.Count);
        Assert.Equal(" M client/src/content/blog/foo.en.md", diff.StatusLines[0]);
        Assert.Contains("+new line", diff.DiffText);
        // Stderr lines (prefixed "! " by IProcessRunner) must not corrupt the diff text.
        Assert.DoesNotContain("warning: LF", diff.DiffText);
        Assert.All(runner.Invocations, i => Assert.Equal(paths.RepoRoot, i.WorkingDir));
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task GetDiffAsync_CleanTree_YieldsEmptyDiff()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new FakeProcessRunner()
            .Expect("git", ["status", "--porcelain", "--", paths.ContentDir], [], 0)
            .Expect("git", ["diff", "--", paths.ContentDir], [], 0);
        var service = ServiceFor(paths, runner);

        var diff = await service.GetDiffAsync();

        Assert.Empty(diff.StatusLines);
        Assert.Equal(string.Empty, diff.DiffText);
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task RunVerifyLiveAsync_PassesExactCliArgsAndCapturesOutput()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        string[]? capturedArgs = null;
        var service = ServiceFor(paths, new FakeProcessRunner(), (args, stdout, stderr) =>
        {
            capturedArgs = args;
            stdout.WriteLine("verify --live: 3 finding(s) across 7 collection(s)");
            stderr.WriteLine("blog/foo: en title drift");
            return 1;
        });

        var run = await service.RunVerifyLiveAsync();

        Assert.Equal(
            new[] { "verify", "--live", "--content", paths.ContentDir, "--env", paths.DirectusEnvPath },
            capturedArgs);
        Assert.Equal(ContentPipelineService.VerifyLiveKind, run.Kind);
        Assert.False(run.Running);
        Assert.Equal(1, run.ExitCode); // the CLI exit code IS the result, verbatim
        Assert.Contains("verify --live: 3 finding(s) across 7 collection(s)", service.OutputSnapshot);
        // Stderr is buffered with the same "! " convention IProcessRunner uses.
        Assert.Contains("! blog/foo: en title drift", service.OutputSnapshot);
    }

    [Fact]
    public async Task RunVerifyLiveAsync_WhileVerifyRunning_PullRejectedWithVerifyKind()
    {
        using var temp = new TempDir();
        var paths = PathsFor(temp);
        var runner = new FakeProcessRunner();
        using var verifyStarted = new SemaphoreSlim(0, 1);
        using var release = new SemaphoreSlim(0, 1);
        var service = ServiceFor(paths, runner, (_, stdout, _) =>
        {
            verifyStarted.Release();
            release.Wait();
            stdout.WriteLine("verify --live: OK");
            return 0;
        });

        var verify = service.RunVerifyLiveAsync();
        await verifyStarted.WaitAsync();

        var rejected = await service.RunPullAsync();
        Assert.True(rejected.Running);
        Assert.Equal(ContentPipelineService.VerifyLiveKind, rejected.Kind);
        Assert.Empty(runner.Invocations); // the rejected pull never spawned pnpm

        release.Release();
        var done = await verify;
        Assert.Equal(0, done.ExitCode);
    }

    [Fact]
    public async Task RunVerifyOfflineAsync_DelegatesToSnapshotHealth_TypedFindings()
    {
        var paths = new RepoPaths(new AdminOptions
        {
            RepoRoot = Path.GetTempPath(),
            ContentDir = FixturePath.For("valid"),
        });
        var service = ServiceFor(paths, new FakeProcessRunner());

        var result = await service.RunVerifyOfflineAsync();

        Assert.Empty(result.Findings);
        Assert.Equal(2, result.FileCount);
    }

    [Fact]
    public void ContentPipelineService_HasNoCommitOrPushSurface()
    {
        // Posture invariant (design §2): the panel never writes. No method on the
        // pipeline service may even smell like a git write. Guarded reflectively so
        // a future "convenience" commit helper fails this test by name.
        var forbidden = new[] { "commit", "push", "add", "stage", "write", "mutate" };
        var methods = typeof(ContentPipelineService)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialName) // skip property/event accessors (add_OutputChanged)
            .Select(m => m.Name.ToLowerInvariant());

        foreach (var name in methods)
        {
            Assert.DoesNotContain(forbidden, verb => name.Contains(verb, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// A runner whose single invocation stays in flight until the test releases it —
    /// the seam for asserting the global lock's reject-not-queue behavior.
    /// </summary>
    private sealed class BlockingProcessRunner : IProcessRunner
    {
        private readonly TaskCompletionSource<int> _exit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _invocationCount;

        public int InvocationCount => _invocationCount;

        public void Complete(int exitCode) => _exit.SetResult(exitCode);

        public async Task<int> RunAsync(
            string fileName, string[] args, string workingDir, Action<string> onLine, CancellationToken ct)
        {
            Interlocked.Increment(ref _invocationCount);
            onLine("streaming…");
            return await _exit.Task;
        }
    }
}
