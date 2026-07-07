using System.Text.Json;
using Jonnxor.Api.Equivalence;
using Jonnxor.Api.Snapshot;
using Jonnxor.Api.Verification;

namespace Jonnxor.Api.Tests;

public class EquivalenceComparerTests
{
    private static SnapshotEntry Entry(string collection, string slug, string locale, IReadOnlyDictionary<string, object?> fields) =>
        new()
        {
            Collection = collection,
            Slug = slug,
            Locale = locale,
            FilePath = $"{collection}/{slug}.{locale}.yaml",
            RawFrontmatter = string.Empty,
            Fields = fields,
        };

    private static DirectusItem DirectusItemFromJson(string json) =>
        Equivalence.DirectusItem.FromJson(JsonDocument.Parse(json).RootElement);

    [Fact]
    public void EqualSnapshotAndDirectus_ProducesNoFindings()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?>
            {
                ["slug"] = "astro-bot", ["locale"] = "en",
                ["order"] = 10L, ["tab"] = "played", ["initials"] = "AB",
                ["title"] = "Astro Bot", ["sub"] = "Platinum",
            }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                {
                  "id": 1, "slug": "astro-bot", "order": 10, "tab": "played", "initials": "AB",
                  "translations": [
                    { "id": 100, "languages_code": "en", "title": "Astro Bot", "sub": "Platinum" }
                  ]
                }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void SlugMissingInDirectus_ProducesFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "only-in-snapshot", "en", new Dictionary<string, object?> { ["slug"] = "only-in-snapshot", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>();

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("only-in-snapshot", finding.Detail);
        Assert.Contains("missing in Directus", finding.Detail);
    }

