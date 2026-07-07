using System.Text.Json;
using Jonnxor.Api.Equivalence;

namespace Jonnxor.Api.Tests;

public class DirectusItemTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void FromJson_ExtractsSlugBaseFieldsAndTranslationsByLocale()
    {
        var item = DirectusItem.FromJson(Parse("""
            {
              "id": 7, "slug": "astro-bot", "order": 10, "favorite": true,
              "translations": [
                { "id": 100, "languages_code": "en", "title": "Astro Bot" },
                { "id": 101, "languages_code": "is", "title": "Astro Bot IS" }
              ]
            }
            """));

        Assert.Equal("astro-bot", item.Slug);
        Assert.Equal(10L, item.Base["order"]);
        Assert.Equal(true, item.Base["favorite"]);
        Assert.Equal("Astro Bot", item.TranslationsByLocale["en"]["title"]);
        Assert.Equal("Astro Bot IS", item.TranslationsByLocale["is"]["title"]);
    }

    [Fact]
    public void FromJson_ExcludesSystemFieldsFromBaseAndTranslations()
    {
        var item = DirectusItem.FromJson(Parse("""
            {
              "id": 7, "slug": "astro-bot",
              "translations": [ { "id": 100, "languages_code": "en", "title": "Astro Bot" } ]
            }
            """));

        Assert.DoesNotContain("id", item.Base.Keys);
        Assert.DoesNotContain("slug", item.Base.Keys);
        Assert.DoesNotContain("translations", item.Base.Keys);
        Assert.DoesNotContain("id", item.TranslationsByLocale["en"].Keys);
        Assert.DoesNotContain("languages_code", item.TranslationsByLocale["en"].Keys);
    }

    [Fact]
    public void FromJson_MissingSlug_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => DirectusItem.FromJson(Parse("""{ "id": 1, "translations": [] }""")));
    }

    [Fact]
    public void FromJson_ExcludesTranslationParentForeignKeyNamedAfterCollection()
    {
        // games_translations carries a hidden `games` column (foreign_key_table: games, per
        // directus/schema/snapshot.yaml) pointing back at the parent row — it is bookkeeping,
        // not a content field, and must not leak into TranslationsByLocale.
        var item = DirectusItem.FromJson(Parse("""
            {
              "id": 7, "slug": "astro-bot",
              "translations": [
                { "id": 100, "languages_code": "en", "games": 7, "title": "Astro Bot" }
              ]
            }
            """), collection: "games");

        Assert.DoesNotContain("games", item.TranslationsByLocale["en"].Keys);
        Assert.Equal("Astro Bot", item.TranslationsByLocale["en"]["title"]);
    }

    [Fact]
    public void FromJson_WithoutCollectionArgument_DoesNotExcludeAnyExtraField()
    {
        // The collection parameter is optional (defaults to null) so existing call sites that
        // don't pass it keep their prior behavior — nothing named `null` will ever match.
        var item = DirectusItem.FromJson(Parse("""
            {
              "id": 7, "slug": "astro-bot",
              "translations": [ { "id": 100, "languages_code": "en", "games": 7 } ]
            }
            """));

        Assert.Equal(7L, item.TranslationsByLocale["en"]["games"]);
    }
}

public class JsonValueConverterTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", null)]
    public void Convert_HandlesPrimitives(string json, object? expected)
    {
        Assert.Equal(expected, JsonValueConverter.Convert(Parse(json)));
    }

    [Fact]
    public void Convert_IntegralNumber_BecomesLong()
    {
        Assert.Equal(55L, JsonValueConverter.Convert(Parse("55")));
    }

    [Fact]
    public void Convert_FractionalNumber_BecomesDouble()
    {
        Assert.Equal(2.6, JsonValueConverter.Convert(Parse("2.6")));
    }

    [Fact]
    public void Convert_String_StaysString()
    {
        Assert.Equal("hello", JsonValueConverter.Convert(Parse("\"hello\"")));
    }

    [Fact]
    public void Convert_Array_BecomesListRecursively()
    {
        var result = Assert.IsType<List<object?>>(JsonValueConverter.Convert(Parse("[1, 2.5, \"x\", true, null]")));
        Assert.Equal([1L, 2.5, "x", true, null], result);
    }

    [Fact]
    public void Convert_Object_BecomesDictionaryRecursively()
    {
        var result = Assert.IsType<Dictionary<string, object?>>(JsonValueConverter.Convert(Parse("""{ "a": 1, "b": { "c": 2 } }""")));
        Assert.Equal(1L, result["a"]);
        var nested = Assert.IsType<Dictionary<string, object?>>(result["b"]);
        Assert.Equal(2L, nested["c"]);
    }
}
