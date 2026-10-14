#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace Game.App.DevRuntime.Overlays
{
    [BurstCompile]
    public struct BrickOverlayJob : IJob
    {
        [ReadOnly] public NativeArray<Brick> Bricks;
        [ReadOnly] public NativeParallelHashMap<Entity, DevOverlayRoot> Roots;
        public DevLineBatch Lines;
        public void Execute()
        {
            foreach (var brick in Bricks)
                if (Roots.TryGetValue(brick.Grid, out var root))
                {
                    float3 min = (float3)(brick.Coord * 16) * root.CellSize;
                    Lines.Box(min, min + 16 * root.CellSize, root.Transform, new Color32(255, 255, 255, 255));
                    if (Lines.Truncated) return;
                }
        }
    }
}
#endif
