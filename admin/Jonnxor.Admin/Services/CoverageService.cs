using Jonnxor.Api.Report;
using Jonnxor.Api.Snapshot;

namespace Jonnxor.Admin.Services;

/// <summary>
/// Outcome of one coverage build: the API's own <see cref="CoverageReport"/> (same
/// numbers as the <c>report</c> CLI verb, by construction — same reader, same
/// reporter) plus the drill-down data the report record doesn't carry and a degrade
/// <see cref="Detail"/>. A missing content directory yields an empty report with
/// <see cref="Detail"/> explaining why, never a crash.
/// <para>
/// The result is self-contained: <see cref="MissingBySlug"/> answers from
/// <see cref="Missing"/>, computed from the same entries as <see cref="Report"/> —
/// a page holding this result gets drill-downs that always agree with the table it
/// rendered, no matter how many rebuilds other tabs trigger meanwhile.
/// </para>
/// </summary>
/// <param name="Missing">collection → locale → ordinal-ordered slugs lacking that locale.</param>
public sealed record CoverageResult(
    CoverageReport Report,
    string? Detail,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> Missing)
{
    /// <summary>
    /// The slugs in <paramref name="collection"/> lacking a (parseable) file for
    /// <paramref name="locale"/>. Unknown collection/locale → empty: a locale
    /// outside <see cref="SnapshotReader.Locales"/> is simply not a column this
    /// seam has, never "every slug missing".
    /// </summary>
    public IReadOnlyList<string> MissingBySlug(string collection, string locale)
        => Missing.TryGetValue(collection, out var byLocale)
           && byLocale.TryGetValue(locale, out var missing)
            ? missing
            : [];
}

/// <summary>
/// Coverage-page source: wraps <see cref="SnapshotReader"/> +
/// <see cref="CoverageReporter"/> in-process (design §5 — no CLI re-parse, no
/// Directus, read-only file I/O). Stateless: every build returns a self-contained
/// <see cref="CoverageResult"/>. Parse-error files follow
/// <see cref="CoverageReporter"/>'s convention: they never count as present, so a
/// slug whose only file for a locale fails to parse IS listed as missing.
/// </summary>
public sealed class CoverageService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> NoMissing
        = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>();

    private readonly RepoPaths _paths;

    public CoverageService(RepoPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// Reads the snapshot and builds the coverage report + drill-down map. File
    /// I/O, so it is pushed off the caller's (UI) thread via
    /// <see cref="Task.Run(Action)"/> — same convention as
    /// <see cref="SnapshotHealthService.RunOfflineVerifyAsync"/>.
    /// </summary>
    public Task<CoverageResult> BuildAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            List<SnapshotEntry> entries;
            try
            {
                entries = SnapshotReader.ReadAll(_paths.ContentDir).ToList();
            }
            catch (DirectoryNotFoundException ex)
            {
                return new CoverageResult(
                    new CoverageReport(DateTimeOffset.UtcNow, []), ex.Message, NoMissing);
            }

            return new CoverageResult(
                CoverageReporter.Build(entries, DateTimeOffset.UtcNow),
                null,
                BuildMissing(entries));
        }, ct);

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> BuildMissing(
        IEnumerable<SnapshotEntry> entries)
        => entries
            .Where(e => e.ParseError is null) // CoverageReporter's "usable" convention
            .GroupBy(e => e.Collection)
            .ToDictionary(
                collection => collection.Key,
                collection =>
                {
                    var localesBySlug = collection
                        .GroupBy(e => e.Slug)
                        .ToDictionary(
                            slug => slug.Key,
                            slug => slug.Select(e => e.Locale).ToHashSet(StringComparer.Ordinal));

                    return (IReadOnlyDictionary<string, IReadOnlyList<string>>)SnapshotReader.Locales
                        .ToDictionary(
                            locale => locale,
                            locale => (IReadOnlyList<string>)localesBySlug
                                .Where(kv => !kv.Value.Contains(locale))
                                .Select(kv => kv.Key)
                                .OrderBy(slug => slug, StringComparer.Ordinal)
                                .ToList());
                });
}
