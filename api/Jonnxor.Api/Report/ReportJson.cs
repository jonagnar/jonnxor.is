using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jonnxor.Api.Report;

/// <summary>
/// Serializes a <see cref="CoverageReport"/> to the `--json` artifact schema:
/// `{ generatedAt, collections: [{ name, slugs, locales: { is: { present, coverage }, ... } }] }`.
/// camelCase property names, ISO-8601 `generatedAt` (the default for <see cref="DateTimeOffset"/>).
/// </summary>
public static class ReportJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string Serialize(CoverageReport report) => JsonSerializer.Serialize(report, Options);
}
