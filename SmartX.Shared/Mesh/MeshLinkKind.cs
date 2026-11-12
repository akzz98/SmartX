namespace SmartX.Shared;

/// <summary>
/// Physical meaning of a wireless edge weight (used when explaining MST / hops).
/// </summary>
public enum MeshLinkKind
{
    /// <summary>Generic hop cost (lower is better).</summary>
    HopCost = 0,

    /// <summary>Derived from RSSI / signal quality (higher signal → lower cost).</summary>
    RssiDerived = 1,

    /// <summary>Estimated radio energy to forward a packet (lower is greener).</summary>
    PowerCost = 2
}
