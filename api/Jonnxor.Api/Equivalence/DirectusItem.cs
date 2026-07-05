using System.Text.Json;

namespace Jonnxor.Api.Equivalence;

/// <summary>
/// One Directus item flattened into a form the equivalence comparator can walk generically:
/// base fields as a name -> value map, plus its translations keyed by `languages_code`. Values
/// are normalized <see cref="object"/>s (bool/long/double/string/null/List/Dictionary) using
/// the same shape <see cref="Jonnxor.Api.Snapshot.SnapshotEntry.Fields"/> uses, so the
/// comparator never has to special-case JsonElement vs YAML-derived values.
/// </summary>
public sealed class DirectusItem
{
    public required string Slug { get; init; }
    public required IReadOnlyDictionary<string, object?> Base { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> TranslationsByLocale { get; init; }

    /// <summary>Fields that exist on every Directus item/translation row purely for the seam's
    /// own bookkeeping (primary keys, junction rows) — never present in a snapshot record and
    /// never worth diffing.</summary>
    public static readonly HashSet<string> SystemFields = ["id", "translations", "languages_code", "slug"];

    /// <summary>
    /// Builds a <see cref="DirectusItem"/> from the raw JSON returned by
    /// `GET /items/{collection}?fields=*,translations.*`. Requires a `slug` field (every
    /// collection's item has one) and a `translations` array of objects, each carrying
    /// `languages_code`. <paramref name="collection"/> excludes the one per-collection system
    /// field the schema doesn't name uniformly: the translation row's parent foreign key, whose
    /// column name is the collection name itself (e.g. `games_translations.games` — live-
    /// verified against `directus/schema/snapshot.yaml`), not a fixed name like `languages_code`.
    /// </summary>
    public static DirectusItem FromJson(JsonElement item, string? collection = null)
    {
        var slug = item.TryGetProperty("slug", out var slugEl) && slugEl.ValueKind == JsonValueKind.String
            ? slugEl.GetString()!
            : throw new InvalidOperationException("Directus item has no string 'slug' field");

        var baseFields = new Dictionary<string, object?>();
        var translations = new Dictionary<string, IReadOnlyDictionary<string, object?>>();

        foreach (var prop in item.EnumerateObject())
        {
            if (prop.NameEquals("translations"))
            {
                foreach (var t in prop.Value.EnumerateArray())
                {
                    var localeFields = new Dictionary<string, object?>();
                    string? locale = null;
                    foreach (var tprop in t.EnumerateObject())
                    {
                        if (tprop.NameEquals("languages_code"))
                        {
                            locale = tprop.Value.GetString();
                        }

                        if (!SystemFields.Contains(tprop.Name) && tprop.Name != collection)
                        {
                            localeFields[tprop.Name] = JsonValueConverter.Convert(tprop.Value);
                        }
                    }

                    if (locale is not null)
                    {
                        translations[locale] = localeFields;
                    }
                }

                continue;
            }

            if (!SystemFields.Contains(prop.Name))
            {
                baseFields[prop.Name] = JsonValueConverter.Convert(prop.Value);
            }
        }

        return new DirectusItem
        {
            Slug = slug,
            Base = baseFields,
            TranslationsByLocale = translations,
        };
    }
}
