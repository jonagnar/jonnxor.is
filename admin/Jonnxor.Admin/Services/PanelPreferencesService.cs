using System.Text.Json;

namespace Jonnxor.Admin.Services;

/// <summary>
/// Operator-local panel preferences — the only editable state in admin v1.
/// <see cref="Theme"/> is one of the site's three themes; <see cref="TimestampLocale"/>
/// formats the ABSOLUTE timestamps the panel shows (tiles' refreshed-at); relative
/// strings ("3 h ago") stay invariant by design (<see cref="RelativeTime"/>).
/// </summary>
public sealed record PanelPreferences(string Theme, string TimestampLocale)
{
    /// <summary>The site's three themes (tokens.css vocabulary).</summary>
    public static readonly IReadOnlyList<string> Themes = ["dawn", "rune", "neon"];

    /// <summary>The site's three locales as full culture names.</summary>
    public static readonly IReadOnlyList<string> TimestampLocales = ["is-IS", "en-GB", "ja-JP"];

    /// <summary>
    /// rune is the site's default theme (what App.razor hardcoded before this pref
    /// existed); is-IS matches the site's defaultLocale.
    /// </summary>
    public static PanelPreferences Default { get; } = new("rune", "is-IS");
}

/// <summary>
/// Persists <see cref="PanelPreferences"/> as JSON under the OS app-data dir
/// (<c>{ApplicationData}/jonnxor-admin/preferences.json</c>) — operator state, never
/// the repo. Load degrades like the sibling services: a missing file is a normal
/// first run (defaults, no warning, nothing written until the first Save); a corrupt
/// or partially-invalid file coerces to defaults with <see cref="LoadWarning"/> as
/// data for the Config page. Saves are atomic (unique temp file + rename in the same
/// directory). Thread-safety: one operator, but two tabs share this singleton and
/// could save concurrently — a plain lock around save is all that scenario needs.
/// </summary>
public sealed class PanelPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly object _gate = new();
    private readonly string _path;

    /// <param name="baseDirectory">
    /// Directory the <c>jonnxor-admin/</c> folder lives under, injectable for tests
    /// (the RepoPaths startDirectory pattern). Null means
    /// <see cref="Environment.SpecialFolder.ApplicationData"/>.
    /// </param>
    public PanelPreferencesService(string? baseDirectory = null)
    {
        _path = Path.Combine(
            baseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "jonnxor-admin", "preferences.json");
        (Current, LoadWarning) = Load(_path);
    }

    /// <summary>The effective preferences — always valid values, never null.</summary>
    public PanelPreferences Current { get; private set; }

    /// <summary>
    /// Why <see cref="Current"/> is (partly) defaults instead of the stored file,
    /// or null when the load was clean. Cleared by the next successful Save.
    /// </summary>
    public string? LoadWarning { get; private set; }

    /// <summary>
    /// Validates, persists atomically, and updates <see cref="Current"/>. Throws
    /// <see cref="ArgumentException"/> on values outside the allowed sets (the UI
    /// only offers valid options; anything else is a programming error) — the
    /// current state is untouched on any failure.
    /// </summary>
    public void Save(PanelPreferences preferences)
    {
        if (!PanelPreferences.Themes.Contains(preferences.Theme))
        {
            throw new ArgumentException(
                $"unknown theme '{preferences.Theme}' — expected one of: "
                + string.Join(", ", PanelPreferences.Themes),
                nameof(preferences));
        }

        if (!PanelPreferences.TimestampLocales.Contains(preferences.TimestampLocale))
        {
            throw new ArgumentException(
                $"unknown timestamp locale '{preferences.TimestampLocale}' — expected one of: "
                + string.Join(", ", PanelPreferences.TimestampLocales),
                nameof(preferences));
        }

        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);

            // Atomic write: a unique temp name (two saves racing across processes
            // must not share one), then rename — readers only ever see a complete file.
            var temp = Path.Combine(directory, $".preferences-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(preferences, JsonOptions));
                File.Move(temp, _path, overwrite: true);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }

            Current = preferences;
            LoadWarning = null;
        }
    }

    private static (PanelPreferences Preferences, string? Warning) Load(string path)
    {
        string text;
        try
        {
            if (!File.Exists(path))
            {
                return (PanelPreferences.Default, null); // normal first run
            }

            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (PanelPreferences.Default,
                $"could not read preferences file '{path}': {ex.Message} — using defaults");
        }

        StoredPreferences? stored;
        try
        {
            stored = JsonSerializer.Deserialize<StoredPreferences>(text, JsonOptions);
        }
        catch (JsonException ex)
        {
            return (PanelPreferences.Default,
                $"preferences file '{path}' is not valid JSON ({ex.Message}) — using defaults");
        }

        // Per-field coercion: an unknown value falls back alone, keeping the other
        // field's stored choice, and the warning names what was rejected.
        var problems = new List<string>();
        var theme = Coerce(
            stored?.Theme, PanelPreferences.Themes, PanelPreferences.Default.Theme,
            "theme", problems);
        var locale = Coerce(
            stored?.TimestampLocale, PanelPreferences.TimestampLocales,
            PanelPreferences.Default.TimestampLocale, "timestamp locale", problems);

        return (new PanelPreferences(theme, locale),
            problems.Count > 0
                ? $"preferences file '{path}': {string.Join("; ", problems)} — using defaults for those"
                : null);
    }

    private static string Coerce(
        string? value, IReadOnlyList<string> allowed, string fallback,
        string label, List<string> problems)
    {
        if (value is not null && allowed.Contains(value))
        {
            return value;
        }

        problems.Add($"unknown {label} '{value ?? "(missing)"}'");
        return fallback;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best-effort cleanup of the temp file; the throw in flight matters more
        }
    }

    /// <summary>Lenient on-disk shape: nullable so hand-edited/partial files coerce, not crash.</summary>
    private sealed record StoredPreferences(string? Theme, string? TimestampLocale);
}
