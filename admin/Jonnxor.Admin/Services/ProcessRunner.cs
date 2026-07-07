using System.ComponentModel;
using System.Diagnostics;

namespace Jonnxor.Admin.Services;

/// <summary>
/// Real <see cref="IProcessRunner"/> on <see cref="Process"/> with redirected
/// stdout/stderr. Line delivery is serialized through one lock so
/// <c>onLine</c> callbacks never observe the two stream pumps concurrently.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(
        string fileName, string[] args, string workingDir, Action<string> onLine, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start process '{fileName}': {ex.Message}", ex);
        }

        var gate = new object();
        void Emit(string line)
        {
            lock (gate)
            {
                onLine(line);
            }
        }

        var stdoutPump = PumpAsync(process.StandardOutput, Emit);
        var stderrPump = PumpAsync(process.StandardError, line => Emit("! " + line));

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited between the cancellation and the kill.
            }

            await Task.WhenAll(stdoutPump, stderrPump);
            throw;
        }

        // The process has exited, but the pipes may still hold buffered output —
        // drain both pumps before surfacing the exit code.
        await Task.WhenAll(stdoutPump, stderrPump);
        return process.ExitCode;
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            onLine(line);
        }
    }
}
