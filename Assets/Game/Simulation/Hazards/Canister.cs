using Unity.Entities;

namespace Game.Simulation.Hazards
{
    /// <summary>HAZ-019 (ADR-0119 decision 7): an energy canister's device root. PickupSystem resolves its Pickup block on the first tick
    /// the grid has instances, grants its energy to the first ship within reach, then despawns it. Removed once spent or shot apart.</summary>
    public struct Canister : IComponentData
    {
        public ushort Group;
        public int Instance;            // BlockInstance index of the Pickup block; -1 until resolved
        public float Energy;
        public float Reach;
    }
}
