using Unity.Collections.LowLevel.Unsafe;

namespace Game.Voxel.Rendering
{
    /// <summary>Owned cross-frame geometry for one work slot (SPEC-11 §5.10 / ADR-0032).
    /// Unsafe lists avoid nested native safety containers; only that slot's job writes them, and the owner completes before disposal.</summary>
    public struct BrickMeshResult
    {
        public UnsafeList<PackedBrickVertex> Vertices;
        public UnsafeList<uint> Indices;
        public void Dispose()
        {
            if (Vertices.IsCreated) Vertices.Dispose();
            if (Indices.IsCreated) Indices.Dispose();
            this = default;
        }
    }
}
