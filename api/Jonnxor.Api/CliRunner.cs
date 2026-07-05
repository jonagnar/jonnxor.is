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
                   content directory.
          report   Emit a translation coverage report. --json writes a JSON artifact
                   to the given path in addition to the console table.
        """;

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            Console.WriteLine(Usage);
            return 0;
        }

        var verb = args[0];
        var rest = args[1..];

        return verb switch
        {
            "verify" => RunVerify(rest),
            "report" => RunReport(rest),
            _ => UnknownVerb(verb),
        };
    }

    private static int RunVerify(string[] args)
    {
        Console.Error.WriteLine("verify: not implemented");
        return 2;
    }

    private static int RunReport(string[] args)
    {
        Console.Error.WriteLine("report: not implemented");
        return 2;
    }

    private static int UnknownVerb(string verb)
    {
        Console.Error.WriteLine($"Unknown verb: '{verb}'");
        Console.Error.WriteLine(Usage);
        return 64;
    }
}
