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

    /// <summary>Parsed top-level YAML/frontmatter fields.</summary>
    public required IReadOnlyDictionary<string, object?> Fields { get; init; }
}
