using Jonnxor.Api.Report;
using Jonnxor.Api.Snapshot;

namespace Jonnxor.Admin.Services;

/// <summary>
/// Outcome of one coverage build: the API's own <see cref="CoverageReport"/> (same
/// numbers as the <c>report</c> CLI verb, by construction — same reader, same
/// reporter) plus a degrade <see cref="Detail"/>. A missing content directory
/// yields an empty report with <see cref="Detail"/> explaining why, never a crash.
/// </summary>
public sealed record CoverageResult(CoverageReport Report, string? Detail);

/// <summary>
/// Coverage-page source: wraps <see cref="SnapshotReader"/> +
/// <see cref="CoverageReporter"/> in-process (design §5 — no CLI re-parse, no
/// Directus, read-only file I/O).
/// <para>
/// <b>Drill-down staleness contract:</b> <see cref="MissingBySlug"/> answers from a
/// cache populated by the LAST <see cref="BuildAsync"/> — deliberately, so the
/// drill-down always agrees with the table the page currently shows instead of
/// re-reading a possibly-changed snapshot mid-view. Before the first build (or
/// after a degraded one) it is empty. Parse-error files follow
/// <see cref="CoverageReporter"/>'s convention: they never count as present, so a
/// slug whose only file for a locale fails to parse IS listed as missing.
/// </para>
/// </summary>
public sealed class CoverageService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlySet<string>>> EmptyCache
        = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlySet<string>>>();

    private readonly RepoPaths _paths;

    // collection → slug → locales present; swapped atomically per build.
    private volatile IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlySet<string>>> _slugLocales
        = EmptyCache;

    public CoverageService(RepoPaths paths)
    {
        _paths = paths;
    }

    /// <summary>
    /// Reads the snapshot and builds the coverage report. File I/O, so it is pushed
    /// off the caller's (UI) thread via <see cref="Task.Run(Action)"/> — same
    /// convention as <see cref="SnapshotHealthService.RunOfflineVerifyAsync"/>.
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
                _slugLocales = EmptyCache;
                return new CoverageResult(
                    new CoverageReport(DateTimeOffset.UtcNow, []), ex.Message);
            }

            var report = CoverageReporter.Build(entries, DateTimeOffset.UtcNow);
            _slugLocales = BuildCache(entries);
            return new CoverageResult(report, null);
        }, ct);

    /// <summary>
    /// The slugs in <paramref name="collection"/> lacking a (parseable) file for
    /// <paramref name="locale"/>, ordinal-ordered — the drill-down data the report
    /// record itself doesn't carry. Unknown collection/locale → empty: a locale
    /// outside <see cref="SnapshotReader.Locales"/> must not report "every slug
    /// missing", it is simply not a column this seam has.
    /// </summary>
    public IReadOnlyList<string> MissingBySlug(string collection, string locale)
        => SnapshotReader.Locales.Contains(locale)
           && _slugLocales.TryGetValue(collection, out var bySlug)
            ? bySlug
                .Where(kv => !kv.Value.Contains(locale))
                .Select(kv => kv.Key)
                .OrderBy(slug => slug, StringComparer.Ordinal)
                .ToList()
            : [];

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlySet<string>>> BuildCache(
        IEnumerable<SnapshotEntry> entries)
        => entries
            .Where(e => e.ParseError is null) // CoverageReporter's "usable" convention
            .GroupBy(e => e.Collection)
            .ToDictionary(
                collection => collection.Key,
                collection => (IReadOnlyDictionary<string, IReadOnlySet<string>>)collection
                    .GroupBy(e => e.Slug)
                    .ToDictionary(
                        slug => slug.Key,
                        slug => (IReadOnlySet<string>)slug
                            .Select(e => e.Locale)
                            .ToHashSet(StringComparer.Ordinal)));
}
