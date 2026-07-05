using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Verification;

/// <summary>
/// Mirrors the countdowns `superRefine` kind-coherence check in `client/src/content.config.ts`:
/// `kind: countdown` must not carry `icon`/`start`/`rate`; `kind: countup` must carry all
/// three and must not carry `when`/`gold`. This duplication is deliberate (design doc §
/// "plan self-review notes") — it is a validator invariant mirrored from the zod schema, not
/// a re-encoding of the field-level kind maps that `scripts/lib/collections.mjs` owns.
/// </summary>
public sealed class CountdownKindCoherenceRule : IVerificationRule
{
    private static readonly string[] CountupOnlyFields = ["icon", "start", "rate"];

    public IEnumerable<Finding> Check(IReadOnlyList<SnapshotEntry> entries)
    {
        foreach (var entry in entries.Where(e => e.Collection == "countdowns" && e.ParseError is null))
        {
            if (!entry.Fields.TryGetValue("kind", out var kindValue)
                || kindValue is not string kind
                || kind is not ("countdown" or "countup"))
            {
                yield return Violation(entry, "missing or unknown 'kind' (expected 'countdown' or 'countup')");
                continue;
            }

            if (kind == "countup")
            {
                foreach (var field in CountupOnlyFields)
                {
                    if (!HasValue(entry, field))
                    {
                        yield return Violation(entry, $"kind 'countup' requires '{field}' but it is absent");
                    }
                }

                if (HasValue(entry, "when"))
                {
                    yield return Violation(entry, "kind 'countup' must not carry 'when'");
                }

                // Relies on SnapshotReader's typed scalar resolution: `gold: true` arrives
                // as a bool, so `is true` matches. (With string-typed scalars this check
                // silently never fired — the review-caught false negative.)
                if (entry.Fields.TryGetValue("gold", out var goldValue) && goldValue is true)
                {
                    yield return Violation(entry, "kind 'countup' must not carry 'gold'");
                }
            }
            else if (kind == "countdown")
            {
                foreach (var field in CountupOnlyFields)
                {
                    if (HasValue(entry, field))
                    {
                        yield return Violation(entry, $"kind 'countdown' must not carry '{field}'");
                    }
                }
            }
        }
    }

    private static bool HasValue(SnapshotEntry entry, string field) =>
        entry.Fields.TryGetValue(field, out var value) && value is not null;

    private static Finding Violation(SnapshotEntry entry, string detail) =>
        new(nameof(CountdownKindCoherenceRule), entry.FilePath, $"{entry.Collection}/{entry.Slug}: {detail}");
}
