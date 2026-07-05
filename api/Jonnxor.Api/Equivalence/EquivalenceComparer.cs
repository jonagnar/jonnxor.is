using Jonnxor.Api.Snapshot;
using Jonnxor.Api.Verification;

namespace Jonnxor.Api.Equivalence;

/// <summary>
/// Pure, HTTP-free comparator: snapshot entries vs Directus items, for one collection at a
/// time. Deliberately generic — it does NOT re-encode `client/scripts/lib/collections.mjs`'s
/// per-field kind maps (the design's explicit non-goal). Instead it treats every snapshot
/// field as "compares against the Directus item's base field of the same name if present,
/// else the matching translation's field of the same name," with a small set of normalization
/// rules that account for the seam's known, deliberate shape differences:
///
/// <list type="bullet">
/// <item>null (Directus) ≈ absent (snapshot omits `undefined`/optional fields entirely) ≈ each
/// other — `entry-yaml.mjs` drops undefined/null keys on serialize, so a field that is
/// genuinely unset never appears in the snapshot file at all.</item>
/// <item>Numbers compare by value across the long/double boundary (`55 == 55.0`).</item>
/// <item>Arrays/objects compare structurally (order-sensitive for arrays, matching the
/// snapshot's array-preserving round-trip).</item>
/// <item>blog/grimoire `date`/`updated`: Directus stores a full timestamp; the snapshot stores
/// the Node `toRecord` convention's first 10 characters. The Directus side is sliced to 10
/// chars before comparing (mirrors `p.date.slice(0, 10)` / `d.updated.slice(0, 10)`).</item>
/// <item>blog's `readTime` (snapshot) / `read_time` (Directus): the one legacy rename in the
/// descriptor table, handled via a tiny per-collection rename map rather than generalizing
/// renames for every collection.</item>
/// </list>
///
/// `slug` and `locale` are snapshot-only bookkeeping (the filename already encodes them) and
/// are excluded from the field walk; Directus-only system fields (`id`, junction ids, the
/// `translations` container, and the translation row's collection-named parent FK) are
/// excluded upstream by <see cref="DirectusItem.FromJson"/>.
///
/// The walk runs both directions: the forward pass above visits every SNAPSHOT field and
/// checks it against Directus; a second reverse pass (<see cref="CompareReverse"/>) visits
/// every populated DIRECTUS field and flags any whose name is entirely missing from the
/// snapshot entry — catching a field Directus carries that the snapshot dropped outright,
/// which the forward walk can never see since it only iterates the snapshot's own keys. Two
/// exclusions keep that pass from false-positiving on documented, deliberate shape gaps
/// (both live-verified against the real Directus stack on 2026-07-05): a Directus `false` on
/// an `omitEmpty`/"flag"-convention field (`entry-yaml.mjs`) the snapshot never writes when
/// false, and blog's `body` (<see cref="ReverseWalkStructuralExclusions"/>), which lives after
/// the frontmatter fences rather than as a frontmatter key <c>SnapshotReader</c> can see.
/// </summary>
public static class EquivalenceComparer
{
    private const string Rule = "LiveEquivalence";

    /// <summary>Collections whose `date`/`updated` fields need the 10-char Directus-side slice.</summary>
    private static readonly HashSet<string> DateSlicedCollections = ["blog", "grimoire"];
    private static readonly string[] DateSlicedFields = ["date", "updated"];

