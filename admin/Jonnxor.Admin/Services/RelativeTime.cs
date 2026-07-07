using System.Globalization;

namespace Jonnxor.Admin.Services;

/// <summary>
/// Invariant-culture relative-age formatting for the dashboard tiles
/// ("just now" / "5 min ago" / "3 h ago" / "2 d ago"). Deliberately simple:
/// locale/timezone preferences are a Config-page concern (Task 7), not this helper's.
/// </summary>
public static class RelativeTime
{
    /// <summary>
    /// Formats the age of <paramref name="then"/> relative to <paramref name="now"/>.
    /// A future <paramref name="then"/> (clock skew) clamps to "just now" rather
    /// than rendering a negative age.
    /// </summary>
    public static string Format(DateTimeOffset then, DateTimeOffset now)
    {
        var age = now - then;
        if (age < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return Unit((long)age.TotalMinutes, "min");
        }

        return age < TimeSpan.FromDays(1)
            ? Unit((long)age.TotalHours, "h")
            : Unit((long)age.TotalDays, "d");
    }

    private static string Unit(long amount, string unit)
        => string.Create(CultureInfo.InvariantCulture, $"{amount} {unit} ago");
}
