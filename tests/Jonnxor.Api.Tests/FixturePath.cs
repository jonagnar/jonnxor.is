namespace Jonnxor.Api.Tests;

/// <summary>
/// Resolves fixture content directories by walking up from the running test assembly's
/// location to find the checked-out `tests/Jonnxor.Api.Tests/fixtures` directory. This
/// avoids duplicating the fixture tree via CopyToOutputDirectory globs — the fixtures are
/// read directly from source, keyed off the repo layout, which the SnapshotReader tests
/// exercise as the "real" content directory shape anyway.
/// </summary>
internal static class FixturePath
{
    public static string For(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "fixtures")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException(
                $"Could not locate a 'fixtures' directory above '{AppContext.BaseDirectory}'.");
        }

        var path = Path.Combine(dir.FullName, "fixtures", name);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Fixture directory not found: {path}");
        }

        return path;
    }
}
