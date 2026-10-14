#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
namespace Game.App.DevRuntime.Overlays
{
    /// <summary>Draw cached bounds with the current root transform; keep the largest visible within the line cap.</summary>
    [BurstCompile]
    internal struct IslandOverlayDrawJob : IJob
    {
        [ReadOnly] public NativeList<IslandOverlayBounds> Islands;
        public IslandOverlayState Result;
        public DevOverlayRoot Root;
        public DevLineBatch Lines;
        void Box(IslandOverlayBounds bounds, Color32 color)
            => Lines.Box((float3)bounds.Min * Root.CellSize, ((float3)bounds.Max + 1) * Root.CellSize, Root.Transform, color);
        public void Execute()
        {
            if (Result.Count == 0 || Result.Skipped != 0) return;
            Box(Result.Largest, new Color32(255, 255, 255, 255));
            foreach (var bounds in Islands)
            {
                if (bounds.Index == Result.Largest.Index) continue;
                float hue = math.frac(bounds.Index * .618f);
                float3 rgb = math.saturate(math.abs(math.frac(hue + new float3(0, 2f / 3, 1f / 3)) * 6 - 3) - 1);
                Box(bounds, new Color32((byte)(rgb.x * 255), (byte)(rgb.y * 255), (byte)(rgb.z * 255), 255));
                if (Lines.Truncated) break;
            }
            if (Result.Count > IslandOverlayJob.MaxBounds) Lines.Overflow.Value = 1;
        }
    }
}
#endif
