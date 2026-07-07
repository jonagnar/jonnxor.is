using Jonnxor.Api.Directus;
using Jonnxor.Api.Equivalence;
using Jonnxor.Api.Snapshot;
using Jonnxor.Api.Verification;

namespace Jonnxor.Api;

/// <summary>
/// Top-level CLI dispatcher. Kept as a plain static method (no CLI parsing library) so the
/// verb surface stays trivial to read and test — see the phase report for the rationale.
/// </summary>
public static class CliRunner
{
    private const string Usage = """
        jonnxor-api — content seam validator

        Usage:
          jonnxor-api verify --offline [--content <dir>]
          jonnxor-api verify --live [--content <dir>] [--env <path>]
          jonnxor-api report [--content <dir>] [--json <path>]
          jonnxor-api --help

        Verbs:
          verify   Validate the content snapshot (--offline) or the snapshot against
                   a live Directus instance (--live, read-only). --content overrides
                   the default content directory (client/src/content, relative to the
                   current working directory). --live always runs the offline rules
                   first and fails fast on any offline finding before touching
                   Directus. --live credentials come from DIRECTUS_URL/ADMIN_EMAIL/
                   ADMIN_PASSWORD — either already exported, or loaded from a
                   KEY=VALUE file via --env (explicit env vars win over the file).
          report   Emit a per-locale translation coverage table from the snapshot
                   alone (offline, no Directus). --content overrides the default
                   content directory. --json writes the same data as a JSON
                   artifact to the given path, in addition to the console table.
        """;

    /// <summary>Console-backed convenience overload for Program.cs.</summary>
    public static int Run(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            stdout.WriteLine(Usage);
            return 0;
        }

        var verb = args[0];
        var rest = args[1..];

