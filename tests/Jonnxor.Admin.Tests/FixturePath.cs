namespace Jonnxor.Admin.Tests;

/// <summary>
/// Resolves fixture content directories by walking up from the running test assembly's
/// location to the checked-out <c>tests/Jonnxor.Admin.Tests/fixtures</c> directory —
/// same pattern as the API suite's <c>FixturePath</c>: fixtures are read from source,
/// no CopyToOutputDirectory duplication.
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
