using Game.Core.Components;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Profiling;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Grid
{
    /// <summary>SPEC-10 §5.10: frame-delayed mesh cleanup and frame/tick-delayed physics blob cleanup, including while paused.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial struct DeferredDisposalSystem : ISystem
    {
        static readonly ProfilerMarker FreeMarker = new ProfilerMarker("DeferredDisposalSystem.Free");
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<DeferredDisposalQueue>();
        public void OnUpdate(ref SystemState state)
        {
            using var marker = FreeMarker.Auto();
            ref var queue = ref SystemAPI.GetSingletonRW<DeferredDisposalQueue>().ValueRW;
            queue.Frame++;
            queue.HasClock = SystemAPI.TryGetSingleton<SimClock>(out var clock) ? (byte)1 : (byte)0;
            queue.Tick = queue.HasClock != 0 ? clock.Tick : 0;
            for (int i = queue.Blobs.Length - 1; i >= 0; i--)
            {
                var retired = queue.Blobs[i];
                if (retired.DueFrame > queue.Frame || (queue.HasClock != 0 && queue.Tick < retired.DueTick)) continue;
                if (retired.Blob.IsCreated) retired.Blob.Dispose();
                queue.Blobs.RemoveAtSwapBack(i);
            }
            if (queue.Meshes.Length == 0) return;
            var graphics = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            for (int i = queue.Meshes.Length - 1; i >= 0; i--)
            {
                var retired = queue.Meshes[i];
                if (retired.DueFrame > queue.Frame) continue;
                if (retired.Id != BatchMeshID.Null && graphics != null) graphics.UnregisterMesh(retired.Id);
                var mesh = retired.Mesh.Value;
                if (mesh != null) Object.Destroy(mesh);
                queue.Meshes.RemoveAtSwapBack(i);
            }
        }
        // GridSpawnSystem owns the queue and drains all remaining allocations during world teardown.
    }
}
