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
    public void Verify_Live_WithoutCredentials_ExitsTwo_ErrorsToStderr()
    {
        // No --env given and (in a sane test environment) no DIRECTUS_URL/ADMIN_EMAIL/
        // ADMIN_PASSWORD exported — the live verb must fail fast with a clear message
        // rather than attempting a request with empty credentials.
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var root = FixturePath.For("valid");

        var exitCode = CliRunner.Run(["verify", "--live", "--content", root], stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("DIRECTUS_URL", stderr.ToString());
    }

    [Fact]
    public void Verify_Live_OfflineFailureFailsFast_NeverAttemptsDirectus()
    {
        // Broken snapshot content must fail on the offline pass before verify --live ever
        // tries to reach Directus — proven here by pointing at a fixture with an offline
        // rule violation and no Directus credentials at all: if it tried to connect first,
        // the failure message would be about DIRECTUS_URL, not the offline finding.
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var root = FixturePath.For("missing-en");

        var exitCode = CliRunner.Run(["verify", "--live", "--content", root], stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains(nameof(Jonnxor.Api.Verification.EnBasePresentRule), stderr.ToString());
    }

    [Fact]
    public void Verify_Live_ExportedEnvVarWinsOverEnvFile()
    {
        // Resolve() checks Environment.GetEnvironmentVariable before the --env file — an
        // explicitly exported var must win even when a --env file defines the same key
        // differently. Proven here via a malformed exported DIRECTUS_URL: if the file's
        // (well-formed but unreachable) value won the race, this would fail on a network
        // attempt/timeout instead of the immediate ctor-validation error.
        using var envFileDir = new TempDir();
        var envPath = System.IO.Path.Combine(envFileDir.Path, ".env");
        File.WriteAllText(envPath, """
            DIRECTUS_URL=http://localhost:8055
            ADMIN_EMAIL=admin@jonnxor.is
            ADMIN_PASSWORD=hunter2
            """);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var root = FixturePath.For("valid");

        // Save/restore the true prior value (rather than hard-resetting to null) so this
        // test can't clobber a genuinely-exported DIRECTUS_URL in whatever environment
        // it runs in.
        var previousValue = Environment.GetEnvironmentVariable("DIRECTUS_URL");
        Environment.SetEnvironmentVariable("DIRECTUS_URL", "not-a-valid-url");
        try
        {
            var exitCode = CliRunner.Run(
                ["verify", "--live", "--content", root, "--env", envPath], stdout, stderr);

            Assert.Equal(2, exitCode);
            Assert.Contains("not-a-valid-url", stderr.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DIRECTUS_URL", previousValue);
        }
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
