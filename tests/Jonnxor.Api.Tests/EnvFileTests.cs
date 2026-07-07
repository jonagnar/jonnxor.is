namespace Jonnxor.Api.Tests;

public class EnvFileTests
{
    [Fact]
    public void Parse_ReadsKeyValueLines()
    {
        var result = EnvFile.Parse("DIRECTUS_URL=http://localhost:8055\nADMIN_EMAIL=admin@jonnxor.is\n");

        Assert.Equal("http://localhost:8055", result["DIRECTUS_URL"]);
        Assert.Equal("admin@jonnxor.is", result["ADMIN_EMAIL"]);
    }

    [Fact]
    public void Parse_SkipsBlankLinesAndComments()
    {
        var result = EnvFile.Parse("""
            # Directus client env
            DIRECTUS_URL=http://localhost:8055

            # admin creds
            ADMIN_EMAIL=admin@jonnxor.is
            ADMIN_PASSWORD=hunter2
            """);

        Assert.Equal(3, result.Count);
        Assert.Equal("hunter2", result["ADMIN_PASSWORD"]);
    }

    [Fact]
    public void Parse_TrimsWhitespaceAroundKeyAndValue()
    {
        var result = EnvFile.Parse("  DIRECTUS_URL = http://localhost:8055  \n");

        Assert.Equal("http://localhost:8055", result["DIRECTUS_URL"]);
    }

    [Fact]
    public void Parse_HandlesCrLfLineEndings()
    {
        var result = EnvFile.Parse("DIRECTUS_URL=http://localhost:8055\r\nADMIN_EMAIL=admin@jonnxor.is\r\n");

        Assert.Equal("http://localhost:8055", result["DIRECTUS_URL"]);
        Assert.Equal("admin@jonnxor.is", result["ADMIN_EMAIL"]);
    }

    [Fact]
    public void Parse_LaterDuplicateKeyWins()
    {
        var result = EnvFile.Parse("ADMIN_EMAIL=first@jonnxor.is\nADMIN_EMAIL=second@jonnxor.is\n");

        Assert.Equal("second@jonnxor.is", result["ADMIN_EMAIL"]);
    }

    [Fact]
    public void Parse_IgnoresLinesWithoutEquals()
    {
        var result = EnvFile.Parse("this is not a valid line\nDIRECTUS_URL=http://localhost:8055\n");

        Assert.Single(result);
        Assert.Equal("http://localhost:8055", result["DIRECTUS_URL"]);
    }

    [Fact]
    public void Parse_ValueMayContainEqualsSign()
    {
        // e.g. a password or token that happens to contain '='.
        var result = EnvFile.Parse("ADMIN_PASSWORD=abc=def==\n");

        Assert.Equal("abc=def==", result["ADMIN_PASSWORD"]);
    }

    [Fact]
    public void Load_ReadsFromDisk()
    {
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, ".env");
        File.WriteAllText(path, "DIRECTUS_URL=http://localhost:8055\n");

        var result = EnvFile.Load(path);

        Assert.Equal("http://localhost:8055", result["DIRECTUS_URL"]);
    }
}
