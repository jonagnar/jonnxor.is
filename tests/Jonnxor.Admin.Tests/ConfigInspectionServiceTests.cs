using Jonnxor.Admin.Services;

namespace Jonnxor.Admin.Tests;

public class ConfigInspectionServiceTests
{
    /// <summary>The exact worktree caveat wording the plan mandates (shared with the Directus tile).</summary>
    private const string WorktreeCaveat =
        "direnv does not reach worktrees — export DIRECTUS_URL or ensure directus/.env exists "
        + "(decrypted via sops/direnv from the main checkout)";

    private static string? NoEnv(string _) => null;

    private static ConfigInspectionService ServiceFor(
        TempDir temp, Func<string, string?>? getEnv = null, AdminOptions? options = null)
    {
        options ??= new AdminOptions { RepoRoot = temp.Path };
        return new ConfigInspectionService(options, new RepoPaths(options), getEnv ?? NoEnv);
    }

    [Fact]
    public void Inspect_DirectusUrlFromEnvFile_ValueAndEnvFileSource()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "DIRECTUS_URL=http://localhost:8055\n");

        var config = ServiceFor(temp).Inspect();

        Assert.Equal("http://localhost:8055", config.DirectusUrl.Url);
        Assert.Equal(DirectusUrlSource.EnvFile, config.DirectusUrl.Source);
        Assert.Null(config.DirectusUrl.Problem);
        Assert.True(config.DirectusEnvFileExists);
        Assert.Null(config.EnvFileProblem);
    }

    [Fact]
    public void Inspect_ExportedDirectusUrl_WinsOverEnvFile()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "DIRECTUS_URL=http://from-file:9999\n");

        var config = ServiceFor(
            temp, key => key == "DIRECTUS_URL" ? "http://from-env:8055" : null).Inspect();

        Assert.Equal("http://from-env:8055", config.DirectusUrl.Url);
        Assert.Equal(DirectusUrlSource.EnvironmentVariable, config.DirectusUrl.Source);
    }

    [Fact]
    public void Inspect_MissingEnvFile_UnresolvedUrlNotSetSecretsAndCaveat()
    {
        using var temp = new TempDir(); // no directus/.env

        var config = ServiceFor(temp).Inspect();

        Assert.False(config.DirectusEnvFileExists);
        Assert.Null(config.DirectusUrl.Url);
        Assert.Equal(DirectusUrlSource.Unresolved, config.DirectusUrl.Source);
        Assert.Contains(WorktreeCaveat, config.DirectusUrl.Problem);
        Assert.Contains(WorktreeCaveat, config.EnvFileProblem);
        Assert.All(config.Secrets, s => Assert.False(s.IsSet));
        Assert.All(config.Secrets, s => Assert.Equal(SecretSource.NotSet, s.Source));
    }

    [Fact]
    public void Inspect_FileSecrets_SetWithEnvFileSource()
    {
        using var temp = new TempDir();
        temp.WriteFile(
            "directus/.env",
            "DIRECTUS_URL=http://localhost:8055\nADMIN_EMAIL=a@b.c\nADMIN_PASSWORD=pw\n");

        var config = ServiceFor(temp).Inspect();

        var email = Assert.Single(config.Secrets, s => s.Name == "ADMIN_EMAIL");
        var password = Assert.Single(config.Secrets, s => s.Name == "ADMIN_PASSWORD");
        Assert.True(email.IsSet);
        Assert.Equal(SecretSource.EnvFile, email.Source);
        Assert.True(password.IsSet);
        Assert.Equal(SecretSource.EnvFile, password.Source);
    }

    [Fact]
    public void Inspect_ExportedSecret_WinsOverEnvFile_CliRunnerPrecedence()
    {
        using var temp = new TempDir();
        temp.WriteFile("directus/.env", "ADMIN_EMAIL=file@b.c\n");

        var config = ServiceFor(
            temp, key => key == "ADMIN_EMAIL" ? "env@b.c" : null).Inspect();

        var email = Assert.Single(config.Secrets, s => s.Name == "ADMIN_EMAIL");
        Assert.True(email.IsSet);
        Assert.Equal(SecretSource.EnvironmentVariable, email.Source);
    }

    [Fact]
    public void Inspect_ForgejoToken_ReadsTheEnvVarOnly_NeverTheFile()
    {
        using var temp = new TempDir();
        // A FORGEJO_TOKEN line in the env file must NOT count — the token is env-var-only.
        temp.WriteFile("directus/.env", "FORGEJO_TOKEN=file-token\n");

        var withoutVar = ServiceFor(temp).Inspect();
        var token = Assert.Single(withoutVar.Secrets, s => s.Name == "FORGEJO_TOKEN");
        Assert.False(token.IsSet);
        Assert.Equal(SecretSource.NotSet, token.Source);

        var withVar = ServiceFor(temp, key => key == "FORGEJO_TOKEN" ? "tok" : null).Inspect();
        token = Assert.Single(withVar.Secrets, s => s.Name == "FORGEJO_TOKEN");
        Assert.True(token.IsSet);
        Assert.Equal(SecretSource.EnvironmentVariable, token.Source);
    }

    [Fact]
    public void Inspect_EnvVarOnlyFlag_MarksTheTokenAndOnlyTheToken()
    {
        using var temp = new TempDir();

        var config = ServiceFor(temp).Inspect();

        // The per-key sourcing policy travels on the badge, so the page renders it
        // without any key-name knowledge of its own.
        Assert.All(config.Secrets, s =>
            Assert.Equal(s.Name == "FORGEJO_TOKEN", s.EnvVarOnly));
    }

    [Fact]
    public void Inspect_SecretBadges_CarryNoValueAnywhere()
    {
        // Structural guarantee for "values never render": the badge type has no
        // property that could even HOLD a secret value — only the key name.
        var stringProperties = typeof(SecretBadge).GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToList();
        Assert.Equal(new[] { "Name" }, stringProperties);
    }

    [Fact]
    public void Inspect_ForgejoDefaults_FlaggedAsAppsettingsDefaults()
    {
        using var temp = new TempDir();

        var config = ServiceFor(temp).Inspect();

        Assert.True(config.ForgejoBaseUrlIsDefault);
        Assert.True(config.ForgejoRepoIsDefault);
    }

    [Fact]
    public void Inspect_ForgejoOverrides_FlaggedAsOverrides()
    {
        using var temp = new TempDir();
        var options = new AdminOptions
        {
            RepoRoot = temp.Path,
            ForgejoBaseUrl = "http://forgejo.example:3333",
            ForgejoRepo = "someone/else",
        };

        var config = ServiceFor(temp, options: options).Inspect();

        Assert.False(config.ForgejoBaseUrlIsDefault);
        Assert.False(config.ForgejoRepoIsDefault);
        Assert.Equal("http://forgejo.example:3333", config.ForgejoBaseUrl);
        Assert.Equal("someone/else", config.ForgejoRepo);
    }

    [Fact]
    public void Inspect_ExplicitRepoRoot_Flagged()
    {
        using var temp = new TempDir();

        var config = ServiceFor(temp).Inspect();

        Assert.True(config.RepoRootIsExplicit);
        Assert.Equal(Path.GetFullPath(temp.Path), config.RepoRoot);
    }

    [Fact]
    public void Inspect_WalkedUpRepoRoot_FlaggedAsWalkedUp()
    {
        using var temp = new TempDir();
        temp.WriteFile("jonnxor.sln", "");
        var nested = temp.CreateDir("admin", "bin");
        var options = new AdminOptions(); // no explicit RepoRoot
        var paths = new RepoPaths(options, startDirectory: nested);

        var config = new ConfigInspectionService(options, paths, NoEnv).Inspect();

        Assert.False(config.RepoRootIsExplicit);
        Assert.Equal(paths.RepoRoot, config.RepoRoot);
    }
}
