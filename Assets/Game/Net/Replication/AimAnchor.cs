using Game.Core.Components;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Net.Replication
{
    /// <summary>Lag-compensated aim for player ships (SPEC-90 §10a). A guest sees other ships about 100 ms in the past, and its command
    /// reaches the host later still, so an absolute aim point lands where the target used to be. Instead a player's aim travels as the
    /// grid under it (its PersistentId, the network id on both ends) and a point in that grid's frame; every machine rebuilds the aim from
    /// the pose it shows for that grid, so a shot that hits on the shooter's screen hits on the host too. Main thread.</summary>
    public static class AimAnchor
    {
        public const float Margin = 6f;   // metres around a grid's cell bounds that still count as aiming at it
        [Game.Core.StaticStateOk("dev A/B switch (net anchor), main thread")]
        public static bool Enabled = true;

        /// <summary>The grid an aim point (local frame) is on or near: the locked target when it qualifies, else the nearest qualifying
        /// grid. Returns 0 when the aim is in open space.</summary>
        public static ulong Find(EntityManager em, EntityQuery grids, float3 aim, Entity locked, Entity self, out float3 local)
        {
            local = default;
            if (locked != Entity.Null && locked != self && TryLocal(em, locked, aim, out local, out _)) return em.GetComponentData<PersistentId>(locked).Value;
            ulong best = 0; float bestDistance = float.MaxValue;
            using var entities = grids.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (entity == self || !TryLocal(em, entity, aim, out var point, out float distance) || distance >= bestDistance) continue;
                ulong id = em.GetComponentData<PersistentId>(entity).Value; if (id == 0) continue;
                best = id; bestDistance = distance; local = point;
            }
            return best;
        }

        static bool TryLocal(EntityManager em, Entity grid, float3 aim, out float3 local, out float distance)
        {
            local = default; distance = 0;
            if (!em.Exists(grid) || !em.HasComponent<VoxelGrid>(grid) || !em.HasComponent<LocalTransform>(grid) || !em.HasComponent<PersistentId>(grid)) return false;
            var g = em.GetComponentData<VoxelGrid>(grid);
            if (g.Kind == GridKind.Asteroid) return false;
            var pose = em.GetComponentData<LocalTransform>(grid);
            local = pose.InverseTransformPoint(aim);
            float3 min = (float3)g.CellMin * g.CellSize, max = ((float3)g.CellMax + 1) * g.CellSize;
            distance = math.length(math.max(0, math.max(min - local, local - max)));   // 0 inside the bounds
            return distance <= Margin;
        }

        /// <summary>The aim point (local frame) for an anchor on this machine, or false when the grid is unknown here.</summary>
        public static bool Resolve(EntityManager em, Entity grid, float3 local, out float3 aim)
        {
            aim = default;
            if (!Enabled || grid == Entity.Null || !em.Exists(grid) || !em.HasComponent<LocalTransform>(grid)) return false;
            aim = em.GetComponentData<LocalTransform>(grid).TransformPoint(local);
            return true;
        }
    }
}
