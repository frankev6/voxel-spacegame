namespace Game.Voxel.Damage
{
    /// <summary>Request to push a body (ADR-0016): tractor beams, launch catapults, gravity wells. Applied by ImpactEventSystem (§5.2 step 1b).</summary>
    [System.Flags] public enum ImpulseRequestFlags : byte { None = 0, AtCenterOfMass = 1 /* WorldPoint ignored: no angular part */ }
}
