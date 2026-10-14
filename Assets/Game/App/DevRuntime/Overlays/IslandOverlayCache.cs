#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
namespace Game.App.DevRuntime.Overlays
{
    /// <summary>Host-owned native bounds cache, refreshed on target/world changes and every 60 enabled draws.</summary>
    internal sealed class IslandOverlayCache : IDisposable
    {
        NativeList<IslandOverlayBounds> bounds = new(16, Allocator.Persistent);
        NativeReference<IslandOverlayState> result = new(Allocator.Persistent);
        Entity target;
        uint serial;
        int frames;
        internal string Status => target == Entity.Null ? "islands target unavailable" : result.Value.Skipped != 0
            ? "grid too large for islands overlay" : "islands=" + result.Value.Count;
        internal void Invalidate() => frames = 0;
        internal void Clear() { target = Entity.Null; frames = 0; bounds.Clear(); result.Value = default; }
        internal void Gather(EntityManager em, Entity root, VoxelGrid grid, float4x4 transform, DevLineBatch lines)
        {
            if (root != target || grid.Serial != serial) frames = 0;
            target = root; serial = grid.Serial;
            if (frames > 0) frames--;
            if (frames == 0)
            {
                Refresh(em, root, grid); frames = 60;
            }
            new IslandOverlayDrawJob { Islands = bounds, Result = result.Value,
                Root = new DevOverlayRoot { Transform = transform, CellSize = grid.CellSize }, Lines = lines }.Run();
        }
        void Refresh(EntityManager em, Entity root, VoxelGrid grid)
        {
            bounds.Clear();
            int count = IslandOverlayJob.CellCount(grid.CellMin, grid.CellMax);
            if (count <= 0)
            { result.Value = new IslandOverlayState { Skipped = count < 0 ? 1 : 0 }; return; }
            if (!em.HasBuffer<BrickSlot>(root)) { result.Value = default; return; }
            var slots = em.GetBuffer<BrickSlot>(root, true);
            using var offsets = new NativeParallelHashMap<int3, int>(math.max(1, slots.Length), Allocator.TempJob);
            using var masks = new NativeList<ulong>(Allocator.TempJob);
            foreach (var slot in slots)
            {
                var brick = slot.Brick;
                if (!em.Exists(brick) || !em.HasComponent<Brick>(brick) || !em.HasBuffer<BrickSolidMask>(brick)) continue;
                var mask = em.GetBuffer<BrickSolidMask>(brick, true);
                if (mask.Length != BrickMath.MaskWords) continue;
                var info = em.GetComponentData<Brick>(brick);
                if (info.Grid != root || !offsets.TryAdd(info.Coord, masks.Length)) continue;
                for (int i = 0; i < mask.Length; i++) masks.Add(mask[i].Bits);
            }
            new IslandOverlayJob { Min = grid.CellMin, Max = grid.CellMax, BrickOffsets = offsets,
                Masks = masks.AsArray(), Islands = bounds, Result = result }.Run();
        }
        public void Dispose() { bounds.Dispose(); result.Dispose(); }
    }
}
#endif
