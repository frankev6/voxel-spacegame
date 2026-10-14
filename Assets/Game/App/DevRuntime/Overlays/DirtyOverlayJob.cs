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
    public struct DirtyOverlayJob : IJob
    {
        [ReadOnly] public NativeArray<Brick> Bricks;
        [ReadOnly] public NativeArray<BrickDirty> Dirty;
        [ReadOnly] public NativeParallelHashMap<Entity, DevOverlayRoot> Roots;
        public DevLineBatch Lines;
        public void Execute()
        {
            for (int i = 0; i < Bricks.Length; i++)
            {
                var flags = Dirty[i].Flags;
                if (flags == BrickDirtyFlags.None || !Roots.TryGetValue(Bricks[i].Grid, out var root)) continue;
                bool mesh = (flags & BrickDirtyFlags.Mesh) != 0, collider = (flags & BrickDirtyFlags.Collider) != 0;
                var color = new Color32(255, mesh ? (byte)(collider ? 128 : 255) : (byte)0, 0, 255);
                float3 min = (float3)(Bricks[i].Coord * 16) * root.CellSize;
                Lines.Box(min, min + 16 * root.CellSize, root.Transform, color);
                if (Lines.Truncated) return;
            }
        }
    }
}
#endif
