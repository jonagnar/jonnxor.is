namespace Jonnxor.Admin.Tests;

/// <summary>Disposable temp directory for synthesizing one-off directory trees in tests.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("jonnxor-admin-tests-").FullName;

    /// <summary>Creates (and returns the absolute path of) a nested directory.</summary>
    public string CreateDir(params string[] segments)
    {
        var dir = System.IO.Path.Combine([Path, .. segments]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Writes a file under the temp root, creating parent directories as needed.</summary>
    public string WriteFile(string relativePath, string content)
    {
        var filePath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }
}
