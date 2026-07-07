using System.Collections.Concurrent;
using Jonnxor.Api;

namespace Jonnxor.Admin.Services;

/// <summary>
/// State of the pipeline's current (or most recent) exclusive run. While
/// <see cref="Running"/> is true, <see cref="ExitCode"/> is null; a completed run
/// carries the process/CLI exit code verbatim (non-zero is data, not an error).
/// <see cref="Running"/> false with a null <see cref="ExitCode"/> is the aborted
/// state: the run ended without producing an exit code (spawn failure or
/// cancellation — the exception itself went to the triggering caller).
/// A trigger rejected by the global lock receives the in-flight run's state —
/// <see cref="Running"/> true and <see cref="Kind"/> naming what is running.
/// </summary>
public sealed record PipelineRun(string Kind, DateTimeOffset StartedAt, bool Running, int? ExitCode);

/// <summary>
/// Read-only view of the working tree under the content snapshot:
/// <c>git status --porcelain</c> lines plus the raw <c>git diff</c> text, both
/// scoped to the content dir. This record is where the pipeline ends — reviewing
/// and committing the diff is the operator's job, in their own shell.
/// </summary>
public sealed record SnapshotDiff(IReadOnlyList<string> StatusLines, string DiffText);

/// <summary>
/// Orchestrates the content-pull surface: run <c>pnpm content:pull</c>, inspect the
/// resulting diff, and verify the snapshot offline or against live Directus.
/// <para>
/// <b>Never writes.</b> There is no commit, push, stage, or mutation method here and
/// none may ever be added (design §2: the panel's only side effect is spawning the
/// same read/pull commands the operator runs by hand — the diff view ends at "commit
/// it yourself").
/// </para>
/// <para>
/// <b>Concurrency:</b> one global <see cref="SemaphoreSlim"/>(1,1) serializes the two
/// operations with side effects — <see cref="RunPullAsync"/> (mutates the working
/// tree via the pull) and <see cref="RunVerifyLiveAsync"/> (logs into Directus). A
/// trigger while either is running is rejected immediately with the in-flight run's
/// state; nothing ever queues. <see cref="GetDiffAsync"/> and
/// <see cref="RunVerifyOfflineAsync"/> are read-only (read-only git / in-process file
/// reads, no working-tree mutation, no Directus) and deliberately run outside the
/// lock so the UI can refresh the diff and offline findings while a run streams.
/// </para>
/// </summary>
public sealed class ContentPipelineService
{
    /// <summary><see cref="PipelineRun.Kind"/> of a content pull.</summary>
    public const string PullKind = "pull";

    /// <summary><see cref="PipelineRun.Kind"/> of a live verify.</summary>
    public const string VerifyLiveKind = "verify-live";

    /// <summary>Output buffer cap: beyond this many lines, oldest are dropped.</summary>
    public const int OutputCapacity = 2000;

    private readonly RepoPaths _paths;
    private readonly IProcessRunner _runner;
    private readonly SnapshotHealthService _snapshotHealth;
    private readonly Func<string[], TextWriter, TextWriter, int> _liveVerify;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentQueue<string> _output = new();
    private volatile PipelineRun? _current;

    /// <param name="liveVerify">
    /// Seam for the live verify only, so tests can assert the arg/lock plumbing
    /// without a reachable Directus. Null (production) means
    /// <see cref="CliRunner.Run(string[], TextWriter, TextWriter)"/> — exact CLI
    /// parity, zero duplication of the live-equivalence logic.
    /// </param>
    public ContentPipelineService(
        RepoPaths paths,
        IProcessRunner runner,
        SnapshotHealthService snapshotHealth,
        Func<string[], TextWriter, TextWriter, int>? liveVerify = null)
    {
        _paths = paths;
        _runner = runner;
        _snapshotHealth = snapshotHealth;
        _liveVerify = liveVerify ?? CliRunner.Run;
    }

    /// <summary>
    /// Raised on run start, on every buffered output line, and on run completion —
    /// the UI subscribes and re-renders. Fires on arbitrary background threads,
    /// potentially once per output line: subscribers must be cheap and marshal any
    /// UI work themselves (Blazor: <c>InvokeAsync(StateHasChanged)</c>). Subscriber
    /// exceptions are swallowed per subscriber: a throwing handler must never abort
    /// a run (the buffer feed is an <see cref="IProcessRunner"/> <c>onLine</c>
    /// callback, which must not throw) nor starve subscribers registered after it.
    /// </summary>
    public event Action? OutputChanged;

    /// <summary>The in-flight or most recently completed run; null before the first.</summary>
    public PipelineRun? CurrentRun => _current;

    /// <summary>Point-in-time copy of the output buffer, oldest line first.</summary>
    public IReadOnlyList<string> OutputSnapshot => _output.ToArray();

    /// <summary>
    /// Runs <c>pnpm content:pull</c> in the JS workspace (<see cref="RepoPaths.ClientDir"/>),
    /// streaming output into the buffer. Returns the completed run, or the in-flight
    /// run's state (<see cref="PipelineRun.Running"/> true) when rejected by the lock.
    /// </summary>
    public Task<PipelineRun> RunPullAsync(CancellationToken ct = default)
        => RunExclusiveAsync(
            PullKind,
            () => _runner.RunAsync("pnpm", ["content:pull"], _paths.ClientDir, AppendLine, ct));

