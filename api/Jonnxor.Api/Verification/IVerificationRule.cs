using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Verification;

/// <summary>
/// A single snapshot invariant. Each rule sees every entry in the snapshot in one pass —
/// rules that reason across slugs (e.g. "every slug has an en file") need the full set, not
/// just one entry at a time.
/// </summary>
public interface IVerificationRule
{
    IEnumerable<Finding> Check(IReadOnlyList<SnapshotEntry> entries);
}
