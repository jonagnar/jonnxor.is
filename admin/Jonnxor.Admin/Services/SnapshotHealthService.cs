using System.Globalization;
using Jonnxor.Api.Snapshot;
using Jonnxor.Api.Verification;

namespace Jonnxor.Admin.Services;

/// <summary>
/// Freshness of the committed content snapshot. The Directus schema has no
/// <c>date_updated</c> on any collection, so freshness is commit-age plus working-tree
/// dirtiness only: <see cref="LastCommit"/> is the newest commit touching the content
/// dir (null when git fails or no commit touches it), <see cref="Dirty"/> flags
/// uncommitted pull output, <see cref="FileCount"/> counts the changed paths.
/// </summary>
public sealed record SnapshotFreshness(DateTimeOffset? LastCommit, bool Dirty, int FileCount);

/// <summary>
/// Typed outcome of an in-process offline verify: the API's own findings plus the
/// number of snapshot files scanned.
/// </summary>
public sealed record OfflineVerifyResult(IReadOnlyList<Finding> Findings, int FileCount);

/// <summary>
/// Dashboard/pipeline source for snapshot health. Git access is read-only
/// (<c>log</c>/<c>status</c>) through <see cref="IProcessRunner"/>; verification runs
/// the API's <see cref="SnapshotReader"/> + <see cref="Verifier"/> in-process — typed
/// findings, no CLI output re-parse, and no write path anywhere.
/// </summary>
public sealed class SnapshotHealthService
{
    /// <summary>Rule name of the synthetic finding for a missing content directory.</summary>
    public const string ContentDirMissingRule = "ContentDirMissing";

    private readonly RepoPaths _paths;
    private readonly IProcessRunner _runner;

    public SnapshotHealthService(RepoPaths paths, IProcessRunner runner)
    {
        _paths = paths;
        _runner = runner;
    }

    public async Task<SnapshotFreshness> GetFreshnessAsync(CancellationToken ct = default)
    {
        var logLines = new List<string>();
        var logExit = await _runner.RunAsync(
            "git", ["log", "-1", "--format=%cI", "--", _paths.ContentDir],
            _paths.RepoRoot, logLines.Add, ct).ConfigureAwait(false);

        DateTimeOffset? lastCommit = null;
        if (logExit == 0
            && FirstStdoutLine(logLines) is { } stamp
            && DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            lastCommit = parsed;
        }

        var statusLines = new List<string>();
        var statusExit = await _runner.RunAsync(
            "git", ["status", "--porcelain", "--", _paths.ContentDir],
            _paths.RepoRoot, statusLines.Add, ct).ConfigureAwait(false);

        var changedCount = statusExit == 0 ? statusLines.Count(IsStdoutLine) : 0;

        return new SnapshotFreshness(lastCommit, changedCount > 0, changedCount);
    }

    /// <summary>
    /// Runs the offline invariant rules against the snapshot in-process. File I/O, so
    /// it is pushed off the caller's (UI) thread via <see cref="Task.Run(Action)"/>.
    /// A missing content directory degrades to a single synthetic finding, never a crash.
    /// </summary>
    public Task<OfflineVerifyResult> RunOfflineVerifyAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            try
            {
                var entries = SnapshotReader.ReadAll(_paths.ContentDir).ToList();
                return new OfflineVerifyResult(Verifier.Run(entries), entries.Count);
            }
            catch (DirectoryNotFoundException ex)
            {
                return new OfflineVerifyResult(
                    [new Finding(ContentDirMissingRule, _paths.ContentDir, ex.Message)], 0);
            }
        }, ct);

    // IProcessRunner merges stderr into the line stream prefixed "! " — freshness
    // parsing only wants real stdout.
    private static bool IsStdoutLine(string line)
        => line.Length > 0 && !line.StartsWith("! ", StringComparison.Ordinal);

    private static string? FirstStdoutLine(List<string> lines) => lines.Find(IsStdoutLine);
}
