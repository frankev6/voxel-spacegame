using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4. M3, on the root.</summary>
    [InternalBufferCapacity(0)] public struct GridEditedBrick : IBufferElementData { public int3 Coord; }
}
