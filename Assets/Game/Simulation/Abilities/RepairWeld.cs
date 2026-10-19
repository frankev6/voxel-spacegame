using Game.Data.Definitions;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Abilities
{
    /// <summary>ABL-006 weld sparks on cells an emergency repair rebuilds (presentation only, main thread). The host sparks the cells it
    /// writes; a co-op guest sparks the cells its replica gains while the ship's repair slot is active. Kinetic hit sparks, tinted hot.</summary>
    public static class RepairWeld
    {
        public const uint SparkRgba8 = 0xFF5AC8FFu;   // R 255, G 200, B 90, A 255: a hot weld
        public const int SparksPerTick = 1;           // per ship; more merge into one glow at flight-camera range

        /// <summary>True while one of the ship's EmergencyRepair slots is active.</summary>
        public static bool Channelling(EntityManager em, Entity root)
        {
            if (!em.HasBuffer<AbilitySlot>(root)) return false;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<AbilityDefsRef>());
            if (q.IsEmpty) return false;
            var defs = q.GetSingleton<AbilityDefsRef>().Value;
            foreach (var slot in em.GetBuffer<AbilitySlot>(root, true))
                if (slot.Active != 0 && slot.AbilityIndex < defs.Value.Abilities.Length && defs.Value.Abilities[slot.AbilityIndex].Kind == AbilityKind.EmergencyRepair)
                    return true;
            return false;
        }

        /// <summary>One burst of sparks at the centre of `cell`, thrown outward from the ship's centre.</summary>
        public static void Spark(EntityManager em, Entity root, int3 cell)
        {
            if (!em.HasComponent<VoxelGrid>(root) || !em.HasComponent<LocalToWorld>(root)) return;
            var grid = em.GetComponentData<VoxelGrid>(root); var ltw = em.GetComponentData<LocalToWorld>(root).Value;
            float3 local = ((float3)cell + .5f) * grid.CellSize, centre = ((float3)(grid.CellMin + grid.CellMax) + 1) * .5f * grid.CellSize;
            float3 pos = math.transform(ltw, local), dir = math.rotate(ltw, math.normalizesafe(local - centre, new float3(0, 1, 0)));
            float3 vel = em.HasComponent<PhysicsVelocity>(root) ? em.GetComponentData<PhysicsVelocity>(root).Linear : default;
            FxEvents.EnqueueMainThread(em, new FxEvent { Kind = FxKind.HitKinetic, Pos = pos, Dir = dir, Vel = vel,
                Scale = math.max(.3f, grid.CellSize * .8f), Color = SparkRgba8 });   // never PlayerSource: HitMarkerSystem would mark it as a hit
        }
    }
}
