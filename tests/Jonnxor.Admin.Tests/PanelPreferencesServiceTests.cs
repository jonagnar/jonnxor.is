using Jonnxor.Admin.Services;

namespace Jonnxor.Admin.Tests;

public class PanelPreferencesServiceTests
{
    [Fact]
    public void FirstRun_NoFile_LoadsDefaultsWithoutWarning()
    {
        using var temp = new TempDir();

        var service = new PanelPreferencesService(temp.Path);

        Assert.Equal(PanelPreferences.Default, service.Current);
        Assert.Null(service.LoadWarning);
        // First run must not write anything — the file appears on the first Save.
        Assert.False(File.Exists(Path.Combine(temp.Path, "jonnxor-admin", "preferences.json")));
    }

    [Fact]
    public void SaveThenReload_RoundTripsAcrossInstances()
    {
        using var temp = new TempDir();
        new PanelPreferencesService(temp.Path).Save(new PanelPreferences("neon", "ja-JP"));

        var reloaded = new PanelPreferencesService(temp.Path);

        Assert.Equal(new PanelPreferences("neon", "ja-JP"), reloaded.Current);
        Assert.Null(reloaded.LoadWarning);
    }

    [Fact]
    public void Save_UpdatesCurrentInPlace()
    {
        using var temp = new TempDir();
        var service = new PanelPreferencesService(temp.Path);

        service.Save(new PanelPreferences("dawn", "is-IS"));

        Assert.Equal(new PanelPreferences("dawn", "is-IS"), service.Current);
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        using var temp = new TempDir();
        var service = new PanelPreferencesService(temp.Path);

        service.Save(new PanelPreferences("neon", "en-GB"));
        service.Save(new PanelPreferences("dawn", "en-GB"));

        var dir = Path.Combine(temp.Path, "jonnxor-admin");
        var file = Assert.Single(Directory.GetFiles(dir));
        Assert.Equal("preferences.json", Path.GetFileName(file));
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultsWithWarning()
    {
        using var temp = new TempDir();
        temp.WriteFile("jonnxor-admin/preferences.json", "{ this is not json");

        var service = new PanelPreferencesService(temp.Path);

        Assert.Equal(PanelPreferences.Default, service.Current);
        Assert.NotNull(service.LoadWarning);
    }

    [Fact]
    public void UnknownStoredValues_CoercePerFieldToDefaultsWithWarning()
    {
        using var temp = new TempDir();
        temp.WriteFile(
            "jonnxor-admin/preferences.json",
            """{"Theme":"solarized","TimestampLocale":"ja-JP"}""");

        var service = new PanelPreferencesService(temp.Path);

        // The invalid theme falls back; the valid locale survives.
        Assert.Equal(PanelPreferences.Default.Theme, service.Current.Theme);
        Assert.Equal("ja-JP", service.Current.TimestampLocale);
        Assert.NotNull(service.LoadWarning);
        Assert.Contains("solarized", service.LoadWarning);
    }

    [Fact]
    public void Save_AfterCorruptLoad_ClearsTheWarning()
    {
        using var temp = new TempDir();
        temp.WriteFile("jonnxor-admin/preferences.json", "not json");
        var service = new PanelPreferencesService(temp.Path);
        Assert.NotNull(service.LoadWarning);

        service.Save(new PanelPreferences("rune", "en-GB"));

        Assert.Null(service.LoadWarning);
        Assert.Null(new PanelPreferencesService(temp.Path).LoadWarning);
    }

    [Theory]
    [InlineData("solarized", "en-GB")]
    [InlineData("rune", "en-US")]
    [InlineData("", "en-GB")]
    public void Save_RejectsValuesOutsideTheAllowedSets(string theme, string locale)
    {
        using var temp = new TempDir();
        var service = new PanelPreferencesService(temp.Path);

        Assert.Throws<ArgumentException>(() => service.Save(new PanelPreferences(theme, locale)));
        // A rejected save must not clobber the current state.
        Assert.Equal(PanelPreferences.Default, service.Current);
    }
}
