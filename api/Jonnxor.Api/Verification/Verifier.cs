using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Verification;

/// <summary>Runs every registered invariant rule against the full snapshot in one pass.</summary>
public static class Verifier
{
    public static readonly IReadOnlyList<IVerificationRule> Rules =
    [
        new EnBasePresentRule(),
        new NoCommentNodesRule(),
        new CountdownKindCoherenceRule(),
        new PagesSectionsPresentRule(),
    ];

    public static IReadOnlyList<Finding> Run(IReadOnlyList<SnapshotEntry> entries)
    {
        var findings = new List<Finding>();
        foreach (var rule in Rules)
        {
            findings.AddRange(rule.Check(entries));
        }

        return findings;
    }
}
