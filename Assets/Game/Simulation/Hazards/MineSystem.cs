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
    /// the first live ship root (by Entity.Index) whose bounding sphere comes within TriggerRadius and that is heavy enough; once armed,
    /// any cell lost to damage cooks it off at once (while arming, damage only resets its baseline). FuseSeconds after the trigger it posts one ExplosionRequest at its centre and despawns.
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
                if (fuse.SpawnRevision == 0)
                {
                    fuse.SpawnRevision = grid.Revision;
                    fuse.Cells = em.HasComponent<Game.Voxel.PhysicsBody.GridMass>(e) ? (int)em.GetComponentData<Game.Voxel.PhysicsBody.GridMass>(e).Data.CellCount : 0;
                    em.SetComponentData(e, fuse);
                }
                // The charge cooks off once shots have put CasingHp into the casing since it armed, or once a quarter of the casing is
                // gone (blown apart); a chipped cell alone only shoves it (owner: "sturdy and bouncy enough that I can shoot them to
                // redirect them ... if i shoot it a ton, it should detonate"). Without CasingHp, any lost cell cooks it off.
                var stats = em.HasComponent<Game.Voxel.Damage.GridDamageStats>(e) ? em.GetComponentData<Game.Voxel.Damage.GridDamageStats>(e) : default;
                float taken = stats.DamageTaken;
                bool hit = p.CasingHp > 0
                    ? mine.Phase != MinePhase.Arming && (taken - fuse.DamageAtArm >= p.CasingHp || stats.CellsDestroyed + stats.CellsDetached > fuse.Cells / 4)
                    : grid.Revision != fuse.SpawnRevision;
                switch (mine.Phase)
                {
                    case MinePhase.Arming:
                        // Not live yet: a scrape while it settles (a mine scattered against a post or a fragment is pushed clear at
                        // spawn) becomes its new baseline instead of a cook-off (RACE-016: such mines blew fences on their own).
                        if (hit) { fuse.SpawnRevision = grid.Revision; em.SetComponentData(e, fuse); }
                        if (tick - mine.PhaseTick >= (uint)(p.ArmSeconds * 60)) { mine.Phase = MinePhase.Armed; mine.PhaseTick = tick; fuse.DamageAtArm = taken; em.SetComponentData(e, fuse); }
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
                            Producer = ProducerKind.Hazard, Sequence = (uint)m, Instigator = mine.TriggeredBy, Knockback = p.BlastImpulse
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

    /// <summary>SPEC-91 §5.3 step 5: a capped spring holds an anchored mine near its point (every machine). The point is where the mine
    /// first appears. A knock faster than ReleaseSpeed (a shot, a ram, a blast) tears it loose: from then on it drifts and bounces where
    /// it was sent (owner: "shoot them to redirect them ... onto an opponent, or out of my way"). An origin rebase moves the mine but
    /// not its point, so a jump over 50 m in one tick moves the point with it.</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(MineSystem)), UpdateBefore(typeof(ImpulseApplySystem))]
    public partial struct MineAnchorSystem : ISystem
    {
        internal const float ReleaseSpeed = 2.5f;   // m/s; the overdamped spring alone never moves a mine this fast
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
            var loose = new NativeList<Entity>(Allocator.Temp);
            foreach (var e in entities)
            {
                var anchor = em.GetComponentData<MineAnchor>(e); var mass = em.GetComponentData<PhysicsMass>(e);
                if (mass.InverseMass <= 0) continue;
                float2 pos = em.GetComponentData<LocalTransform>(e).Position.xz;
                float2 v = em.GetComponentData<PhysicsVelocity>(e).Linear.xz;
                if (anchor.Placed == 0) { anchor.Point = pos; anchor.Placed = 1; em.SetComponentData(e, anchor); }
                else if (last.TryGetValue(e, out var before) && math.distancesq(before, pos) > 50 * 50) { anchor.Point += pos - before; em.SetComponentData(e, anchor); }
                seen[e] = pos;
                if (math.lengthsq(v) > ReleaseSpeed * ReleaseSpeed) { loose.Add(e); continue; }
                float2 accel = anchor.Stiffness * (anchor.Point - pos) - anchor.Damping * v;
                float length = math.length(accel);
                if (length > anchor.MaxAccel) accel *= anchor.MaxAccel / length;
                if (length < 1e-4f) continue;
                float2 j = accel * dt / mass.InverseMass;
                streams.Impulses.PostFromMainThread(new ImpulseRequest { Body = e, WorldImpulse = new float3(j.x, 0, j.y), Flags = ImpulseRequestFlags.AtCenterOfMass,
                    Producer = ProducerKind.Hazard, Sequence = (uint)e.Index });
            }
            foreach (var e in loose) { em.RemoveComponent<MineAnchor>(e); seen.Remove(e); }
            last.Clear(); foreach (var kv in seen) last[kv.Key] = kv.Value;
            seen.Dispose(); loose.Dispose();
        }
    }
}