    /// <summary>The one legacy rename (blog only): snapshot field name -> Directus field name.</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> FieldRenames =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["blog"] = new Dictionary<string, string> { ["readTime"] = "read_time" },
        };

    private static readonly HashSet<string> BookkeepingFields = ["slug", "locale"];

    /// <summary>
    /// Fields the reverse walk must not flag as "missing in snapshot" even though they are
    /// absent from <see cref="SnapshotEntry.Fields"/>, because they are absent from the
    /// SNAPSHOT FILE ITSELF by a documented, structural convention rather than by drift:
    ///
    /// <list type="bullet">
    /// <item>blog's `body`: `serializePost`/`parsePost` (`client/scripts/lib/collections.mjs`)
    /// store the post body as Markdown text AFTER the frontmatter fences, not as a frontmatter
    /// key — <see cref="Jonnxor.Api.Snapshot.SnapshotReader"/> only parses the frontmatter into
    /// <c>Fields</c> (by design: `RawFrontmatter` is "the whole file for YAML, the fenced
    /// region for Markdown"), so `body` can never appear there for blog even though the
    /// record genuinely carries it. This is a reader-coverage gap, not content drift; live-
    /// verified against the real Directus stack (2026-07-05).</item>
    /// </list>
    /// </summary>
    private static readonly IReadOnlyDictionary<string, HashSet<string>> ReverseWalkStructuralExclusions =
        new Dictionary<string, HashSet<string>>
        {
            ["blog"] = ["body"],
        };

    public static IReadOnlyList<Finding> Compare(
        string collection,
        IReadOnlyList<SnapshotEntry> snapshotEntries,
        IReadOnlyList<DirectusItem> directusItems)
    {
        var findings = new List<Finding>();

        var snapshotBySlug = snapshotEntries
            .Where(e => e.ParseError is null)
            .GroupBy(e => e.Slug)
            .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.Locale, e => e));
        var directusBySlug = directusItems.ToDictionary(i => i.Slug);

        var snapshotSlugs = snapshotBySlug.Keys.ToHashSet(StringComparer.Ordinal);
        var directusSlugs = directusBySlug.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var slug in snapshotSlugs.Except(directusSlugs).OrderBy(s => s, StringComparer.Ordinal))
        {
            findings.Add(new Finding(Rule, $"{collection}/{slug}", $"{collection}/{slug}: present in snapshot, missing in Directus"));
        }

        foreach (var slug in directusSlugs.Except(snapshotSlugs).OrderBy(s => s, StringComparer.Ordinal))
        {
            findings.Add(new Finding(Rule, $"{collection}/{slug}", $"{collection}/{slug}: present in Directus, missing in snapshot"));
        }

        foreach (var slug in snapshotSlugs.Intersect(directusSlugs).OrderBy(s => s, StringComparer.Ordinal))
        {
            var directusItem = directusBySlug[slug];
            CompareSlug(collection, slug, snapshotBySlug[slug], directusItem, findings);
        }

        return findings;
    }

    private static void CompareSlug(
        string collection,
        string slug,
        IReadOnlyDictionary<string, SnapshotEntry> snapshotByLocale,
        DirectusItem directusItem,
        List<Finding> findings)
    {
        var snapshotLocales = snapshotByLocale.Keys.ToHashSet(StringComparer.Ordinal);
        var directusLocales = directusItem.TranslationsByLocale.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var locale in snapshotLocales.Except(directusLocales).OrderBy(l => l, StringComparer.Ordinal))
        {
            findings.Add(new Finding(Rule, $"{collection}/{slug}/{locale}",
                $"{collection}/{slug}/{locale}: locale file present in snapshot, missing translation in Directus"));
        }

        foreach (var locale in directusLocales.Except(snapshotLocales).OrderBy(l => l, StringComparer.Ordinal))
        {
            findings.Add(new Finding(Rule, $"{collection}/{slug}/{locale}",
                $"{collection}/{slug}/{locale}: translation present in Directus, missing locale file in snapshot"));
        }

        foreach (var locale in snapshotLocales.Intersect(directusLocales).OrderBy(l => l, StringComparer.Ordinal))
        {
            var entry = snapshotByLocale[locale];
            var translation = directusItem.TranslationsByLocale[locale];
            CompareFields(collection, slug, locale, entry, directusItem.Base, translation, findings);
            CompareReverse(collection, slug, locale, entry, directusItem.Base, translation, findings);
        }
    }

    /// <summary>
    /// The reverse walk: after the snapshot->Directus field pass (which only visits fields the
    /// SNAPSHOT carries), walk every Directus base + translation field and flag any populated
    /// value whose (rename-reversed) name is missing from the snapshot entry entirely. This
    /// catches drift the forward walk structurally cannot see — a field Directus carries that
    /// the snapshot dropped outright — while still treating null/absent-per-kind Directus
    /// values as "nothing to report" (a genuinely unset column is not drift). System fields are
    /// already excluded upstream by <see cref="DirectusItem.FromJson"/>, including the
    /// translation parent FK (named after the collection, e.g. `games_translations.games`).
    /// </summary>
    private static void CompareReverse(
        string collection,
        string slug,
        string locale,
        SnapshotEntry entry,
        IReadOnlyDictionary<string, object?> directusBase,
        IReadOnlyDictionary<string, object?> directusTranslation,
        List<Finding> findings)
    {
        var renames = FieldRenames.GetValueOrDefault(collection, EmptyRenames);
        var reverseRenames = renames.ToDictionary(kv => kv.Value, kv => kv.Key);

        var structuralExclusions = ReverseWalkStructuralExclusions.GetValueOrDefault(collection, EmptyExclusions);

        void CheckSide(IReadOnlyDictionary<string, object?> directusFields)
        {
            foreach (var (directusFieldName, directusValue) in directusFields)
            {
                // null/empty-per-kind ≈ absent (same rule the forward walk applies), PLUS
                // `false` specifically: `entry-yaml.mjs`'s documented `omitEmpty` convention
                // drops boolean "flag" fields from the snapshot file entirely when false
                // (`out: (v) => v || undefined` in `collections.mjs`) — so a Directus `false`
                // with no snapshot key is that convention working as designed, not drift.
                // Scoped to the reverse walk only: the forward walk still flags a snapshot
                // `draft: false` missing from Directus, since non-flag booleans ARE written.
                if (IsAbsent(directusValue) || directusValue is false)
                {
                    continue;
                }

                var snapshotFieldName = reverseRenames.GetValueOrDefault(directusFieldName, directusFieldName);
                if (BookkeepingFields.Contains(snapshotFieldName) || structuralExclusions.Contains(snapshotFieldName))
                {
                    continue;
                }

                if (!entry.Fields.ContainsKey(snapshotFieldName))
                {
                    findings.Add(FieldFinding(
                        collection, slug, locale, snapshotFieldName, null, directusValue,
                        "field present on Directus item/translation, missing in snapshot"));
                }
            }
        }

        CheckSide(directusBase);
        CheckSide(directusTranslation);
    }

    private static void CompareFields(
        string collection,
        string slug,
        string locale,
        SnapshotEntry entry,
        IReadOnlyDictionary<string, object?> directusBase,
        IReadOnlyDictionary<string, object?> directusTranslation,
        List<Finding> findings)
    {
        var renames = FieldRenames.GetValueOrDefault(collection, EmptyRenames);

        foreach (var (field, snapshotValue) in entry.Fields)
        {
            if (BookkeepingFields.Contains(field))
            {
                continue;
            }

            var directusFieldName = renames.GetValueOrDefault(field, field);

            object? directusValue;
            bool foundOnDirectus;
            if (directusBase.TryGetValue(directusFieldName, out directusValue))
            {
                foundOnDirectus = true;
            }
            else if (directusTranslation.TryGetValue(directusFieldName, out directusValue))
            {
                foundOnDirectus = true;
            }
            else
            {
                foundOnDirectus = false;
            }

            if (DateSlicedCollections.Contains(collection) && DateSlicedFields.Contains(field) && directusValue is string dateStr)
            {
                directusValue = dateStr.Length > 10 ? dateStr[..10] : dateStr;
            }

            if (!foundOnDirectus)
            {
                // A field the snapshot carries but Directus never returned under either name.
                // Only worth flagging if the snapshot's own value isn't itself "absent" —
                // null≈absent so an omitted-on-both-sides field is not a drift.
                if (!IsAbsent(snapshotValue))
                {
                    findings.Add(FieldFinding(collection, slug, locale, field, snapshotValue, null, "field absent on Directus item/translation"));
                }

                continue;
            }

            if (!ValuesEquivalent(snapshotValue, directusValue))
            {
                findings.Add(FieldFinding(collection, slug, locale, field, snapshotValue, directusValue, "value mismatch"));
            }
        }
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyRenames = new Dictionary<string, string>();
    private static readonly HashSet<string> EmptyExclusions = [];

    private static Finding FieldFinding(
        string collection, string slug, string locale, string field, object? snapshotValue, object? directusValue, string reason) =>
        new(Rule, $"{collection}/{slug}/{locale}/{field}",
            $"{collection}/{slug}/{locale}/{field}: {reason} (snapshot={Describe(snapshotValue)}, directus={Describe(directusValue)})");

    private static string Describe(object? value) => value switch
    {
        null => "<absent/null>",
        string s => $"\"{s}\"",
        List<object?> list => $"[{string.Join(", ", list.Select(Describe))}]",
        Dictionary<string, object?> map => $"{{{string.Join(", ", map.Select(kv => $"{kv.Key}: {Describe(kv.Value)}"))}}}",
        _ => value.ToString() ?? "<null>",
    };

    /// <summary>null≈absent≈empty-per-kind: an empty string, empty list, or empty map on
    /// EITHER side is treated as equivalent to null/absent on the other, since Directus and
    /// the snapshot serializer disagree about representing "nothing" (Directus often returns
    /// `null` for an unset array/object field where the snapshot has none of the key at all,
    /// or an empty array where the snapshot omits the key via `??[]` defaulting). Honesty note:
    /// this predicate is kind-agnostic by construction — it inspects the value alone (a single
    /// switch over null/string/list/map), never which side (snapshot vs Directus) or which
    /// collection produced it, so "empty-per-kind" above means "per the value's own .NET
    /// runtime type," not per any collection-specific field-kind knowledge.</summary>
    private static bool IsAbsent(object? value) => value switch
    {
        null => true,
        string s => s.Length == 0,
        List<object?> list => list.Count == 0,
        Dictionary<string, object?> map => map.Count == 0,
        _ => false,
    };

    private static bool ValuesEquivalent(object? snapshotValue, object? directusValue)
    {
        if (IsAbsent(snapshotValue) && IsAbsent(directusValue))
        {
            return true;
        }

        return StructurallyEqual(snapshotValue, directusValue);
    }

    private static bool StructurallyEqual(object? a, object? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        // Numbers compare by value across long/double.
        if (IsNumber(a) && IsNumber(b))
        {
            return ToDouble(a) == ToDouble(b);
        }

        if (a is List<object?> listA && b is List<object?> listB)
        {
            if (listA.Count != listB.Count)
            {
                return false;
            }

            return listA.Zip(listB, StructurallyEqual).All(eq => eq);
        }

        if (a is Dictionary<string, object?> mapA && b is Dictionary<string, object?> mapB)
        {
            var keys = new HashSet<string>(mapA.Keys, StringComparer.Ordinal);
            keys.UnionWith(mapB.Keys);
            return keys.All(k => ValuesEquivalent(
                mapA.GetValueOrDefault(k), mapB.GetValueOrDefault(k)));
        }

        return a.Equals(b);
    }

    private static bool IsNumber(object value) => value is long or double or int;

    private static double ToDouble(object value) => value switch
    {
        long l => l,
        int i => i,
        double d => d,
        _ => throw new InvalidOperationException($"Not a number: {value}"),
    };
}
