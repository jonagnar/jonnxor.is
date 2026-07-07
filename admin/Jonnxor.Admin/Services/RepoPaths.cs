namespace Jonnxor.Admin.Services;

/// <summary>
/// Resolves the repo root once, at construction, and derives every absolute path
/// the panel hands to git/pnpm/readers from it. An explicit
/// <see cref="AdminOptions.RepoRoot"/> wins and must be absolute (a relative value
/// would resolve against the process CWD and is refused); otherwise the resolver walks up from
/// the app base directory to the first directory containing <c>jonnxor.sln</c>
/// (at runtime that base is <c>admin/Jonnxor.Admin/bin/…</c> inside the repo).
/// Failing to resolve is a hard constructor failure — no service downstream may
/// operate on a guessed root.
/// </summary>
public sealed class RepoPaths
{
    private const string SolutionFileName = "jonnxor.sln";

    /// <param name="options">Bound panel configuration; relative paths in it are resolved here.</param>
    /// <param name="startDirectory">
    /// Walk-up starting point, injectable for tests. Null means
    /// <see cref="AppContext.BaseDirectory"/>.
    /// </param>
    public RepoPaths(AdminOptions options, string? startDirectory = null)
    {
        RepoRoot = options.RepoRoot is { Length: > 0 } explicitRoot
            ? ValidateExplicitRoot(explicitRoot)
            : FindRepoRoot(startDirectory ?? AppContext.BaseDirectory);

        ContentDir = Path.GetFullPath(Path.Combine(RepoRoot, options.ContentDir));
        ClientDir = Path.GetFullPath(Path.Combine(RepoRoot, "client"));
        DirectusEnvPath = Path.GetFullPath(Path.Combine(RepoRoot, options.DirectusEnvPath));
    }

    /// <summary>Absolute repo root (the directory containing <c>jonnxor.sln</c>).</summary>
    public string RepoRoot { get; }

    /// <summary>Absolute path to the committed content snapshot.</summary>
    public string ContentDir { get; }

    /// <summary>Absolute path to the JS/Astro workspace — the working dir for pnpm shell-outs.</summary>
    public string ClientDir { get; }

    /// <summary>Absolute path to the Directus client env file.</summary>
    public string DirectusEnvPath { get; }

    private static string ValidateExplicitRoot(string root)
    {
        if (!Path.IsPathRooted(root))
        {
            // A relative override would silently resolve against the process
            // working directory — a wrong-but-plausible root, worse than failing.
            throw new InvalidOperationException(
                $"Admin:RepoRoot must be an absolute path; got '{root}'.");
        }

        return Path.GetFullPath(root);
    }

    private static string FindRepoRoot(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not resolve the repo root: no '{SolutionFileName}' found in '{startDirectory}' "
            + "or any parent directory. Set Admin:RepoRoot explicitly.");
    }
}
