using Jonnxor.Admin.Services;

namespace Jonnxor.Admin.Tests;

public class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-07T12:00:00+00:00");

    [Theory]
    [InlineData("2026-07-07T12:00:00+00:00", "just now")] // zero age
    [InlineData("2026-07-07T11:59:01+00:00", "just now")] // under a minute
    [InlineData("2026-07-07T11:59:00+00:00", "1 min ago")] // exactly one minute
    [InlineData("2026-07-07T11:15:00+00:00", "45 min ago")]
    [InlineData("2026-07-07T11:00:00+00:00", "1 h ago")] // exactly one hour
    [InlineData("2026-07-07T09:30:00+00:00", "2 h ago")] // floors, never rounds up
    [InlineData("2026-07-06T12:00:00+00:00", "1 d ago")] // exactly one day
    [InlineData("2026-06-30T06:00:00+00:00", "7 d ago")]
    public void Format_KnownAges_FormatsInvariant(string then, string expected)
        => Assert.Equal(expected, RelativeTime.Format(DateTimeOffset.Parse(then), Now));

    [Fact]
    public void Format_FutureTimestamp_ClampsToJustNow()
        => Assert.Equal("just now", RelativeTime.Format(Now.AddMinutes(5), Now));

    [Fact]
    public void Format_RespectsOffsetsNotWallClock()
    {
        // Same instant expressed in a different offset must not change the age.
        var then = DateTimeOffset.Parse("2026-07-07T09:00:00+00:00");
        var sameInstantOtherOffset = then.ToOffset(TimeSpan.FromHours(9));
        Assert.Equal("3 h ago", RelativeTime.Format(sameInstantOtherOffset, Now));
    }
}
