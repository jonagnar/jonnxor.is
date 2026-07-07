using Jonnxor.Admin.Services;

namespace Jonnxor.Admin.Tests;

/// <summary>
/// Scripted <see cref="IProcessRunner"/> double: tests enqueue the invocations they
/// expect, in order, via <see cref="Expect"/>; any deviation — wrong command, wrong
/// args, or an invocation past the end of the script — fails with a message naming
/// expected vs received. Working dirs are recorded on <see cref="Invocations"/> for
/// callers that need to assert them.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public sealed record Invocation(string FileName, string[] Args, string WorkingDir);

    private sealed record Scripted(string FileName, string[] Args, string[] Lines, int ExitCode);

    private readonly Queue<Scripted> _script = new();
    private readonly List<Invocation> _invocations = [];

    /// <summary>Every invocation received, in order.</summary>
    public IReadOnlyList<Invocation> Invocations => _invocations;

    /// <summary>Appends one expected invocation to the script; chainable.</summary>
    public FakeProcessRunner Expect(string fileName, string[] args, string[] lines, int exitCode)
    {
        _script.Enqueue(new Scripted(fileName, args, lines, exitCode));
        return this;
    }

    public Task<int> RunAsync(
        string fileName, string[] args, string workingDir, Action<string> onLine, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _invocations.Add(new Invocation(fileName, args, workingDir));

        if (_script.Count == 0)
        {
            throw new InvalidOperationException(
                $"FakeProcessRunner: unexpected invocation {Describe(fileName, args)} — the script has no more entries.");
        }

        var expected = _script.Dequeue();
        if (!string.Equals(expected.FileName, fileName, StringComparison.Ordinal)
            || !expected.Args.SequenceEqual(args, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "FakeProcessRunner: invocation mismatch."
                + $"\n  expected: {Describe(expected.FileName, expected.Args)}"
                + $"\n  received: {Describe(fileName, args)}");
        }

        foreach (var line in expected.Lines)
        {
            onLine(line);
        }

        return Task.FromResult(expected.ExitCode);
    }

    /// <summary>Fails if any scripted invocation was never consumed.</summary>
    public void VerifyAllConsumed()
    {
        if (_script.Count > 0)
        {
            var remaining = string.Join(", ", _script.Select(s => Describe(s.FileName, s.Args)));
            throw new InvalidOperationException(
                $"FakeProcessRunner: {_script.Count} scripted invocation(s) never ran: {remaining}");
        }
    }

    private static string Describe(string fileName, string[] args)
        => args.Length == 0 ? $"`{fileName}`" : $"`{fileName} {string.Join(' ', args)}`";
}
