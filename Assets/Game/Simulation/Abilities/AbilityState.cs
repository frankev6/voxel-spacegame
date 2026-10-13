using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.3. Per-ship slot synchronization state; only writer: AbilitySystem.</summary>
    public struct AbilityState : IComponentData
    {
        public uint SyncedRevision;      // ShipStats.FunctionalRevision the slots were built from; 0 = never
        public byte SlotCount;
        public byte Reserved0;
        public ushort Reserved1;
    }
}
