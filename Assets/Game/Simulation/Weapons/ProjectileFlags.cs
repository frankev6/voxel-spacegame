namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: ProjectileFlags.</summary>
    [System.Flags] public enum ProjectileFlags : byte { None = 0, Homing = 1, ExplodeOnExpire = 2, Dead = 4, PlayerSource = 8, Targetable = 16 /* reserved for point defence (§10 Q10); unused in v1 */ }
}
