namespace Jonnxor.Api.Verification;

/// <summary>One rule violation: which rule, which file, and human-readable detail.</summary>
public sealed record Finding(string Rule, string File, string Detail)
{
    public override string ToString() => $"[{Rule}] {File}: {Detail}";
}
