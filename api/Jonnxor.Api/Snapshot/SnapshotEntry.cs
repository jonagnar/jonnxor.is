namespace Jonnxor.Api.Snapshot;

/// <summary>
/// One locale file within the content snapshot: `&lt;content&gt;/&lt;collection&gt;/&lt;slug&gt;.&lt;locale&gt;.{yaml,md}`.
/// </summary>
public sealed class SnapshotEntry
{
    public required string Collection { get; init; }
    public required string Slug { get; init; }
    public required string Locale { get; init; }
    public required string FilePath { get; init; }

    /// <summary>
    /// The raw frontmatter text — for YAML files, the whole file; for blog Markdown,
    /// the text between the leading `---` fences. Used by comment-token scanning so the
    /// unquoted-mid-scalar-` #`-truncation bug class is caught before parsing discards it.
    /// </summary>
    public required string RawFrontmatter { get; init; }

    /// <summary>
    /// Parsed top-level YAML/frontmatter fields. Plain scalars are resolved to YAML core
    /// schema types (bool/long/double/null); quoted scalars stay strings. Empty when
    /// <see cref="ParseError"/> is set.
    /// </summary>
    public required IReadOnlyDictionary<string, object?> Fields { get; init; }

    /// <summary>
    /// Non-null when the file could not be parsed (malformed YAML, or a blog `.md` without
    /// well-formed leading `---` frontmatter fences). Parse failures surface as verifier
    /// findings via <c>ParseErrorRule</c> rather than crashing the run; rules that inspect
    /// <see cref="Fields"/> or <see cref="RawFrontmatter"/> skip entries with a parse error.
    /// </summary>
    public string? ParseError { get; init; }
}
