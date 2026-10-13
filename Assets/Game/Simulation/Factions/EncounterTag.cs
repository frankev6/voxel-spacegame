namespace Game.Simulation.Factions
{
    /// <summary>SPEC-33 §4.2 instance attribution on encounter roots; assigned by ENC-007.</summary>
    public struct EncounterTag : Unity.Entities.IComponentData { public Game.Core.ContentHash Encounter; public uint Instance; }
}
