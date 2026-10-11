using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>Seeds kept for the next tick when integrity was deferred. Writer: IntegritySystem.</summary>
    [InternalBufferCapacity(0)]
    public struct IntegritySeed : IBufferElementData { public int3 Cell; }
}
