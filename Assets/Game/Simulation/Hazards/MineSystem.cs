using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Hazards
{
    /// <summary>SPEC-91 §5.3 (HAZ-008): the mine decider, on the authority. Arming → Armed after ArmSeconds; an armed mine triggers on
    /// the first live ship root (by Entity.Index) whose bounding sphere comes within TriggerRadius and that is heavy enough; any cell
    /// lost to damage cooks it off at once. FuseSeconds after the trigger it posts one ExplosionRequest at its centre and despawns.
    /// Debris, chunks, hulks and devices never trigger it (decision 8).</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(Game.Simulation.Weapons.ExplosionSystem))]
    public partial struct MineSystem : ISystem
    {
        EntityQuery mines, ships;
        public void OnCreate(ref SystemState state)
        {
            mines = state.GetEntityQuery(typeof(MineState), typeof(MineFuse), ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<PhysicsMass>());
            ships = state.GetEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<PhysicsMass>(),
                ComponentType.Exclude<Hulk>(), ComponentType.Exclude<MineFuse>());
            state.RequireForUpdate(mines); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<DamageStreams>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            using var mineEntities = mines.ToEntityArray(Allocator.Temp);
            using var shipEntities = ships.ToEntityArray(Allocator.Temp);
            var spent = new NativeList<Entity>(Allocator.Temp);
            for (int m = 0; m < mineEntities.Length; m++)
            {
                var e = mineEntities[m];
                var mine = em.GetComponentData<MineState>(e); var fuse = em.GetComponentData<MineFuse>(e);
                var grid = em.GetComponentData<VoxelGrid>(e);
                if (grid.Kind != GridKind.Device || mine.Phase == MinePhase.Spent) continue;
                var pose = em.GetComponentData<LocalTransform>(e);
                float3 centre = pose.TransformPoint(em.GetComponentData<PhysicsMass>(e).CenterOfMass);
                var p = fuse.Params;
                if (fuse.SpawnRevision == 0) { fuse.SpawnRevision = grid.Revision; em.SetComponentData(e, fuse); }
                bool hit = grid.Revision != fuse.SpawnRevision;   // any cell lost: the charge cooks off
                switch (mine.Phase)
                {
                    case MinePhase.Arming:
                        if (hit) { mine = Trigger(mine, tick, Entity.Null); p.FuseSeconds = math.min(p.FuseSeconds, .15f); break; }
                        if (tick - mine.PhaseTick >= (uint)(p.ArmSeconds * 60)) { mine.Phase = MinePhase.Armed; mine.PhaseTick = tick; }
                        break;
                    case MinePhase.Armed:
                        if (hit) { mine = Trigger(mine, tick, Entity.Null); break; }
                        Entity best = Entity.Null;
                        foreach (var s in shipEntities)
                        {
                            var g = em.GetComponentData<VoxelGrid>(s);
                            if (g.Kind != GridKind.Ship) continue;
                            var mass = em.GetComponentData<PhysicsMass>(s);
                            if (mass.InverseMass <= 0 || 1f / mass.InverseMass < p.MinTriggerMass) continue;
                            float radius = em.HasComponent<ShipStats>(s) ? em.GetComponentData<ShipStats>(s).BoundingRadius
                                : math.length((float3)(g.CellMax - g.CellMin + 1)) * g.CellSize * .5f;
                            var sp = em.GetComponentData<LocalTransform>(s);
                            float d = math.distance(sp.TransformPoint(mass.CenterOfMass).xz, centre.xz) - radius * .6f;
                            if (d > p.TriggerRadius) continue;
                            if (best == Entity.Null || s.Index < best.Index) best = s;
                        }
                        if (best != Entity.Null) mine = Trigger(mine, tick, best);
                        break;
                    case MinePhase.Triggered:
                        float fuseSeconds = hit ? math.min(p.FuseSeconds, .15f) : p.FuseSeconds;
                        if (tick - mine.PhaseTick < (uint)(fuseSeconds * 60)) break;
                        streams.Explosions.PostFromMainThread(new ExplosionRequest
                        {
                            WorldPos = centre, Radius = p.BlastRadius, Amount = p.BlastDamage, Type = p.BlastType, Source = e,
                            Producer = ProducerKind.Hazard, Sequence = (uint)m, Instigator = mine.TriggeredBy
                        });
                        mine.Phase = MinePhase.Spent; mine.PhaseTick = tick;
                        spent.Add(e);
                        break;
                }
                em.SetComponentData(e, mine);
            }
            foreach (var e in spent)
                if (em.HasComponent<GridDespawnRequest>(e)) Game.Voxel.Grid.GridSpawnApi.RequestDespawn(em, e);
            spent.Dispose();
        }
        static MineState Trigger(MineState mine, uint tick, Entity by) => new MineState { Phase = MinePhase.Triggered, PhaseTick = tick, TriggeredBy = by };
    }

    /// <summary>SPEC-91 §5.3 step 5: a capped spring holds an anchored mine near its point after a shove (every machine). An origin
    /// rebase moves the mine but not its anchor point, so a jump over 50 m in one tick moves the anchor with it.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(MineSystem)), UpdateBefore(typeof(ImpulseApplySystem))]
    public partial struct MineAnchorSystem : ISystem
    {
        EntityQuery anchored;
        NativeHashMap<Entity, float2> last;
        public void OnCreate(ref SystemState state)
        {
            anchored = state.GetEntityQuery(typeof(MineAnchor), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<PhysicsMass>(),
                ComponentType.ReadOnly<PhysicsVelocity>());
            last = new NativeHashMap<Entity, float2>(64, Allocator.Persistent);
            state.RequireForUpdate(anchored); state.RequireForUpdate<DamageStreams>();
        }
        public void OnDestroy(ref SystemState state) { if (last.IsCreated) last.Dispose(); }
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager; var streams = SystemAPI.GetSingleton<DamageStreams>();
            const float dt = Game.Core.SimTime.FixedTimestep;
            using var entities = anchored.ToEntityArray(Allocator.Temp);
            var seen = new NativeHashMap<Entity, float2>(entities.Length, Allocator.Temp);
            foreach (var e in entities)
            {
                var anchor = em.GetComponentData<MineAnchor>(e); var mass = em.GetComponentData<PhysicsMass>(e);
                if (mass.InverseMass <= 0) continue;
                float2 pos = em.GetComponentData<LocalTransform>(e).Position.xz;
                if (last.TryGetValue(e, out var before) && math.distancesq(before, pos) > 50 * 50) { anchor.Point += pos - before; em.SetComponentData(e, anchor); }
                seen[e] = pos;
                float2 v = em.GetComponentData<PhysicsVelocity>(e).Linear.xz;
                float2 accel = anchor.Stiffness * (anchor.Point - pos) - anchor.Damping * v;
                float length = math.length(accel);
                if (length > anchor.MaxAccel) accel *= anchor.MaxAccel / length;
                if (length < 1e-4f) continue;
                float2 j = accel * dt / mass.InverseMass;
                streams.Impulses.PostFromMainThread(new ImpulseRequest { Body = e, WorldImpulse = new float3(j.x, 0, j.y), Flags = ImpulseRequestFlags.AtCenterOfMass,
                    Producer = ProducerKind.Hazard, Sequence = (uint)e.Index });
            }
            last.Clear(); foreach (var kv in seen) last[kv.Key] = kv.Value;
            seen.Dispose();
        }
    }
}
