using Jonnxor.Api;

namespace Jonnxor.Admin.Services;

/// <summary>Which source produced the effective <c>DIRECTUS_URL</c>.</summary>
public enum DirectusUrlSource
{
    /// <summary>Nothing usable: no exported variable and no readable env-file value.</summary>
    Unresolved,

    /// <summary>An exported <c>DIRECTUS_URL</c> environment variable (always wins).</summary>
    EnvironmentVariable,

    /// <summary>The <c>DIRECTUS_URL</c> line of <see cref="RepoPaths.DirectusEnvPath"/>.</summary>
    EnvFile,
}

/// <summary>
/// Outcome of one resolution: the URL and which source won, or — when
/// <see cref="Url"/> is null — <see cref="Problem"/> explaining why (always ending
/// in the worktree caveat, since a missing env file is the common worktree accident).
/// </summary>
public sealed record DirectusUrlResolution(string? Url, DirectusUrlSource Source, string? Problem);

/// <summary>
/// The single owner of DIRECTUS_URL resolution precedence for the panel: an exported
/// env var wins over the env file — the same precedence as the API CLI's
/// <c>verify --live</c> (<c>CliRunner</c>). Both the Directus health tile and the
/// Config page's effective-configuration view resolve through here, so the two can
/// never disagree about which source won.
/// </summary>
public static class DirectusUrlResolver
{
    /// <summary>Worktree wording per the v1 plan — rendered verbatim wherever resolution fails.</summary>
    public const string WorktreeCaveat =
        "direnv does not reach worktrees — export DIRECTUS_URL or ensure directus/.env exists "
        + "(decrypted via sops/direnv from the main checkout)";

    public static DirectusUrlResolution Resolve(string envFilePath, Func<string, string?> getEnv)
    {
        if (getEnv("DIRECTUS_URL") is { Length: > 0 } fromEnv)
        {
            return new DirectusUrlResolution(fromEnv, DirectusUrlSource.EnvironmentVariable, null);
        }

        if (!File.Exists(envFilePath))
        {
            return new DirectusUrlResolution(
                null, DirectusUrlSource.Unresolved,
                $"DIRECTUS_URL is not exported and '{envFilePath}' does not exist; {WorktreeCaveat}");
        }

        IReadOnlyDictionary<string, string> fileValues;
        try
        {
            fileValues = EnvFile.Load(envFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A permission-restricted sops-decrypted file must degrade, not crash the caller.
            return new DirectusUrlResolution(
                null, DirectusUrlSource.Unresolved,
                $"could not read '{envFilePath}': {ex.Message}; {WorktreeCaveat}");
        }

        return fileValues.GetValueOrDefault("DIRECTUS_URL") is { Length: > 0 } fromFile
            ? new DirectusUrlResolution(fromFile, DirectusUrlSource.EnvFile, null)
            : new DirectusUrlResolution(
                null, DirectusUrlSource.Unresolved,
                $"DIRECTUS_URL is not exported and missing from '{envFilePath}'; {WorktreeCaveat}");
    }
}
