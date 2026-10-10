namespace Game.Data.Combat
{
    /// <summary>Who produced a simulation event; part of every deterministic sort key (architecture §5.1). One enum for every channel
    /// (damage, explosions, shield hits, impulses, restores, inhibits). Add values at the end only. Implements SPEC-02 §4.12.</summary>
    public enum ProducerKind : byte
    { Unknown = 0, Impact = 1, Projectile = 2, Beam = 3, Explosion = 4, ShieldOverflow = 5, Ability = 6, Status = 7, Repair = 8, Environment = 9, MainThread = 10, Test = 255 }
}
