namespace Jonnxor.Api.Tests;

/// <summary>Disposable temp directory for synthesizing one-off snapshot shapes in tests.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("jonnxor-api-tests-").FullName;

    public string WriteFile(string collection, string fileName, string content)
    {
        var dir = Directory.CreateDirectory(System.IO.Path.Combine(Path, collection));
        var filePath = System.IO.Path.Combine(dir.FullName, fileName);
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
