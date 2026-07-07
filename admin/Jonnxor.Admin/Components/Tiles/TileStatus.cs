namespace Jonnxor.Admin.Components.Tiles;

/// <summary>
/// The four states a dashboard health tile can render. <see cref="Unknown"/> covers
/// every "cannot tell" case — degraded services, thrown loads, git failures — and is
/// deliberately distinct from <see cref="Fail"/>: unknown means "no verdict", never
/// a false alarm and never a false all-clear.
/// </summary>
public enum TileStatus
{
    Unknown,
    Ok,
    Warn,
    Fail,
}
