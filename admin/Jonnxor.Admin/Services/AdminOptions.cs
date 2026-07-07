namespace Jonnxor.Admin.Services;

/// <summary>
/// Panel configuration bound from the <c>"Admin"</c> section of appsettings/env.
/// Every property has a working default, so an empty section boots the panel;
/// relative paths are resolved against the repo root by <see cref="RepoPaths"/>,
/// never against the process working directory. No secrets live here — the
/// Forgejo token arrives via environment only.
/// </summary>
public sealed class AdminOptions
{
    /// <summary>Configuration section this type binds from.</summary>
    public const string SectionName = "Admin";

    /// <summary>
    /// Absolute repo root override. Null means resolve by walking up from the app
    /// base directory to the first directory containing <c>jonnxor.sln</c>.
    /// </summary>
    public string? RepoRoot { get; set; }

    /// <summary>Path to the Directus client env file, relative to the repo root.</summary>
    public string DirectusEnvPath { get; set; } = "directus/.env";

    /// <summary>Base URL of the Forgejo instance queried for CI run status.</summary>
    public string ForgejoBaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>Forgejo <c>owner/repo</c> slug for the CI tile's runs API.</summary>
    public string ForgejoRepo { get; set; } = "WAAAGH/jonnxor.is";

    /// <summary>Path to the committed content snapshot, relative to the repo root.</summary>
    public string ContentDir { get; set; } = "client/src/content";
}
