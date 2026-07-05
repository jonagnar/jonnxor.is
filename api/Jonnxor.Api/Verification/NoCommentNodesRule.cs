using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Verification;

/// <summary>
/// No YAML comment tokens anywhere in any document/frontmatter. This catches the real bug
/// class where an unquoted mid-scalar ` # comment` silently truncates the value before it
/// (e.g. `what: Jol # trailing comment` parses as `what: Jol`, discarding the tail without
/// any parse error) — see `tests/content/snapshot-comments.test.ts` on the Node side, which
/// guards the same invariant for the JS snapshot pipeline.
/// </summary>
public sealed class NoCommentNodesRule : IVerificationRule
{
    public IEnumerable<Finding> Check(IReadOnlyList<SnapshotEntry> entries)
    {
        // Entries with a parse error are reported by ParseErrorRule; their raw text may not
        // even be YAML-shaped (e.g. fence-less markdown), so scanning it would be noise.
        foreach (var entry in entries.Where(e => e.ParseError is null))
        {
            if (YamlCommentScanner.ContainsComment(entry.RawFrontmatter))
            {
                yield return new Finding(
                    nameof(NoCommentNodesRule),
                    entry.FilePath,
                    $"{entry.Collection}/{entry.Slug}.{entry.Locale} contains a YAML comment token");
            }
        }
    }
}
