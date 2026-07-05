using Jonnxor.Api;

namespace Jonnxor.Api.Tests;

public class CliRunnerTests
{
    [Fact]
    public void NoArgs_PrintsUsage_ExitsZero()
    {
        var exitCode = CliRunner.Run([]);

        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_PrintsUsage_ExitsZero(string flag)
    {
        var exitCode = CliRunner.Run([flag]);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Verify_RoutesToVerify_NotImplementedYet()
    {
        var exitCode = CliRunner.Run(["verify", "--offline"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void Verify_Live_RoutesToVerify_NotImplementedYet()
    {
        var exitCode = CliRunner.Run(["verify", "--live", "--content", "some/dir"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void Report_RoutesToReport_NotImplementedYet()
    {
        var exitCode = CliRunner.Run(["report", "--json", "out.json"]);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void UnknownVerb_ExitsSixtyFour()
    {
        var exitCode = CliRunner.Run(["frobnicate"]);

        Assert.Equal(64, exitCode);
    }
}
