using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Verification;

/// <summary>
/// Every (collection, slug) must have an `en` locale file — English is the authoring base
/// and load-bearing fallback (CLAUDE.md i18n section). A slug with only e.g. `.is.yaml` and
/// no `.en.yaml` breaks the fallback chain.
/// </summary>
public sealed class EnBasePresentRule : IVerificationRule
{
    public IEnumerable<Finding> Check(IReadOnlyList<SnapshotEntry> entries)
    {
        var bySlug = entries.GroupBy(e => (e.Collection, e.Slug));

        foreach (var group in bySlug)
        {
            if (group.Any(e => e.Locale == "en"))
            {
                continue;
            }

            // Report against the first (deterministically ordered) locale file found for
            // this slug, since there is no en file to point at.
            var representative = group.OrderBy(e => e.Locale, StringComparer.Ordinal).First();
            yield return new Finding(
                nameof(EnBasePresentRule),
                representative.FilePath,
                $"{representative.Collection}/{representative.Slug} has no 'en' locale file");
        }
    }
}
