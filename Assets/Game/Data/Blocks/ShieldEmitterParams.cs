namespace Game.Data.Blocks
{
    /// <summary>Shield emitter row. Unmanaged; Burst reads it. Implements SPEC-04 §4.6.</summary>
    [System.Serializable] public struct ShieldEmitterParams
    { public float Capacity; public float RegenPerSecond; public float RechargeDelaySeconds; public float PowerDraw; }   // > 0, ≥ 0, ≥ 0, ≥ 0
}
