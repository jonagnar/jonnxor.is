using System.Text.Json;

namespace Jonnxor.Api.Equivalence;

/// <summary>
/// Converts a <see cref="JsonElement"/> into the same normalized value shape
/// <see cref="Jonnxor.Api.Snapshot.SnapshotReader"/> produces from YAML: bool/long/double/
/// string/null/List&lt;object?&gt;/Dictionary&lt;string, object?&gt;, recursively. This is what
/// lets <see cref="EquivalenceComparer"/> compare a snapshot field and a Directus field with
/// plain <c>Equals</c>/structural walks instead of juggling two different value systems.
/// </summary>
public static class JsonValueConverter
{
    public static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => ConvertNumber(element),
        JsonValueKind.Array => element.EnumerateArray().Select(Convert).ToList(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => Convert(p.Value)),
        _ => null,
    };

    private static object? ConvertNumber(JsonElement element)
    {
        // Integral values arrive as long (matching SnapshotReader's plain-int resolution to
        // long); anything with a fractional part or too large for long falls back to double.
        if (element.TryGetInt64(out var asLong))
        {
            return asLong;
        }

        return element.GetDouble();
    }
}
