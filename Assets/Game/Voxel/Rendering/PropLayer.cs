using Unity.Entities;
using Unity.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>QA-001: a functional block's material layer (BlockDef.MaterialLayer), so its prop takes that layer's
    /// function colour, metal and glow from the shared layer table (NOCTURNE machinery look). 0 = unset/hull.</summary>
    [MaterialProperty("_PropLayer")]
    public struct PropLayer : IComponentData { public float Value; }
}
