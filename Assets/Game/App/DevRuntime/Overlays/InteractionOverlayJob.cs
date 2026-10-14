#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace Game.App.DevRuntime.Overlays
{
    public struct InteractionOverlayItem { public float3 Center; public float SurfaceRadius, Range; }
    /// <summary>DEV-014: 32-segment XZ range circles and the actual owner's target/range line, in the shared bounded batch.</summary>
    [BurstCompile]
    public struct InteractionOverlayJob : IJob
    {
        [ReadOnly] public NativeArray<InteractionOverlayItem> Items;
        public float3 Player, Target;
        public bool HasTarget, InRange;
        public int ReservedVertices;
        public DevLineBatch Lines;
        public void Execute()
        {
            if (HasTarget) Lines.Line(Player, Target, InRange ? new Color32(0, 255, 0, 255) : new Color32(255, 0, 0, 255));
            foreach (var item in Items)
            {
                if (item.Range < 0) continue;
                float radius = item.SurfaceRadius + item.Range;
                if (!math.isfinite(radius) || radius <= 0) continue;
                if (Lines.Vertices.Length + 64 + ReservedVertices > DevLineBatch.MaxVertices) { Lines.Overflow.Value = 1; return; }
                float3 previous = item.Center + new float3(radius, 0, 0);
                for (int segment = 1; segment <= 32; segment++)
                {
                    float angle = segment * (2 * math.PI / 32);
                    math.sincos(angle, out float sin, out float cos);
                    float3 next = item.Center + new float3(cos * radius, 0, sin * radius);
                    Lines.Line(previous, next, new Color32(0, 255, 255, 255)); previous = next;
                }
            }
        }
    }
}
#endif
