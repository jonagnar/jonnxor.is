using Jonnxor.Api;

namespace Jonnxor.Admin.Services;

/// <summary>Which source set a secret — or nothing did.</summary>
public enum SecretSource
{
    NotSet,
    EnvironmentVariable,
    EnvFile,
}

/// <summary>
/// Presence-only view of one secret. The point of this type is what it LACKS: no
/// value, no length, no prefix — only the key name and whether/where it is set, so
/// nothing a renderer touches can ever leak the secret itself (design §2, asserted
/// structurally in ConfigInspectionServiceTests and against HTML in RenderTests).
/// <see cref="EnvVarOnly"/> carries the per-key sourcing policy (FORGEJO_TOKEN is
/// never read from a file) as a bool, so the page renders policy without knowing
/// key names — and the "string properties = Name only" guarantee still holds.
/// </summary>
public sealed record SecretBadge(string Name, bool IsSet, SecretSource Source, bool EnvVarOnly);

/// <summary>The Config page's read-only model — everything on it is runtime state.</summary>
public sealed record EffectiveConfig(
    string RepoRoot,
    bool RepoRootIsExplicit,
    string ContentDir,
    string DirectusEnvPath,
    bool DirectusEnvFileExists,
    DirectusUrlResolution DirectusUrl,
    string ForgejoBaseUrl,
    bool ForgejoBaseUrlIsDefault,
    string ForgejoRepo,
    bool ForgejoRepoIsDefault,
    IReadOnlyList<SecretBadge> Secrets,
    string? EnvFileProblem);

/// <summary>
/// Builds the effective-configuration view for the Config page: resolved paths (with
/// how the repo root was resolved), endpoint values with the source that won, and
/// secret PRESENCE badges. Secret precedence mirrors the API CLI (<c>CliRunner</c>):
/// an exported variable wins over the env file — except <c>FORGEJO_TOKEN</c>, which
/// is env-var-only by design (it must never live in a file in the repo). Read-only
/// by construction: reads config, <see cref="File.Exists(string)"/> and the env
/// file; writes nothing.
/// </summary>
public sealed class ConfigInspectionService
{
    /// <summary>Fresh options = the appsettings defaults, for default-vs-override labeling.</summary>
    private static readonly AdminOptions Defaults = new();

    private readonly AdminOptions _options;
    private readonly RepoPaths _paths;
    private readonly Func<string, string?> _getEnv;

    /// <param name="options">Bound panel configuration.</param>
    /// <param name="paths">Resolved repo paths.</param>
    /// <param name="getEnv">Injectable env lookup for tests; null means the process environment.</param>
    public ConfigInspectionService(
        AdminOptions options, RepoPaths paths, Func<string, string?>? getEnv = null)
    {
        _options = options;
        _paths = paths;
        _getEnv = getEnv ?? Environment.GetEnvironmentVariable;
    }

    public EffectiveConfig Inspect()
    {
        var envPath = _paths.DirectusEnvPath;
        var envFileExists = File.Exists(envPath);

        IReadOnlyDictionary<string, string> fileValues = new Dictionary<string, string>();
        string? envFileProblem = null;
        if (!envFileExists)
        {
            envFileProblem =
                $"'{envPath}' does not exist — file-sourced secrets read as not set; "
                + DirectusUrlResolver.WorktreeCaveat;
        }
        else
        {
            try
            {
                fileValues = EnvFile.Load(envPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                envFileProblem = $"could not read '{envPath}': {ex.Message}; "
                    + DirectusUrlResolver.WorktreeCaveat;
            }
        }

        return new EffectiveConfig(
            RepoRoot: _paths.RepoRoot,
            RepoRootIsExplicit: _paths.RepoRootIsExplicit,
            ContentDir: _paths.ContentDir,
            DirectusEnvPath: envPath,
            DirectusEnvFileExists: envFileExists,
            // Same resolver as the Directus health tile — one owner of the precedence.
            DirectusUrl: DirectusUrlResolver.Resolve(envPath, _getEnv),
            ForgejoBaseUrl: _options.ForgejoBaseUrl,
            ForgejoBaseUrlIsDefault: _options.ForgejoBaseUrl == Defaults.ForgejoBaseUrl,
            ForgejoRepo: _options.ForgejoRepo,
            ForgejoRepoIsDefault: _options.ForgejoRepo == Defaults.ForgejoRepo,
            Secrets:
            [
                EnvOnlyBadge("FORGEJO_TOKEN"),
                Badge("ADMIN_EMAIL", fileValues),
                Badge("ADMIN_PASSWORD", fileValues),
            ],
            EnvFileProblem: envFileProblem);
    }

    /// <summary>FORGEJO_TOKEN never comes from a file — only the exported variable counts.</summary>
    private SecretBadge EnvOnlyBadge(string name)
        => _getEnv(name) is { Length: > 0 }
            ? new SecretBadge(name, IsSet: true, SecretSource.EnvironmentVariable, EnvVarOnly: true)
            : new SecretBadge(name, IsSet: false, SecretSource.NotSet, EnvVarOnly: true);

    /// <summary>Env var wins over the env file — the CliRunner precedence.</summary>
    private SecretBadge Badge(string name, IReadOnlyDictionary<string, string> fileValues)
    {
        if (_getEnv(name) is { Length: > 0 })
        {
            return new SecretBadge(name, IsSet: true, SecretSource.EnvironmentVariable, EnvVarOnly: false);
        }

        return fileValues.GetValueOrDefault(name) is { Length: > 0 }
            ? new SecretBadge(name, IsSet: true, SecretSource.EnvFile, EnvVarOnly: false)
            : new SecretBadge(name, IsSet: false, SecretSource.NotSet, EnvVarOnly: false);
    }
}
