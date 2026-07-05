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
          jonnxor-api verify [--offline | --live] [--content <dir>]
          jonnxor-api report [--json <path>]
          jonnxor-api --help

        Verbs:
          verify   Validate the content snapshot (--offline) or the snapshot against
                   a live Directus instance (--live). --content overrides the default
                   content directory (client/src/content, relative to the repo root).
          report   Emit a translation coverage report. --json writes a JSON artifact
                   to the given path in addition to the console table.
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

    private static int RunVerify(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var mode = args.FirstOrDefault(a => a is "--offline" or "--live");
        var contentDir = ReadOption(args, "--content") ?? DefaultContentDir;

        if (mode is null or "--live")
        {
            stderr.WriteLine(mode == "--live"
                ? "verify --live: not implemented"
                : "verify: specify --offline or --live");
            return 2;
        }

        return RunVerifyOffline(contentDir, stdout, stderr);
    }

    private static int RunVerifyOffline(string contentDir, TextWriter stdout, TextWriter stderr)
    {
        IReadOnlyList<SnapshotEntry> entries;
        try
        {
            entries = SnapshotReader.ReadAll(contentDir).ToList();
        }
        catch (DirectoryNotFoundException ex)
        {
            stderr.WriteLine($"verify --offline: {ex.Message}");
            return 2;
        }

        var findings = Verifier.Run(entries);

        if (findings.Count > 0)
        {
            foreach (var finding in findings)
            {
                stderr.WriteLine(finding.ToString());
            }

            stderr.WriteLine($"verify --offline: {findings.Count} finding(s) across {entries.Count} file(s)");
            return 1;
        }

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

    private static string? ReadOption(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int RunReport(string[] args, TextWriter stdout, TextWriter stderr)
    {
        stderr.WriteLine("report: not implemented");
        return 2;
    }

    private static int UnknownVerb(string verb, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown verb: '{verb}'");
        stderr.WriteLine(Usage);
        return 64;
    }
}
