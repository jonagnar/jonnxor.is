using YamlDotNet.Serialization;

namespace Jonnxor.Api.Snapshot;

/// <summary>
/// Enumerates the content snapshot: `&lt;content&gt;/&lt;collection&gt;/&lt;slug&gt;.&lt;locale&gt;.{yaml,md}`.
/// One directory level below the content root names the collection; everything inside is a
/// locale file for some slug. This mirrors the Node loaders' `generateId: localeEntryId`
/// convention (`src/content/loaders.ts`) — one file per locale, never merged on disk.
/// </summary>
public static class SnapshotReader
{
    private static readonly string[] Locales = ["is", "en", "ja"];
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder().Build();

    public static IEnumerable<SnapshotEntry> ReadAll(string contentRoot)
    {
        if (!Directory.Exists(contentRoot))
        {
            throw new DirectoryNotFoundException($"Content directory not found: {contentRoot}");
        }

        foreach (var collectionDir in Directory.EnumerateDirectories(contentRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            var collection = Path.GetFileName(collectionDir);

            foreach (var filePath in Directory.EnumerateFiles(collectionDir)
                         .Where(f => f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                                     || f.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                if (!TryParseFilename(filePath, out var slug, out var locale))
                {
                    continue;
                }

                var rawText = File.ReadAllText(filePath);
                var isMarkdown = filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
                var frontmatter = isMarkdown ? ExtractMarkdownFrontmatter(rawText) : rawText;
                var fields = ParseFields(frontmatter);

                yield return new SnapshotEntry
                {
                    Collection = collection,
                    Slug = slug,
                    Locale = locale,
                    FilePath = filePath,
                    RawFrontmatter = frontmatter,
                    Fields = fields,
                };
            }
        }
    }

    private static bool TryParseFilename(string filePath, out string slug, out string locale)
    {
        slug = string.Empty;
        locale = string.Empty;

        var fileName = Path.GetFileNameWithoutExtension(filePath); // "<slug>.<locale>"
        var lastDot = fileName.LastIndexOf('.');
        if (lastDot < 0)
        {
            return false;
        }

        var candidateLocale = fileName[(lastDot + 1)..];
        if (!Locales.Contains(candidateLocale))
        {
            return false;
        }

        slug = fileName[..lastDot];
        locale = candidateLocale;
        return true;
    }

    /// <summary>
    /// Blog frontmatter is the text between the leading `---` fences. If the fences are
    /// missing/malformed, the whole file is returned so downstream rules still see it (and
    /// can flag the shape problem rather than silently skipping the file).
    /// </summary>
    private static string ExtractMarkdownFrontmatter(string rawText)
    {
        const string fence = "---";
        var normalized = rawText.Replace("\r\n", "\n");
        if (!normalized.StartsWith(fence, StringComparison.Ordinal))
        {
            return rawText;
        }

        var afterOpen = normalized.IndexOf('\n', fence.Length);
        if (afterOpen < 0)
        {
            return rawText;
        }

        var closeIndex = normalized.IndexOf("\n" + fence, afterOpen, StringComparison.Ordinal);
        if (closeIndex < 0)
        {
            return rawText;
        }

        return normalized[(afterOpen + 1)..closeIndex];
    }

    private static IReadOnlyDictionary<string, object?> ParseFields(string yamlText)
    {
        var raw = YamlDeserializer.Deserialize<Dictionary<object, object?>>(yamlText);
        if (raw is null)
        {
            return new Dictionary<string, object?>();
        }

        var result = new Dictionary<string, object?>();
        foreach (var (key, value) in raw)
        {
            result[key.ToString() ?? string.Empty] = value;
        }

        return result;
    }
}
