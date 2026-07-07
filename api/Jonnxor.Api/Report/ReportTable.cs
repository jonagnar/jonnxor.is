using System.Globalization;
using Jonnxor.Api.Snapshot;

namespace Jonnxor.Api.Report;

/// <summary>Renders a <see cref="CoverageReport"/> as a human-readable console table, one row
/// per collection plus a totals row, one column per locale (order per
/// <see cref="SnapshotReader.Locales"/>).</summary>
public static class ReportTable
{
    public static void Write(CoverageReport report, TextWriter stdout)
    {
        var locales = SnapshotReader.Locales;

        stdout.WriteLine("report: translation coverage (offline, from the snapshot)");
        stdout.WriteLine($"  generated at: {report.GeneratedAt:O}");
        stdout.WriteLine();

        var collectionWidth = Math.Max("collection".Length, report.Collections.Select(c => c.Name.Length).DefaultIfEmpty(0).Max());
        var slugsWidth = "slugs".Length;

        var header = $"{"collection".PadRight(collectionWidth)}  {"slugs".PadRight(slugsWidth)}  " +
                     string.Join("  ", locales.Select(l => l.PadRight(LocaleColumnWidth)));
        stdout.WriteLine(header);
        stdout.WriteLine(new string('-', header.Length));

        var totalSlugs = 0;
        var totalPresent = locales.ToDictionary(l => l, _ => 0);

        foreach (var collection in report.Collections)
        {
            totalSlugs += collection.Slugs;

            var cells = locales.Select(locale =>
            {
                var lc = collection.Locales[locale];
                totalPresent[locale] += lc.Present;
                return FormatCell(lc);
            });

            stdout.WriteLine(
                $"{collection.Name.PadRight(collectionWidth)}  {collection.Slugs.ToString().PadRight(slugsWidth)}  " +
                string.Join("  ", cells));
        }

        stdout.WriteLine(new string('-', header.Length));

        var totalCells = locales.Select(locale =>
        {
            var present = totalPresent[locale];
            var coverage = totalSlugs == 0 ? 0.0 : (double)present / totalSlugs;
            return FormatCell(new LocaleCoverage(present, coverage));
        });
        stdout.WriteLine(
            $"{"TOTAL".PadRight(collectionWidth)}  {totalSlugs.ToString().PadRight(slugsWidth)}  " +
            string.Join("  ", totalCells));
    }

    private const int LocaleColumnWidth = 14; // "999 (100%)"

    /// <summary>
    /// The one percent formatter for coverage fractions (0.0–1.0 → "0"–"100", no sign).
    /// Formatted manually (rather than "{0:P0}") so output is stable across the invariant
    /// vs. system culture's differing "before the % sign" conventions. Public because the
    /// admin panel's coverage page renders through it too — one definition, so the page
    /// can never disagree with this table's rounding.
    /// </summary>
    public static string FormatPercent(double coverage) =>
        Math.Round(coverage * 100, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);

    private static string FormatCell(LocaleCoverage locale) =>
        $"{locale.Present} ({FormatPercent(locale.Coverage)}%)".PadRight(LocaleColumnWidth);
}
