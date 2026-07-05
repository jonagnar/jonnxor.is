using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Verification;

/// <summary>
/// Pages entries whose slug is one of the section-gated pages must carry non-empty
/// `sections` (`src/content/page-sections.ts` defines per-slug section shapes for these).
/// </summary>
public sealed class PagesSectionsPresentRule : IVerificationRule
{
    private static readonly HashSet<string> GatedSlugs = ["countdowns", "home", "about", "cv"];

    public IEnumerable<Finding> Check(IReadOnlyList<SnapshotEntry> entries)
    {
        foreach (var entry in entries.Where(e => e.Collection == "pages" && GatedSlugs.Contains(e.Slug)))
        {
            if (!IsNonEmptySections(entry))
            {
                yield return new Finding(
                    nameof(PagesSectionsPresentRule),
                    entry.FilePath,
                    $"pages/{entry.Slug} requires non-empty 'sections'");
            }
        }
    }

    private static bool IsNonEmptySections(SnapshotEntry entry)
    {
        if (!entry.Fields.TryGetValue("sections", out var sections) || sections is null)
        {
            return false;
        }

        return sections switch
        {
            Dictionary<object, object?> map => map.Count > 0,
            System.Collections.ICollection collection => collection.Count > 0,
            _ => true,
        };
    }
}
