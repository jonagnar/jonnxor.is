namespace Jonnxor.Api.Tests;

public class VerifyOfflineCliTests
{
    [Fact]
    public void Valid_ExitsZero_PrintsSummaryToStdout()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var root = FixturePath.For("valid");

        var exitCode = CliRunner.Run(["verify", "--offline", "--content", root], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("files", stdout.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Broken_ExitsOne_ListsFindingsToStderr()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var root = FixturePath.For("missing-en");

        var exitCode = CliRunner.Run(["verify", "--offline", "--content", root], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains(nameof(Jonnxor.Api.Verification.EnBasePresentRule), stderr.ToString());
        Assert.Contains("summer-solstice.is.yaml", stderr.ToString());
    }

    [Fact]
    public void MissingContentDir_ExitsTwo_ErrorsToStderr()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["verify", "--offline", "--content", "does/not/exist"], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("does/not/exist", stderr.ToString());
    }
}
