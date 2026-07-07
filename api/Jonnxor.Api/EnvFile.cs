namespace Jonnxor.Api;

/// <summary>
/// Minimal `.env` file reader: `KEY=VALUE` lines, blank lines and `#`-prefixed comment lines
/// skipped, no quoting/escaping/export handling. Matches the shape of `directus/.env`
/// (consumed client-side by the Node scripts via `node --env-file`) closely enough for this
/// read-only CLI's needs — it is not a general-purpose dotenv implementation.
/// </summary>
public static class EnvFile
{
    /// <summary>
    /// Parses `KEY=VALUE` lines from <paramref name="text"/> into a dictionary. Later
    /// duplicate keys win. Lines without an `=` are ignored.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>Reads and parses the file at <paramref name="path"/>.</summary>
    public static IReadOnlyDictionary<string, string> Load(string path) => Parse(File.ReadAllText(path));
}