    [Fact]
    public void SlugMissingInSnapshot_ProducesFinding()
    {
        var snapshot = new List<SnapshotEntry>();
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "only-in-directus", "translations": [] }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("only-in-directus", finding.Detail);
        Assert.Contains("missing in snapshot", finding.Detail);
    }

    [Fact]
    public void LocaleMissingInDirectusTranslations_ProducesFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en", ["title"] = "Astro Bot" }),
            Entry("games", "astro-bot", "is", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "is", ["title"] = "Astro Bot IS" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                {
                  "id": 1, "slug": "astro-bot",
                  "translations": [ { "id": 100, "languages_code": "en", "title": "Astro Bot" } ]
                }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("astro-bot/is", finding.Detail);
        Assert.Contains("missing translation in Directus", finding.Detail);
    }

    [Fact]
    public void BaseFieldDrift_ProducesFieldPreciseFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en", ["order"] = 10L }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "astro-bot", "order": 11, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("astro-bot/en/order", finding.Detail);
        Assert.Contains("snapshot=10", finding.Detail);
        Assert.Contains("directus=11", finding.Detail);
    }

    [Fact]
    public void TranslationFieldDrift_ProducesFieldPreciseFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en", ["title"] = "Astro Bot" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                {
                  "id": 1, "slug": "astro-bot",
                  "translations": [ { "id": 1, "languages_code": "en", "title": "Astro Bot 2" } ]
                }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("astro-bot/en/title", finding.Detail);
    }

    [Fact]
    public void NumberEquivalence_LongVsJsonNumber_NoFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("countdowns", "gaming", "en", new Dictionary<string, object?>
            {
                ["slug"] = "gaming", ["locale"] = "en", ["order"] = 8L, ["rate"] = 2.6,
            }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "gaming", "order": 8, "rate": 2.6, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("countdowns", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void BoolEquivalence_NoFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en", ["favorite"] = true }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "astro-bot", "favorite": true, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void ArrayEquivalence_StructuralMatch_NoFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?>
            {
                ["slug"] = "astro-bot", ["locale"] = "en",
                ["platforms"] = new List<object?> { "PS5" },
                ["gradient"] = new List<object?> { "#0a1f33", "#1d63b8" },
            }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                {
                  "id": 1, "slug": "astro-bot", "platforms": ["PS5"], "gradient": ["#0a1f33", "#1d63b8"],
                  "translations": [ { "id": 1, "languages_code": "en" } ]
                }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void ArrayDrift_ProducesFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?>
            {
                ["slug"] = "astro-bot", ["locale"] = "en", ["platforms"] = new List<object?> { "PS5" },
            }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "astro-bot", "platforms": ["PS5", "PC"], "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("platforms", finding.Detail);
    }

    [Fact]
    public void NestedObjectEquivalence_StructuralMatch_NoFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("projects", "drekis-vault", "en", new Dictionary<string, object?>
            {
                ["slug"] = "drekis-vault", ["locale"] = "en",
                ["status"] = new Dictionary<string, object?> { ["label"] = "Jam winner", ["kind"] = "gold" },
            }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                {
                  "id": 1, "slug": "drekis-vault",
                  "status": { "label": "Jam winner", "kind": "gold" },
                  "translations": [ { "id": 1, "languages_code": "en" } ]
                }
                """),
        };

        var findings = EquivalenceComparer.Compare("projects", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void NestedObjectDrift_ProducesFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("projects", "drekis-vault", "en", new Dictionary<string, object?>
            {
                ["slug"] = "drekis-vault", ["locale"] = "en",
                ["status"] = new Dictionary<string, object?> { ["label"] = "Jam winner", ["kind"] = "gold" },
            }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                {
                  "id": 1, "slug": "drekis-vault",
                  "status": { "label": "Jam winner", "kind": "silver" },
                  "translations": [ { "id": 1, "languages_code": "en" } ]
                }
                """),
        };

        var findings = EquivalenceComparer.Compare("projects", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("status", finding.Detail);
    }

    [Fact]
    public void NullVsAbsentField_NoFinding()
    {
        // Snapshot omits an optional field entirely (entry-yaml.mjs drops undefined/null on
        // serialize); Directus returns an explicit null for the same column.
        var snapshot = new List<SnapshotEntry>
        {
            Entry("projects", "drekis-vault", "en", new Dictionary<string, object?> { ["slug"] = "drekis-vault", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "drekis-vault", "status": null, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("projects", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void NullVsEmptyArray_NoFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("projects", "drekis-vault", "en", new Dictionary<string, object?>
            {
                ["slug"] = "drekis-vault", ["locale"] = "en", ["links"] = new List<object?>(),
            }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "drekis-vault", "links": null, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("projects", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void BlogDate_ComparesAfterSlicingDirectusTimestampToTenChars()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("blog", "some-post", "en", new Dictionary<string, object?> { ["slug"] = "some-post", ["locale"] = "en", ["date"] = "2026-03-30" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "some-post", "date": "2026-03-30T00:00:00.000Z", "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("blog", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void GrimoireUpdated_ComparesAfterSlicingDirectusTimestampToTenChars()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("grimoire", "chai", "en", new Dictionary<string, object?> { ["slug"] = "chai", ["locale"] = "en", ["updated"] = "2026-06-01" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "chai", "updated": "2026-06-01T12:34:56.000Z", "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("grimoire", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void BlogDateDrift_StillDetectedAfterSlicing()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("blog", "some-post", "en", new Dictionary<string, object?> { ["slug"] = "some-post", ["locale"] = "en", ["date"] = "2026-03-30" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "some-post", "date": "2026-03-31T00:00:00.000Z", "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("blog", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("date", finding.Detail);
    }

    [Fact]
    public void BlogReadTimeRename_ComparesAgainstDirectusReadTimeField()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("blog", "some-post", "en", new Dictionary<string, object?> { ["slug"] = "some-post", ["locale"] = "en", ["readTime"] = "4 min" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "some-post", "translations": [ { "id": 1, "languages_code": "en", "read_time": "4 min" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("blog", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void BlogReadTimeRenameDrift_StillDetected()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("blog", "some-post", "en", new Dictionary<string, object?> { ["slug"] = "some-post", ["locale"] = "en", ["readTime"] = "4 min" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "some-post", "translations": [ { "id": 1, "languages_code": "en", "read_time": "5 min" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("blog", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("readTime", finding.Detail);
    }

    [Fact]
    public void SlugAndLocaleBookkeepingFields_NeverCompared()
    {
        // `slug`/`locale` are snapshot-only bookkeeping the filename already encodes — even
        // though Directus's own `slug` differs in casing here, it must not be flagged because
        // the field walk excludes it entirely (DirectusItem.FromJson uses it for indexing only).
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "astro-bot", "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void ReverseWalk_PopulatedDirectusBaseFieldMissingInSnapshot_ProducesFinding()
    {
        // The forward walk only ever iterates the SNAPSHOT's own field names, so a field
        // Directus carries that the snapshot dropped outright is invisible to it — this is
        // exactly the gap the reverse walk closes.
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "astro-bot", "order": 10, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("astro-bot/en/order", finding.Detail);
        Assert.Contains("missing in snapshot", finding.Detail);
    }

    [Fact]
    public void ReverseWalk_PopulatedDirectusTranslationFieldMissingInSnapshot_ProducesFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                {
                  "id": 1, "slug": "astro-bot",
                  "translations": [ { "id": 1, "languages_code": "en", "title": "Astro Bot" } ]
                }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("astro-bot/en/title", finding.Detail);
        Assert.Contains("missing in snapshot", finding.Detail);
    }

    [Fact]
    public void ReverseWalk_NullDirectusBaseFieldMissingInSnapshot_NoFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("projects", "drekis-vault", "en", new Dictionary<string, object?> { ["slug"] = "drekis-vault", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "drekis-vault", "status": null, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("projects", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void ReverseWalk_EmptyArrayDirectusTranslationFieldMissingInSnapshot_NoFinding()
    {
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                {
                  "id": 1, "slug": "astro-bot",
                  "translations": [ { "id": 1, "languages_code": "en", "tags": [] } ]
                }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void ReverseWalk_FalseFlagFieldMissingInSnapshot_NoFinding()
    {
        // games.favorite is an `omitEmpty`/"flag" field (entry-yaml.mjs): the snapshot
        // serializer drops it from the file entirely when false. Live-verified against the
        // real stack — every game's `favorite: false` would otherwise false-positive here.
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "astro-bot", "favorite": false, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void ReverseWalk_TrueFlagFieldMissingInSnapshot_StillProducesFinding()
    {
        // The false-flag exclusion must not swallow a genuinely missing `true` value — only
        // `false` is the documented "omitted" convention.
        var snapshot = new List<SnapshotEntry>
        {
            Entry("games", "astro-bot", "en", new Dictionary<string, object?> { ["slug"] = "astro-bot", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "astro-bot", "favorite": true, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("favorite", finding.Detail);
        Assert.Contains("missing in snapshot", finding.Detail);
    }

    [Fact]
    public void ReverseWalk_CountdownsGoldFalseMissingInSnapshot_NoFinding()
    {
        // countdowns.gold is the other documented flag field (entry-yaml.mjs omitEmpty
        // convention) — same mechanism as games.favorite, scoped per-collection.
        var snapshot = new List<SnapshotEntry>
        {
            Entry("countdowns", "gaming", "en", new Dictionary<string, object?> { ["slug"] = "gaming", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "gaming", "gold": false, "translations": [ { "id": 1, "languages_code": "en" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("countdowns", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void ReverseWalk_BlogDraftFalseMissingInSnapshot_ProducesFinding()
    {
        // The false-flag exclusion is scoped to a specific per-collection field set
        // (games.favorite, countdowns.gold) — NOT a blanket "any false is fine" rule. blog's
        // `draft` is a plain boolean the descriptor table always writes, so a hand-deleted
        // `draft: false` frontmatter line is genuine drift and must still surface here. This
        // is the T5-review fix: before scoping, a bare `directusValue is false` check would
        // have silently swallowed exactly this case.
        var snapshot = new List<SnapshotEntry>
        {
            Entry("blog", "some-post", "en", new Dictionary<string, object?> { ["slug"] = "some-post", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                {
                  "id": 1, "slug": "some-post",
                  "translations": [ { "id": 1, "languages_code": "en", "draft": false } ]
                }
                """),
        };

        var findings = EquivalenceComparer.Compare("blog", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("draft", finding.Detail);
        Assert.Contains("missing in snapshot", finding.Detail);
    }

    [Fact]
    public void ReverseWalk_BlogBodyMissingInSnapshotFields_NoFinding()
    {
        // blog's `body` lives after the frontmatter fences (serializePost), never as a
        // frontmatter key — SnapshotReader.Fields structurally cannot contain it for blog,
        // so the reverse walk must not flag it as dropped content. Live-verified.
        var snapshot = new List<SnapshotEntry>
        {
            Entry("blog", "some-post", "en", new Dictionary<string, object?> { ["slug"] = "some-post", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "some-post", "translations": [ { "id": 1, "languages_code": "en", "body": "Some real body text." } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("blog", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void ReverseWalk_GrimoireBodyMissingInSnapshotFields_StillProducesFinding()
    {
        // The blog-body exclusion is scoped to blog only — grimoire's `body` IS a real
        // frontmatter YAML key (grimoire files are .yaml, not .md), so a grimoire entry
        // missing `body` in its Fields is genuine drift and must still be flagged.
        var snapshot = new List<SnapshotEntry>
        {
            Entry("grimoire", "chai", "en", new Dictionary<string, object?> { ["slug"] = "chai", ["locale"] = "en" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "chai", "translations": [ { "id": 1, "languages_code": "en", "body": "Real body text." } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("grimoire", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("body", finding.Detail);
        Assert.Contains("missing in snapshot", finding.Detail);
    }

    [Fact]
    public void ReverseWalk_AppliesRenameReversal_NoFalsePositiveOnBlogReadTime()
    {
        // Directus's `read_time` reverse-renames to the snapshot's `readTime` before the
        // presence check — without the reversal this would false-positive as "missing" even
        // though the forward-walk rename test proves the same pair equivalent.
        var snapshot = new List<SnapshotEntry>
        {
            Entry("blog", "some-post", "en", new Dictionary<string, object?> { ["slug"] = "some-post", ["locale"] = "en", ["readTime"] = "4 min" }),
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""
                { "id": 1, "slug": "some-post", "translations": [ { "id": 1, "languages_code": "en", "read_time": "4 min" } ] }
                """),
        };

        var findings = EquivalenceComparer.Compare("blog", snapshot, directus);

        Assert.Empty(findings);
    }

    [Fact]
    public void ParseErrorEntries_ExcludedFromComparison()
    {
        var snapshot = new List<SnapshotEntry>
        {
            new()
            {
                Collection = "games", Slug = "broken", Locale = "en", FilePath = "games/broken.en.yaml",
                RawFrontmatter = "not valid", Fields = new Dictionary<string, object?>(), ParseError = "boom",
            },
        };
        var directus = new List<DirectusItem>
        {
            DirectusItemFromJson("""{ "id": 1, "slug": "broken", "translations": [] }"""),
        };

        // The broken snapshot entry is excluded from the field walk (it has no usable Fields),
        // but its slug still isn't counted as "present in snapshot" for the slug-set check
        // since it was filtered out before grouping — so this surfaces as slug-missing rather
        // than crashing on empty fields.
        var findings = EquivalenceComparer.Compare("games", snapshot, directus);

        var finding = Assert.Single(findings);
        Assert.Contains("missing in snapshot", finding.Detail);
    }
}