    /// <summary>
    /// Runs <c>verify --live</c> through the API's own <see cref="CliRunner"/> (or the
    /// injected test seam) with absolute content/env paths — the CLI resolves env
    /// exactly as it does for the operator (exported vars win over the --env file).
    /// Output lands in the buffer when the call returns — including on an unexpected
    /// escape from the delegate, so partial diagnostics are never discarded — stderr
    /// prefixed <c>"! "</c>. Takes the global lock: it logs into Directus.
    /// <paramref name="ct"/> only gates the start: once
    /// <see cref="CliRunner.Run(string[], TextWriter, TextWriter)"/> begins it runs
    /// to completion (unlike the pull, whose token kills the process tree).
    /// </summary>
    public Task<PipelineRun> RunVerifyLiveAsync(CancellationToken ct = default)
        => RunExclusiveAsync(VerifyLiveKind, () => Task.Run(() =>
        {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            try
            {
                return _liveVerify(
                    ["verify", "--live", "--content", _paths.ContentDir, "--env", _paths.DirectusEnvPath],
                    stdout, stderr);
            }
            finally
            {
                AppendCaptured(stdout, prefix: null);
                AppendCaptured(stderr, prefix: "! ");
            }
        }, ct));

    /// <summary>
    /// Read-only view of the snapshot's working-tree state, workingDir = repo root,
    /// pathspec = absolute content dir (same convention as the freshness check).
    /// Runs outside the global lock — it mutates nothing, so it stays available
    /// while a pull streams. Stderr lines are filtered out of both fields; a failing
    /// git degrades to an empty diff, never a crash.
    /// </summary>
    public async Task<SnapshotDiff> GetDiffAsync(CancellationToken ct = default)
    {
        var statusLines = new List<string>();
        var diffLines = new List<string>();

        await _runner.RunAsync(
            "git", ["status", "--porcelain", "--", _paths.ContentDir],
            _paths.RepoRoot, statusLines.Add, ct).ConfigureAwait(false);
        await _runner.RunAsync(
            "git", ["diff", "--", _paths.ContentDir],
            _paths.RepoRoot, diffLines.Add, ct).ConfigureAwait(false);

        return new SnapshotDiff(
            statusLines.Where(IsStdoutLine).ToList(),
            string.Join('\n', diffLines.Where(IsStdoutLine)));
    }

    /// <summary>
    /// Offline invariant rules, in-process and hermetic — typed findings straight from
    /// <see cref="SnapshotHealthService"/>. No lock: it reads snapshot files only.
    /// </summary>
    public Task<OfflineVerifyResult> RunVerifyOfflineAsync(CancellationToken ct = default)
        => _snapshotHealth.RunOfflineVerifyAsync(ct);

    private async Task<PipelineRun> RunExclusiveAsync(string kind, Func<Task<int>> run)
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(false))
        {
            // Rejected, never queued: report what IS running. The null fallback covers
            // the sliver between lock acquisition and state publication in the winner.
            return _current is { Running: true } inFlight
                ? inFlight
                : new PipelineRun(kind, DateTimeOffset.UtcNow, Running: true, ExitCode: null);
        }

        try
        {
            _output.Clear(); // the pane shows the current run, not history
            var started = new PipelineRun(kind, DateTimeOffset.UtcNow, Running: true, ExitCode: null);
            _current = started;
            Notify();

            try
            {
                var exitCode = await run().ConfigureAwait(false);
                var completed = started with { Running = false, ExitCode = exitCode };
                _current = completed;
                return completed;
            }
            catch
            {
                // Spawn failure / cancellation: the run is over but produced no exit
                // code. Publish that before rethrowing so the UI never shows a
                // permanently "running" ghost.
                _current = started with { Running = false, ExitCode = null };
                throw;
            }
            finally
            {
                Notify();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void AppendLine(string line)
    {
        _output.Enqueue(line);
        while (_output.Count > OutputCapacity && _output.TryDequeue(out _))
        {
        }

        Notify();
    }

    private void AppendCaptured(StringWriter writer, string? prefix)
    {
        using var reader = new StringReader(writer.ToString());
        while (reader.ReadLine() is { } line)
        {
            AppendLine(prefix is null ? line : prefix + line);
        }
    }

    private void Notify()
    {
        if (OutputChanged is not { } handlers)
        {
            return;
        }

        // Per-subscriber isolation: a single try around the multicast invoke would
        // let one throwing subscriber (e.g. a disposed circuit from a closed browser
        // tab) starve every subscriber registered after it. Each Blazor circuit is
        // its own subscriber, so tab A's failure must not leave tab B stale.
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch
            {
                // A throwing subscriber must not abort the run: this fires from
                // inside IProcessRunner's onLine callback, which forbids throwing.
            }
        }
    }

    // IProcessRunner merges stderr into the line stream prefixed "! " — the diff
    // view only wants real stdout (a stray git warning would corrupt the diff text).
    private static bool IsStdoutLine(string line)
        => !line.StartsWith("! ", StringComparison.Ordinal);
}
