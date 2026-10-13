using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1. One energy consumer of the ship = one PowerRequest element of one functional entity, sorted by (Priority desc,
    /// InstanceIndex asc, Module asc). Built by PowerInitSystem; per-tick fields written only by PowerAllocationSystem.</summary>
    [InternalBufferCapacity(0)]
    public struct PowerConsumer : IBufferElementData
    {
        public Entity Functional;       // the block's functional entity
        public int InstanceIndex;       // index into the grid's BlockInstance buffer
        public Game.Data.Blocks.BlockModuleId Module;   // the consuming module
        public byte Priority;           // copied from the PowerRequest element at init
        public byte RequestIndex;       // index of the element in the entity's PowerRequest buffer
        public PowerConsumerFlags Flags;
        public float Draw;              // E/s while requesting (copied at init)
        public uint DeniedUntilTick;    // brownout hold: no request is granted while SimClock.Tick < this
    }
}
