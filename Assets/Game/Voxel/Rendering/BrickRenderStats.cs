using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.Rendering
{
    /// <summary>Allocated GPU capacity (including retired buffers) and applied live geometry, SPEC-11 §5.10.</summary>
    public static class BrickRenderStats
    {
        public static BrickRenderTotals Gather(EntityManager em)
        {
            em.CompleteDependencyBeforeRO<BrickRenderState>();
            var totals = new BrickRenderTotals();
            using var runtimeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<VoxelRenderRuntime>());
            if (!runtimeQuery.IsEmptyIgnoreFilter)
            {
                var managed = runtimeQuery.GetSingleton<VoxelRenderRuntime>().Managed.Value;
                if (managed && managed.Pool != null) totals.GpuBytes = managed.Pool.GpuBytes;
            }
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<Game.Voxel.Model.Brick>(), ComponentType.ReadOnly<BrickRenderState>());
            using var states = query.ToComponentDataArray<BrickRenderState>(Allocator.Temp);
            foreach (var render in states)
            {
                if (render.HasMesh == 0) continue;
                totals.Meshes++; totals.Vertices += render.VertexCount; totals.Indices += render.IndexCount;
            }
            // Pooled geometry never retains a CPU copy; in-flight results and allocator bookkeeping are accounted separately.
            return totals;
        }
    }
}
