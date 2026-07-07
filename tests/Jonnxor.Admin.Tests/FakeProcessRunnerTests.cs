namespace Jonnxor.Admin.Tests;

public class FakeProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ReplaysScriptedLinesAndExitCode()
    {
        var runner = new FakeProcessRunner()
            .Expect("git", ["status", "--porcelain"], ["M file.txt", "! warning"], 3);
        var lines = new List<string>();

        var exitCode = await runner.RunAsync(
            "git", ["status", "--porcelain"], "/repo", lines.Add, CancellationToken.None);

        Assert.Equal(3, exitCode);
        Assert.Equal(new[] { "M file.txt", "! warning" }, lines);
    }

    [Fact]
    public async Task RunAsync_EnforcesScriptOrderAcrossInvocations()
    {
        var runner = new FakeProcessRunner()
            .Expect("git", ["fetch"], [], 0)
            .Expect("git", ["status"], [], 0);

        Assert.Equal(0, await runner.RunAsync("git", ["fetch"], "/repo", _ => { }, CancellationToken.None));
        Assert.Equal(0, await runner.RunAsync("git", ["status"], "/repo", _ => { }, CancellationToken.None));
        runner.VerifyAllConsumed();
    }

    [Fact]
    public async Task RunAsync_MismatchNamesExpectedAndReceived()
    {
        var runner = new FakeProcessRunner()
            .Expect("pnpm", ["content:pull"], [], 0);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync("git", ["status"], "/repo", _ => { }, CancellationToken.None));

        Assert.Contains("pnpm content:pull", ex.Message);
        Assert.Contains("git status", ex.Message);
    }

    [Fact]
    public async Task RunAsync_UnexpectedInvocationPastEndOfScriptThrows()
    {
        var runner = new FakeProcessRunner();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync("git", ["status"], "/repo", _ => { }, CancellationToken.None));

        Assert.Contains("git status", ex.Message);
        Assert.Contains("no more entries", ex.Message);
    }

    [Fact]
    public async Task RunAsync_RecordsInvocationsIncludingWorkingDir()
    {
        var runner = new FakeProcessRunner()
            .Expect("pnpm", ["content:pull"], [], 0);

        await runner.RunAsync("pnpm", ["content:pull"], "/repo/client", _ => { }, CancellationToken.None);

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("pnpm", invocation.FileName);
        Assert.Equal("/repo/client", invocation.WorkingDir);
    }

    [Fact]
    public void VerifyAllConsumed_ThrowsListingTheRemainingScript()
    {
        var runner = new FakeProcessRunner()
            .Expect("git", ["diff"], [], 0);

        var ex = Assert.Throws<InvalidOperationException>(runner.VerifyAllConsumed);

        Assert.Contains("git diff", ex.Message);
    }
}
