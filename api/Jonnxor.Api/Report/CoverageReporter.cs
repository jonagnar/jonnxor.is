using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Report;

/// <summary>
/// Computes per-collection, per-locale translation coverage from the snapshot alone
/// (offline — no Directus). "Coverage" here means: does this slug have a REAL locale file for
/// this locale, or would the site's `fallbackType: 'rewrite'` (en/ja rewrite to is) i18n
/// serve it English/Icelandic content instead at runtime? English is the authoring base
/// (CLAUDE.md i18n section), so `en` coverage is expected to read 100% across the board —
/// this report exists to make the OTHER locales' real gaps visible instead of silently
/// papered over by the fallback.
///
/// Reuses <see cref="SnapshotReader.Locales"/> for the locale set (never re-derives it) and
/// excludes <see cref="SnapshotEntry.ParseError"/> entries from every count, matching
/// <see cref="Jonnxor.Api.Verification.Verifier"/>'s convention — a file that failed to parse
/// is neither present-and-valid nor a real gap, so the offline verifier and the report
/// agree on what counts as "an actual slug/locale."
/// </summary>
public static class CoverageReporter
{
    public static CoverageReport Build(IReadOnlyList<SnapshotEntry> entries, DateTimeOffset generatedAt)
    {
        var usable = entries.Where(e => e.ParseError is null).ToList();

        var collections = usable
            .GroupBy(e => e.Collection)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(BuildCollection)
            .ToList();

        return new CoverageReport(generatedAt, collections);
    }

    private static CollectionCoverage BuildCollection(IGrouping<string, SnapshotEntry> group)
    {
        var bySlug = group
            .GroupBy(e => e.Slug)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Locale).ToHashSet(StringComparer.Ordinal));

        var slugCount = bySlug.Count;

        var locales = SnapshotReader.Locales.ToDictionary(
            locale => locale,
            locale =>
            {
                var present = bySlug.Values.Count(localesForSlug => localesForSlug.Contains(locale));
                var coverage = slugCount == 0 ? 0.0 : (double)present / slugCount;
                return new LocaleCoverage(present, coverage);
            });

        return new CollectionCoverage(group.Key, slugCount, locales);
    }
}

/// <summary>The full report — matches the `--json` artifact schema exactly (property names are
/// camelCase on serialize via <see cref="Jonnxor.Api.Report.ReportJson"/>'s naming policy).</summary>
public sealed record CoverageReport(DateTimeOffset GeneratedAt, IReadOnlyList<CollectionCoverage> Collections);

public sealed record CollectionCoverage(string Name, int Slugs, IReadOnlyDictionary<string, LocaleCoverage> Locales);

/// <summary>Coverage for one (collection, locale) pair: how many of the collection's slugs have
/// a real file for this locale, and that count as a fraction of the collection's total slugs
/// (1.0 == every slug has this locale; 0.0 == none do — always 1.0 for `en` in a healthy
/// snapshot since <c>EnBasePresentRule</c> gates that offline).</summary>
public sealed record LocaleCoverage(int Present, double Coverage);
