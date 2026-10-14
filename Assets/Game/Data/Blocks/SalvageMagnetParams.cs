namespace Game.Data.Blocks
{
    /// <summary>QA-001: metres added to the ship's salvage reach per intact magnet (summed, capped by the economy config).</summary>
    [System.Serializable] public struct SalvageMagnetParams { public float ReachBonusM; }                   // > 0
}
