using Game.Core;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.5: exclusive shield state/ripple writes per root, with sorted overflow publication.</summary>
    [BurstCompile]
    internal struct ShieldUpdateJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle Entities;
        public ComponentTypeHandle<ShieldState> Shields;
        public BufferTypeHandle<ShieldRipple> Ripples;
        [ReadOnly] public ComponentTypeHandle<ShipStats> Stats;
        [ReadOnly] public BufferTypeHandle<PowerConsumer> Consumers;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public ComponentLookup<ShieldControl> Controls;
        [ReadOnly] public ComponentLookup<ShipStatMultipliers> Multipliers;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public GridCellReader Reader;
        [ReadOnly] public BlobAssetReference<BlockModulesBlob> Modules;
        [ReadOnly] public NativeList<ShieldHit> Hits;
        [ReadOnly] public NativeParallelHashMap<Entity, int2> Ranges;
        public DamageConfig DamageConfig;
        public PowerTuning Tuning;
        public uint Tick;
        public NativeStream.Writer Damage;
        public NativeQueue<FxEvent>.ParallelWriter Fx;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(Entities); var shields = chunk.GetNativeArray(ref Shields);
            var ripples = chunk.GetBufferAccessor(ref Ripples); var stats = chunk.GetNativeArray(ref Stats);
            var consumers = chunk.GetBufferAccessor(ref Consumers);
            Damage.BeginForEachIndex(unfilteredChunkIndex);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out int i))
            {
                Entity root = entities[i];
                if (Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root)) continue;
                var shield = shields[i];
                Ranges.TryGetValue(root, out var range);
                if (Hulks.HasComponent(root))
                {
                    shield.Hp = shield.MaxHp = shield.RegenPerSecond = 0; shield.Active = 0;
                    for (int h = 0; h < range.y; h++) Overflow(root, shield, Hits[range.x + h], Hits[range.x + h].Amount);
                    shields[i] = shield; continue;
                }
                bool requested = Controls.HasComponent(root) && Controls[root].RequestedOn == 1;
                float cap = 0, regen = 0, delay = float.PositiveInfinity;
                if (Instances.HasBuffer(root))
                {
                    var instances = Instances[root]; var cs = consumers[i];
                    for (int c = 0; c < cs.Length; c++)
                    {
                        var consumer = cs[c];
                        if (consumer.Module != BlockModuleId.ShieldEmitter || (consumer.Flags & PowerConsumerFlags.Alive) == 0 ||
                            (uint)consumer.InstanceIndex >= (uint)instances.Length) continue;
                        if (!Modules.Value.TryGet(BlockModuleId.ShieldEmitter, instances[consumer.InstanceIndex].Block, out ShieldEmitterParams row)) continue;
                        delay = math.min(delay, row.RechargeDelaySeconds);
                        bool granted = (consumer.Flags & PowerConsumerFlags.Granted) != 0;
                        // A manual toggle retains the charge (§7); a requesting but unpowered emitter loses capacity.
                        if (granted || !requested) cap += row.Capacity;
                        if (granted && requested) regen += row.RegenPerSecond;
                    }
                }
                if (!math.isfinite(delay)) delay = 4;
                shield.MaxHp = cap; shield.Hp = math.min(shield.Hp, cap);
                shield.RegenPerSecond = regen * (Multipliers.HasComponent(root) ? Multipliers[root].Get(StatId.ShieldRegen) : 1);
                var stat = stats[i];
                if ((shield.Flags & ShieldFlags.Initialized) == 0 || shield.ShapeRevision != stat.FunctionalRevision)
                {
                    float3 half = (stat.BoundsMax - stat.BoundsMin) * .5f;
                    shield.Radii = math.max(half * Tuning.ShieldPaddingScale + Tuning.ShieldPaddingMetres, Tuning.ShieldMinRadiusMetres);
                    // Uniformly expand when needed: the padded ellipsoid must contain the bounding-box corners.
                    shield.Radii *= math.max(1, math.length(half / shield.Radii));
                    shield.Center = stat.BoundsCenter; shield.BoundingRadius = math.cmax(shield.Radii);
                    shield.ShapeRevision = stat.FunctionalRevision; shield.Flags |= ShieldFlags.Initialized;
                }
                if ((shield.Flags & ShieldFlags.FillOnNextUpdate) != 0)
                { shield.Hp = cap; shield.Flags &= ~(ShieldFlags.FillOnNextUpdate | ShieldFlags.Collapsed); }
                if (!requested && shield.Hp < cap * Tuning.ShieldRestartFraction) shield.Flags |= ShieldFlags.Collapsed;
                SetActive(ref shield, requested);
                var ring = ripples[i];
                if (ring.Length != 8)
                {
                    int oldLength = ring.Length; ring.ResizeUninitialized(8);
                    for (int r = oldLength; r < 8; r++) ring[r] = default;
                }
                for (int h = 0; h < range.y; h++)
                {
                    var hit = Hits[range.x + h]; float multiplier = 1;
                    if (DamageConfig.Table.IsCreated && (uint)hit.Type < (uint)DamageConfig.Table.Value.Types.Length)
                        multiplier = DamageConfig.Table.Value.Types[(int)hit.Type].ShieldMultiplier;
                    if (multiplier <= 0) continue;
                    float effective = hit.Amount * multiplier, absorbed = 0;
                    if (shield.Active == 1)
                    {
                        absorbed = math.min(effective, shield.Hp); shield.Hp -= absorbed;
                        shield.RegenResumeTick = Tick + (uint)math.round(delay * 60);
                        float strength = math.min(1, absorbed / 100);
                        ring[shield.RippleHead] = new ShieldRipple { LocalPoint = hit.LocalPoint, StartTick = math.max(Tick, 1u), Strength = strength };
                        shield.RippleHead = (byte)((shield.RippleHead + 1) & 7);
                        if (Transforms.HasComponent(root))
                        {
                            var pose = Transforms[root]; var velocity = Velocities.HasComponent(root) ? Velocities[root].Linear : float3.zero;
                            Fx.Enqueue(FxEvents.ShieldRipple(pose.TransformPoint(hit.LocalPoint), math.rotate(pose.Rotation, ShieldMath.Normal(hit.LocalPoint, shield.Center, shield.Radii)), velocity, strength));
                        }
                        if (shield.Hp <= 0) { shield.Hp = 0; shield.Active = 0; shield.Flags |= ShieldFlags.Collapsed; }
                    }
                    Overflow(root, shield, hit, (effective - absorbed) / multiplier);
                }
                if ((shield.Active == 1 || (shield.Flags & ShieldFlags.Collapsed) != 0) && requested && Tick >= shield.RegenResumeTick)
                    shield.Hp = math.min(shield.MaxHp, shield.Hp + shield.RegenPerSecond * SimTime.FixedTimestep);
                SetActive(ref shield, requested); shields[i] = shield;
            }
            Damage.EndForEachIndex();
        }
        void SetActive(ref ShieldState shield, bool requested)
        {
            if (!requested || shield.MaxHp <= 0) { shield.Active = 0; return; }
            if ((shield.Flags & ShieldFlags.Collapsed) != 0)
            {
                shield.Active = (byte)(shield.Hp >= Tuning.ShieldRestartFraction * shield.MaxHp ? 1 : 0);
                if (shield.Active == 1) shield.Flags &= ~ShieldFlags.Collapsed;
            }
            else shield.Active = (byte)(shield.Hp > 0 ? 1 : 0);
        }
        void Overflow(Entity root, in ShieldState shield, in ShieldHit hit, float rest)
        {
            if (rest <= .001f || hit.Amount <= 0) return;
            bool blast = (hit.Flags & ShieldHitFlags.Blast) != 0; float3 point = hit.LocalPoint;
            if (!blast)
            {
                if (!Reader.Grids.HasComponent(root)) return;
                float cellSize = Reader.Grids[root].CellSize;
                var query = new GridCellQuery { Reader = Reader, Grid = root }; float3 origin = point / cellSize;
                if (!VoxelRaycast.Cast(ref query, origin, hit.LocalDir, 2 * shield.BoundingRadius / cellSize, out var hull)) return;
                point = (origin + math.normalizesafe(hit.LocalDir) * (hull.DistanceCells + .05f)) * cellSize;
            }
            Damage.Write(new DamageEvent { Grid = root, LocalPoint = point, LocalDir = blast ? float3.zero : hit.LocalDir,
                Amount = rest, Radius = blast ? hit.Radius : 0, Penetration = blast ? 0 : hit.Penetration, Type = hit.Type,
                Source = hit.Source, Instigator = hit.Instigator, WorldImpulse = hit.WorldImpulse * (rest / hit.Amount),
                Producer = ProducerKind.ShieldOverflow, Sequence = hit.Sequence });
        }
    }
}
