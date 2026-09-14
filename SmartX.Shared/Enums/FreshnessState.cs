namespace SmartX.Shared;

/// <summary>
/// How recently a sensor last published a valid packet.
/// A disconnected device stays visible in the overview instead of disappearing.
/// </summary>
public enum FreshnessState
{
    Live = 0,
    Aging = 1,
    Stale = 2,
    Disconnected = 3
}
