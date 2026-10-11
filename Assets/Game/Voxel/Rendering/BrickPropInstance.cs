using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    [InternalBufferCapacity(0)]
    public struct BrickPropInstance : IBufferElementData
    { public ushort Block, LocalIndex; public byte Orient, Color; }
}
