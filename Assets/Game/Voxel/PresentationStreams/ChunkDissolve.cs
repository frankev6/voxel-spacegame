using Unity.Entities;
using Unity.Rendering;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Implements SPEC-63 §4.1; queue ownership follows ADR-0050.</summary>
    [MaterialProperty("_DissolveAmount")]
    public struct ChunkDissolve : IComponentData
    {
        public float Value; // 0 = solid, 1 = fully dissolved
    }
}
