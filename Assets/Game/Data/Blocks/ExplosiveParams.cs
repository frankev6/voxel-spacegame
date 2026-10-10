namespace Game.Data.Blocks
{
    /// <summary>Explosive row: ExplosionRequest when the block is destroyed (SPEC-13). Unmanaged; Burst reads it. Implements SPEC-04 §4.6.</summary>
    [System.Serializable] public struct ExplosiveParams { public float BlastRadiusCells; public float BlastDamage; }   // > 0 each
}
