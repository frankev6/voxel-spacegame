#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Simulation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace Game.App.DevRuntime.Overlays
{
    /// <summary>SPEC-70 DEV-013: occupied XZ buckets intersecting the player's one-kilometre neighbourhood.</summary>
    [BurstCompile]
    internal struct SpatialHashOverlayJob : IJob
    {
        [ReadOnly] public SpatialHashQueryData Hash;
        public float3 PlayerPosition;
        public DevLineBatch Lines;
        public void Execute()
        {
            float size = Hash.State.Value.CellSize;
            if (!math.isfinite(size) || size <= 0 || !math.all(math.isfinite(PlayerPosition))) return;
            float2 low = math.floor((PlayerPosition.xz - 1000) / size), high = math.floor((PlayerPosition.xz + 1000) / size);
            if (math.any(low <= int.MinValue) || math.any(high >= int.MaxValue)) return;
            int2 min = (int2)low, max = (int2)high;
            // Valid AI content has cellSize >= 32; deterministic coordinate lookups never iterate the hash map.
            for (int x = min.x; x <= max.x; x++)
                for (int z = min.y; z <= max.y; z++)
                {
                    if (!Hash.Cells.TryGetValue(SpatialHash.CellKey(new int2(x, z)), out var range) || range.y == 0) continue;
                    float2 a = new float2(x, z) * size, b = a + size;
                    float2 closest = math.clamp(PlayerPosition.xz, a, b);
                    if (math.distancesq(closest, PlayerPosition.xz) > 1000000) continue;
                    if (Lines.Vertices.Length + 8 > DevLineBatch.MaxVertices) { Lines.Overflow.Value = 1; return; }
                    var color = new Color32(0, 200, 255, 255);
                    for (int edge = 0; edge < 4; edge++)
                    {
                        float3 p = new float3(edge == 0 || edge == 3 ? a.x : b.x, 0, edge < 2 ? a.y : b.y);
                        int next = (edge + 1) & 3;
                        float3 q = new float3(next == 0 || next == 3 ? a.x : b.x, 0, next < 2 ? a.y : b.y);
                        Lines.Vertices.AddNoResize(new DevLineVertex { Position = p, Color = color });
                        Lines.Vertices.AddNoResize(new DevLineVertex { Position = q, Color = color });
                    }
                }
        }
    }
}
#endif
