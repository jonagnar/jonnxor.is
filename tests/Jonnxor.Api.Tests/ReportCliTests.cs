using System.Text.Json;

namespace Jonnxor.Api.Tests;

public class ReportCliTests
{
    [Fact]
    public void Report_ValidFixture_ExitsZero_PrintsTableToStdout()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var root = FixturePath.For("valid");

        var exitCode = CliRunner.Run(["report", "--content", root], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Empty(stderr.ToString());
        Assert.Contains("games", stdout.ToString());
        Assert.Contains("en", stdout.ToString());
        Assert.Contains("TOTAL", stdout.ToString());
    }

    [Fact]
    public void Report_WithJsonOption_WritesArtifactToDisk()
    {
        using var temp = new TempDir();
        var jsonPath = Path.Combine(temp.Path, "coverage.json");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var root = FixturePath.For("valid");

        var exitCode = CliRunner.Run(["report", "--content", root, "--json", jsonPath], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(jsonPath));

        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
        Assert.True(doc.RootElement.TryGetProperty("generatedAt", out _));
        Assert.True(doc.RootElement.TryGetProperty("collections", out var collections));
        Assert.True(collections.GetArrayLength() > 0);

        Assert.Contains("json artifact written", stdout.ToString());
    }

    [Fact]
    public void Report_MissingContentDir_ExitsTwo_ErrorsToStderr()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = CliRunner.Run(["report", "--content", "does/not/exist"], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("does/not/exist", stderr.ToString());
    }

    [Fact]
    public void Report_DoesNotRequireOfflineRulesToPass()
    {
        // report is purely descriptive (unlike verify) — it should still emit a table over
        // a fixture that would fail verify --offline's EnBasePresentRule, since the report's
        // whole point is to surface exactly that kind of gap as a number, not gate on it.
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var root = FixturePath.For("missing-en");

        var exitCode = CliRunner.Run(["report", "--content", root], stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.Contains("countdowns", stdout.ToString());
    }
}