        return verb switch
        {
            "verify" => RunVerify(rest, stdout, stderr),
            "report" => RunReport(rest, stdout, stderr),
            _ => UnknownVerb(verb, stderr),
        };
    }

    private const string DefaultContentDir = "client/src/content";

    // The collections the equivalence comparator knows about — mirrors the `name` entries in
    // `client/scripts/lib/collections.mjs`'s COLLECTIONS table (the descriptor table remains
    // the single owner of per-field kind knowledge; this list only says WHICH collections
    // exist, which the generic comparator needs to know what to fetch from Directus).
    private static readonly string[] LiveCollections =
        ["blog", "grimoire", "games", "pages", "countdowns", "wallpapers", "projects"];

    private static int RunVerify(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var mode = args.FirstOrDefault(a => a is "--offline" or "--live");
        var contentDir = ReadOption(args, "--content") ?? DefaultContentDir;

        if (mode is null)
        {
            stderr.WriteLine("verify: specify --offline or --live");
            return 2;
        }

        IReadOnlyList<SnapshotEntry> entries;
        try
        {
            entries = SnapshotReader.ReadAll(contentDir).ToList();
        }
        catch (DirectoryNotFoundException ex)
        {
            stderr.WriteLine($"verify {mode}: {ex.Message}");
            return 2;
        }

        var offlineFindings = Verifier.Run(entries);
        if (offlineFindings.Count > 0)
        {
            foreach (var finding in offlineFindings)
            {
                stderr.WriteLine(finding.ToString());
            }

            stderr.WriteLine($"verify {mode}: {offlineFindings.Count} offline finding(s) across {entries.Count} file(s)");
            return 1;
        }

        return mode == "--live"
            ? RunVerifyLive(args, contentDir, entries, stdout, stderr)
            : PrintOfflineSummary(contentDir, entries, stdout);
    }

    private static int PrintOfflineSummary(string contentDir, IReadOnlyList<SnapshotEntry> entries, TextWriter stdout)
    {
        var slugCount = entries.Select(e => (e.Collection, e.Slug)).Distinct().Count();
        var collectionCount = entries.Select(e => e.Collection).Distinct().Count();

        stdout.WriteLine("verify --offline: OK");
        stdout.WriteLine($"  content dir : {contentDir}");
        stdout.WriteLine($"  collections : {collectionCount}");
        stdout.WriteLine($"  slugs       : {slugCount}");
        stdout.WriteLine($"  files       : {entries.Count}");
        stdout.WriteLine($"  rules passed: {Verifier.Rules.Count} ({string.Join(", ", Verifier.Rules.Select(r => r.GetType().Name))})");
        return 0;
    }

    private static int RunVerifyLive(
        string[] args, string contentDir, IReadOnlyList<SnapshotEntry> entries, TextWriter stdout, TextWriter stderr)
    {
        var envPath = ReadOption(args, "--env");
        IReadOnlyDictionary<string, string> fileValues = new Dictionary<string, string>();
        if (envPath is not null)
        {
            try
            {
                fileValues = EnvFile.Load(envPath);
            }
            catch (IOException ex)
            {
                stderr.WriteLine($"verify --live: could not read --env file '{envPath}': {ex.Message}");
                return 2;
            }
        }

        string? Resolve(string key) =>
            Environment.GetEnvironmentVariable(key) is { Length: > 0 } fromEnv
                ? fromEnv
                : fileValues.GetValueOrDefault(key);

        var directusUrl = Resolve("DIRECTUS_URL");
        var adminEmail = Resolve("ADMIN_EMAIL");
        var adminPassword = Resolve("ADMIN_PASSWORD");

        var missing = new[] { ("DIRECTUS_URL", directusUrl), ("ADMIN_EMAIL", adminEmail), ("ADMIN_PASSWORD", adminPassword) }
            .Where(kv => string.IsNullOrEmpty(kv.Item2))
            .Select(kv => kv.Item1)
            .ToList();
        if (missing.Count > 0)
        {
            stderr.WriteLine(
                $"verify --live: missing {string.Join(", ", missing)} — set as environment variable(s) or pass --env <path> to a KEY=VALUE file that defines them");
            return 2;
        }

        return RunVerifyLiveAsync(directusUrl!, adminEmail!, adminPassword!, contentDir, entries, stdout, stderr)
            .GetAwaiter().GetResult();
    }

    private static async Task<int> RunVerifyLiveAsync(
        string directusUrl, string adminEmail, string adminPassword,
        string contentDir, IReadOnlyList<SnapshotEntry> entries, TextWriter stdout, TextWriter stderr)
    {
        DirectusClient client;
        try
        {
            client = new DirectusClient(directusUrl);
        }
        catch (DirectusUnreachableException ex)
        {
            stderr.WriteLine($"verify --live: {ex.Message}");
            return 2;
        }

        using (client)
        {
            try
            {
                await client.LoginAsync(adminEmail, adminPassword);
            }
            catch (DirectusUnreachableException ex)
            {
                stderr.WriteLine($"verify --live: {ex.Message}");
                return 2;
            }

            var findings = new List<Finding>();
            var collectionsChecked = 0;

            foreach (var collection in LiveCollections)
            {
                IReadOnlyList<System.Text.Json.JsonElement> rawItems;
                try
                {
                    rawItems = await client.GetItemsAsync(collection);
                }
                catch (DirectusUnreachableException ex)
                {
                    stderr.WriteLine($"verify --live: {ex.Message}");
                    return 2;
                }

                List<Equivalence.DirectusItem> directusItems;
                try
                {
                    directusItems = rawItems.Select(raw => Equivalence.DirectusItem.FromJson(raw, collection)).ToList();
                }
                catch (InvalidOperationException ex)
                {
                    stderr.WriteLine($"verify --live: Directus collection '{collection}' returned malformed item data: {ex.Message}");
                    return 2;
                }
                catch (ArgumentException ex)
                {
                    stderr.WriteLine($"verify --live: Directus collection '{collection}' returned malformed item data: {ex.Message}");
                    return 2;
                }

                var snapshotEntries = entries.Where(e => e.Collection == collection).ToList();

                IReadOnlyList<Finding> collectionFindings;
                try
                {
                    collectionFindings = EquivalenceComparer.Compare(collection, snapshotEntries, directusItems);
                }
                catch (ArgumentException ex)
                {
                    stderr.WriteLine($"verify --live: Directus collection '{collection}' returned malformed item data: {ex.Message}");
                    return 2;
                }

                findings.AddRange(collectionFindings);
                collectionsChecked++;
            }

            if (findings.Count > 0)
            {
                foreach (var finding in findings)
                {
                    stderr.WriteLine(finding.ToString());
                }

                stderr.WriteLine($"verify --live: {findings.Count} finding(s) across {collectionsChecked} collection(s)");
                return 1;
            }

            stdout.WriteLine("verify --live: OK");
            stdout.WriteLine($"  content dir : {contentDir}");
            stdout.WriteLine($"  directus    : {directusUrl}");
            stdout.WriteLine($"  collections : {collectionsChecked} ({string.Join(", ", LiveCollections)})");
            stdout.WriteLine("  offline rules passed, snapshot equivalent to Directus for every collection above");
            return 0;
        }
    }

    private static string? ReadOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int RunReport(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var contentDir = ReadOption(args, "--content") ?? DefaultContentDir;
        var jsonPath = ReadOption(args, "--json");

        IReadOnlyList<SnapshotEntry> entries;
        try
        {
            entries = SnapshotReader.ReadAll(contentDir).ToList();
        }
        catch (DirectoryNotFoundException ex)
        {
            stderr.WriteLine($"report: {ex.Message}");
            return 2;
        }

        var report = Report.CoverageReporter.Build(entries, DateTimeOffset.UtcNow);

        Report.ReportTable.Write(report, stdout);

        if (jsonPath is not null)
        {
            try
            {
                File.WriteAllText(jsonPath, Report.ReportJson.Serialize(report));
            }
            catch (IOException ex)
            {
                stderr.WriteLine($"report: could not write --json artifact '{jsonPath}': {ex.Message}");
                return 2;
            }

            stdout.WriteLine($"  json artifact written to {jsonPath}");
        }

        return 0;
    }

    private static int UnknownVerb(string verb, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown verb: '{verb}'");
        stderr.WriteLine(Usage);
        return 64;
    }
}
