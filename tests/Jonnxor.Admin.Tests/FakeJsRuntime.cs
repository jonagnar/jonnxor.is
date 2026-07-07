using Microsoft.JSInterop;

namespace Jonnxor.Admin.Tests;

/// <summary>
/// No-op <see cref="IJSRuntime"/> for render tests. Static HTML rendering never
/// reaches JS interop (the Config page only invokes it from user-event handlers),
/// but the page injects the service, so the container must be able to resolve one.
/// </summary>
internal sealed class FakeJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => ValueTask.FromResult(default(TValue)!);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier, CancellationToken cancellationToken, object?[]? args)
        => ValueTask.FromResult(default(TValue)!);
}
