namespace Game.Data.Combat
{
    /// <summary>Implements SPEC-22 §4.2. Add values at the end only (at most 15 values).</summary>
    public enum StatId : byte
    {
        Thrust = 0,
        MaxSpeed = 1,
        FireRate = 2,
        WeaponDamage = 3,
        ShieldRegen = 4,
        TurnRate = 5
    }
}
