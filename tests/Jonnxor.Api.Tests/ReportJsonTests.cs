using System.Text.Json;
using Jonnxor.Api.Report;

namespace Jonnxor.Api.Tests;

public class ReportJsonTests
{
    [Fact]
    public void Serialize_MatchesArtifactSchema_CamelCaseTopLevelAndNestedKeys()
    {
        var report = new CoverageReport(
            new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero),
            [
                new CollectionCoverage("games", 2, new Dictionary<string, LocaleCoverage>
                {
                    ["is"] = new LocaleCoverage(0, 0.0),
                    ["en"] = new LocaleCoverage(2, 1.0),
                    ["ja"] = new LocaleCoverage(0, 0.0),
                }),
            ]);

        var json = ReportJson.Serialize(report);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("2026-07-05T12:00:00+00:00", root.GetProperty("generatedAt").GetString());

        var collections = root.GetProperty("collections");
        Assert.Equal(1, collections.GetArrayLength());

        var games = collections[0];
        Assert.Equal("games", games.GetProperty("name").GetString());
        Assert.Equal(2, games.GetProperty("slugs").GetInt32());

        var locales = games.GetProperty("locales");
        Assert.Equal(2, locales.GetProperty("en").GetProperty("present").GetInt32());
        Assert.Equal(1.0, locales.GetProperty("en").GetProperty("coverage").GetDouble());
        Assert.Equal(0, locales.GetProperty("is").GetProperty("present").GetInt32());
    }

    [Fact]
    public void Serialize_EmptyCollections_ProducesEmptyArray()
    {
        var report = new CoverageReport(DateTimeOffset.UtcNow, []);

        var json = ReportJson.Serialize(report);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(0, doc.RootElement.GetProperty("collections").GetArrayLength());
    }
}
