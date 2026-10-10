namespace Game.Data.Definitions
{
    /// <summary>FxEvent.Color per DamageType (architecture §6.3/§6.6). Implements SPEC-80 §4.1.</summary>
    [System.Serializable]
    public struct ArtDamageTypeColors { public UnityEngine.Color kinetic, energy, explosive, ram; }
}
