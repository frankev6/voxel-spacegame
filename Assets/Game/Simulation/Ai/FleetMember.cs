using Unity.Entities;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.4: FleetMember.</summary>
    public struct FleetMember : Unity.Entities.IComponentData
    {
        public Unity.Entities.Entity Leader;
        public byte Slot;                // index into the formation's slot list
        public ushort FormationIndex;    // FormationDefinition index
    }
}
