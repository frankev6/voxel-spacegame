namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-63 §4; shared content boundary per ADR-0050.</summary>
    public enum FxKind : byte
    {
        None = 0,
        Debris = 1,          // event: one voxel cube (removed cell or tier-1 island cell)
        HitKinetic = 2,      // event: sparks + flash on a hull hit by kinetic/explosive projectiles
        HitEnergy = 3,       // event: energy splash on a hull hit by energy projectiles or beams
        ExplosionSmall = 4,  // event: blast with radius < 4 m
        ExplosionLarge = 5,  // event: blast with radius >= 4 m (pooled hero effect)
        ShieldRipple = 6,    // event: sparks at a shield impact point (the shell ripple itself comes from SPEC-22 state)
        Beam = 7,            // state: beam look (never sent as an event)
        Muzzle = 8,          // event: muzzle flash
        Thruster = 9,        // state: thruster plume look (never sent as an event)
        Warp = 10,           // event: lane-jump departure/arrival (pooled hero effect)
        Tracer = 11,         // state: projectile tracer look (never sent as an event)
        ShieldShell = 12,    // state: shield shell look (never sent as an event)
        RamImpact = 13,      // collision contact/energy event (SPEC-13 §5.2, ADR-0058)
    }
}
