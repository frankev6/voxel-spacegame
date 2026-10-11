using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.2. ECS form of the mask (architecture §6.3: 512 B per brick, capacity 0); exactly 64 elements.</summary>
    [InternalBufferCapacity(0)] public struct BrickSolidMask : IBufferElementData { public ulong Bits; }
}
