using System.Globalization;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Jonnxor.Api.Snapshot;

/// <summary>
/// Enumerates the content snapshot: `&lt;content&gt;/&lt;collection&gt;/&lt;slug&gt;.&lt;locale&gt;.{yaml,md}`.
/// One directory level below the content root names the collection; everything inside is a
/// locale file for some slug. This mirrors the Node loaders' `generateId: localeEntryId`
/// convention (`src/content/loaders.ts`) — one file per locale, never merged on disk.
///
/// Scalar typing: plain (unquoted) scalars are resolved per the YAML 1.2 core schema —
/// `true`/`false` to bool, integers to long, floats to double, `null`/`~`/empty to null —
/// recursively through nested mappings and sequences. QUOTED scalars always stay strings:
/// quoting is authorial intent, and the seam's date-quoting convention (`when: "2026-10-09"`)
/// depends on that distinction surviving the round-trip. This normalization is also the
/// foundation for the Task 4 live-equivalence comparator (null≈absent, numbers by value):
/// snapshot values arrive here already typed, so the comparator never string-compares numbers.
///
/// Parse failures (malformed YAML, missing/unterminated blog frontmatter fences) do NOT
/// throw: the entry is yielded with <see cref="SnapshotEntry.ParseError"/> set and empty
/// fields, and the verifier reports it as a finding. Only a missing content root throws.
/// </summary>
public static class SnapshotReader
{
    private static readonly string[] Locales = ["is", "en", "ja"];
    private static readonly IReadOnlyDictionary<string, object?> EmptyFields = new Dictionary<string, object?>();

    // YAML 1.2 core schema resolution patterns for plain scalars.
    private static readonly Regex IntPattern = new("^[-+]?[0-9]+$", RegexOptions.Compiled);
    private static readonly Regex FloatPattern =
        new(@"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$", RegexOptions.Compiled);

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

                yield return ReadOne(collection, slug, locale, filePath);
            }
        }
    }

    private static SnapshotEntry ReadOne(string collection, string slug, string locale, string filePath)
    {
        var rawText = File.ReadAllText(filePath);
        var isMarkdown = filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

        string frontmatter;
        if (isMarkdown)
        {
            if (!TryExtractMarkdownFrontmatter(rawText, out frontmatter))
            {
                return MakeErrorEntry(collection, slug, locale, filePath, rawText,
                    "missing or unterminated leading '---' frontmatter fences");
            }
        }
        else
        {
            frontmatter = rawText;
        }

        IReadOnlyDictionary<string, object?> fields;
        try
        {
            fields = ParseFields(frontmatter, out var structureError);
            if (structureError is not null)
            {
                return MakeErrorEntry(collection, slug, locale, filePath, frontmatter, structureError);
            }
        }
        catch (YamlException ex)
        {
            return MakeErrorEntry(collection, slug, locale, filePath, frontmatter, $"YAML parse error: {ex.Message}");
        }

        return new SnapshotEntry
        {
            Collection = collection,
            Slug = slug,
            Locale = locale,
            FilePath = filePath,
            RawFrontmatter = frontmatter,
            Fields = fields,
        };
    }

    private static SnapshotEntry MakeErrorEntry(
        string collection, string slug, string locale, string filePath, string rawText, string error) =>
        new()
        {
            Collection = collection,
            Slug = slug,
            Locale = locale,
            FilePath = filePath,
            RawFrontmatter = rawText,
            Fields = EmptyFields,
            ParseError = error,
        };

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
    /// Blog frontmatter is the text between the leading `---` fences. Returns false when the
    /// opening fence is absent or the closing fence never arrives — the caller surfaces that
    /// as a parse-error finding instead of guessing at document shape.
    /// </summary>
    private static bool TryExtractMarkdownFrontmatter(string rawText, out string frontmatter)
    {
        const string fence = "---";
        frontmatter = string.Empty;

        var normalized = rawText.Replace("\r\n", "\n");
        if (!normalized.StartsWith(fence, StringComparison.Ordinal))
        {
            return false;
        }

        var afterOpen = normalized.IndexOf('\n', fence.Length);
        if (afterOpen < 0)
        {
            return false;
        }

        var closeIndex = normalized.IndexOf("\n" + fence, afterOpen, StringComparison.Ordinal);
        if (closeIndex < 0)
        {
            return false;
        }

        frontmatter = normalized[(afterOpen + 1)..closeIndex];
        return true;
    }

    private static IReadOnlyDictionary<string, object?> ParseFields(string yamlText, out string? structureError)
    {
        structureError = null;

        var stream = new YamlStream();
        stream.Load(new StringReader(yamlText));

        if (stream.Documents.Count == 0)
        {
            return EmptyFields;
        }

        if (stream.Documents[0].RootNode is not YamlMappingNode map)
        {
            structureError = "root YAML node is not a mapping";
            return EmptyFields;
        }

        var result = new Dictionary<string, object?>();
        foreach (var (key, value) in map.Children)
        {
            var name = key is YamlScalarNode scalarKey ? scalarKey.Value ?? string.Empty : key.ToString();
            result[name] = ConvertNode(value);
        }

        return result;
    }

    private static object? ConvertNode(YamlNode node) => node switch
    {
        YamlScalarNode scalar => ConvertScalar(scalar),
        YamlMappingNode map => map.Children.ToDictionary(
            kv => kv.Key is YamlScalarNode sk ? sk.Value ?? string.Empty : kv.Key.ToString(),
            kv => ConvertNode(kv.Value)),
        YamlSequenceNode seq => seq.Children.Select(ConvertNode).ToList(),
        _ => null,
    };

    private static object? ConvertScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value;

        // Quoted (and block literal/folded) scalars keep their string identity: quoting is
        // intent — `when: "2026-10-09"` must stay a string even though `2026` alone wouldn't.
        if (scalar.Style != ScalarStyle.Plain)
        {
            return value;
        }

        if (string.IsNullOrEmpty(value) || value is "null" or "Null" or "NULL" or "~")
        {
            return null;
        }

        if (value is "true" or "True" or "TRUE")
        {
            return true;
        }

        if (value is "false" or "False" or "FALSE")
        {
            return false;
        }

        if (IntPattern.IsMatch(value)
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var asLong))
        {
            return asLong;
        }

        if (FloatPattern.IsMatch(value)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var asDouble))
        {
            return asDouble;
        }

        return value switch
        {
            ".inf" or ".Inf" or ".INF" or "+.inf" or "+.Inf" or "+.INF" => double.PositiveInfinity,
            "-.inf" or "-.Inf" or "-.INF" => double.NegativeInfinity,
            ".nan" or ".NaN" or ".NAN" => double.NaN,
            _ => value,
        };
    }
}
