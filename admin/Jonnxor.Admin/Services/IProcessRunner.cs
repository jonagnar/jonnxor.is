namespace Jonnxor.Admin.Services;

/// <summary>
/// The one seam every shell-out (pnpm, git) goes through, so services above it are
/// testable against a scripted fake instead of real processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with <paramref name="args"/> in
    /// <paramref name="workingDir"/> and returns its exit code. A non-zero exit is
    /// data, never an exception. Stdout and stderr are merged line-by-line into
    /// <paramref name="onLine"/> as they arrive, stderr lines prefixed <c>"! "</c>;
    /// both streams are fully drained before the task completes. Throws
    /// <see cref="InvalidOperationException"/> naming <paramref name="fileName"/>
    /// when the process cannot be spawned (missing binary); cancellation kills the
    /// entire process tree.
    /// </summary>
    Task<int> RunAsync(
        string fileName, string[] args, string workingDir, Action<string> onLine, CancellationToken ct);
}
