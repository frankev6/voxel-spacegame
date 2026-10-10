namespace Game.Data.Blocks
{
    /// <summary>Thruster row: thrust along block-local −Z and power draw at full throttle. Unmanaged; Burst reads it. Implements SPEC-04 §4.6.</summary>
    [System.Serializable] public struct ThrusterParams { public float ThrustNewtons; public float PowerDraw; }   // > 0 along block-local −Z; E/s at full throttle, ≥ 0
}
