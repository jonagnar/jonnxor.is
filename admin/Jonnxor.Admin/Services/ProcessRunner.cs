using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Jonnxor.Admin.Services;

/// <summary>
/// Real <see cref="IProcessRunner"/> on <see cref="Process"/> with redirected
/// stdout/stderr. Line delivery is serialized through one lock so
/// <c>onLine</c> callbacks never observe the two stream pumps concurrently.
/// A throwing callback must not stall the run: a faulted pump would stop reading
/// its pipe, a chatty child would block on the full pipe buffer and never exit,
/// and the exit await would deadlock — so the first callback exception is
/// latched, the process tree is killed, both pipes drain to EOF, and the latched
/// exception is rethrown only once the process is gone.
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
        ExceptionDispatchInfo? callbackFault = null;
        void Emit(string line)
        {
            lock (gate)
            {
                if (callbackFault is not null)
                {
                    // Already abandoning the run: keep draining, stop forwarding.
                    return;
                }

                try
                {
                    onLine(line);
                }
                catch (Exception ex)
                {
                    callbackFault = ExceptionDispatchInfo.Capture(ex);
                    // The run is abandoned — kill the tree so both pipes reach
                    // EOF and the exit await below completes promptly.
                    KillTree(process);
                }
            }
        }

        // The ct doubles as the drain escape hatch on the cancel path: a killed
        // child's surviving descendant can keep the pipe write handle open, which
        // would otherwise leave ReadLineAsync pending forever.
        var stdoutPump = PumpAsync(process.StandardOutput, Emit, ct);
        var stderrPump = PumpAsync(process.StandardError, line => Emit("! " + line), ct);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation must surface as OperationCanceledException no matter
            // what the kill or the abandoned pumps throw.
            KillTree(process);
            await WaitQuietlyAsync(stdoutPump, stderrPump).ConfigureAwait(false);
            throw;
        }

        // The process has exited, but the pipes may still hold buffered output —
        // drain both pumps fully before deciding the outcome: a callback can
        // still fault on those buffered lines.
        Exception? drainError = null;
        try
        {
            await Task.WhenAll(stdoutPump, stderrPump).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // A killed-run drain can fault (broken pipe); a latched callback
            // exception must win over it, so decide below instead of here.
            drainError = ex;
        }

        ExceptionDispatchInfo? fault;
        lock (gate)
        {
            fault = callbackFault;
        }

        fault?.Throw();
        if (drainError is not null)
        {
            ExceptionDispatchInfo.Capture(drainError).Throw();
        }

        return process.ExitCode;
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            onLine(line);
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Already exited, or the tree is unkillable — either way the pending
            // primary exception, not this one, is the story.
        }
    }

    /// <summary>Awaits both pumps on an abandoned run, discarding whatever they throw.</summary>
    private static async Task WaitQuietlyAsync(Task stdoutPump, Task stderrPump)
    {
        try
        {
            await Task.WhenAll(stdoutPump, stderrPump).ConfigureAwait(false);
        }
        catch
        {
            // Abandoned-run drain: pump faults must not mask the primary exception.
        }
    }
}
