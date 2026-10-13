using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1. One power-consuming module of a functional entity (ADR-0014). One element per consuming module of the block.
    /// Writers, per element: the feature that owns `Module` (Draw and Priority once at init in GameInitGroup before PowerInitSystem;
    /// Requesting every tick from a system in PowerDemandGroup); ConsumerIndex: PowerInitSystem. Nobody else writes it.</summary>
    [InternalBufferCapacity(2)]
    public struct PowerRequest : IBufferElementData   // 12 bytes
    {
        public Game.Data.Blocks.BlockModuleId Module;
        public byte Priority;           // from PowerTuning (owner's field)
        public byte Requesting;         // 1 = wants its Draw this tick
        public byte Reserved;
        public float Draw;              // E/s while requesting, ≥ 0
        public short ConsumerIndex;     // index in the grid's PowerConsumer buffer; −1 until PowerInitSystem bound it
        public short Reserved2;
    }
}
