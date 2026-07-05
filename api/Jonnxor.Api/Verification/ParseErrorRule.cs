using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Verification;

/// <summary>
/// Surfaces per-file parse failures (malformed YAML, missing/unterminated blog frontmatter
/// fences) as ordinary findings so a broken file fails the run with exit 1 and a named file,
/// rather than crashing the process with an unhandled exception. Runs first; the other rules
/// skip entries carrying a <see cref="SnapshotEntry.ParseError"/> to avoid piling secondary
/// findings onto a file that simply didn't parse.
/// </summary>
public sealed class ParseErrorRule : IVerificationRule
{
    public IEnumerable<Finding> Check(IReadOnlyList<SnapshotEntry> entries)
    {
        foreach (var entry in entries.Where(e => e.ParseError is not null))
        {
            yield return new Finding(
                nameof(ParseErrorRule),
                entry.FilePath,
                $"{entry.Collection}/{entry.Slug}.{entry.Locale}: {entry.ParseError}");
        }
    }
}
