namespace Game.Data.Definitions
{
    /// <summary>Function colours by role. Implements SPEC-80 §4.1 (DESIGN-SYSTEM §10.13, indices 20–25).</summary>
    [System.Serializable]
    public struct ArtFunctionColors
    {
        // BlockCategory (SPEC-04 §4.6, display only) → field: Propulsion→thruster, Weapon→weapon, Power→power, Shield→shield,
        // Core→core, Utility→utility; Structure and Detail have no function colour. Paint index: thruster 20, weapon 21, power 22, shield 23, core 24, utility 25.
        public UnityEngine.Color thruster, weapon, power, shield, core, utility;            // sRGB, alpha 1
        public float thrusterEmissive, weaponEmissive, powerEmissive, shieldEmissive, coreEmissive, utilityEmissive; // DESIGN-SYSTEM §10.16 units
    }
}
