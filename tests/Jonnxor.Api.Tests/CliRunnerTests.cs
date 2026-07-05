namespace Jonnxor.Api.Tests;

public class CliRunnerTests
{
    [Fact]
    public void NoArgs_PrintsUsage_ExitsZero()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run([], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", stdout.ToString());
        Assert.Empty(stderr.ToString());
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_PrintsUsage_ExitsZero(string flag)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run([flag], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", stdout.ToString());
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Verify_Live_RoutesToVerify_NotImplementedYet()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["verify", "--live", "--content", "some/dir"], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Empty(stdout.ToString());
        Assert.Contains("not implemented", stderr.ToString());
    }

    [Fact]
    public void Report_RoutesToReport_NotImplementedYet()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["report", "--json", "out.json"], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Empty(stdout.ToString());
        Assert.Contains("not implemented", stderr.ToString());
    }

    [Fact]
    public void UnknownVerb_ExitsSixtyFour()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["frobnicate"], stdout, stderr);

        Assert.Equal(64, exitCode);
        Assert.Empty(stdout.ToString());
        Assert.Contains("Unknown verb: 'frobnicate'", stderr.ToString());
    }
}
