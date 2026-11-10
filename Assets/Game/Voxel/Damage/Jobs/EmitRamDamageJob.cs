using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.2: pair aggregation, energy/hardness split, cooldowns and presentation events.</summary>
    [BurstCompile] internal unsafe struct EmitRamDamageJob : IJob
    {
        public NativeList<RamContact> Contacts;
        public NativeHashMap<ulong, uint> Pairs;
        public GridCellReader Cells;
        [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<OwnedBy> Owners;
        [ReadOnly] public ComponentLookup<RamImmune> RamImmune;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        public DamageConfig Config;
        public uint Tick;
        public NativeStream.Writer Damage;
        // Atomic enqueue only; FxEventQueue.Access guards shared writers against drain/disposal.
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;
        /// <summary>Closing speed (m/s) under which a contact with RamImmune scenery is a harmless scrape.</summary>
        public const float ScrapeSpeedMs = 6f;
        public void Execute()
        {
            Contacts.Sort(); Damage.BeginForEachIndex(0);
            int ordinal = 0;
            for (int first = 0; first < Contacts.Length; ordinal++)
            {
                var pair = Contacts[first]; float impulse = 0; float3 point = 0, normal = 0; int end = first;
                while (end < Contacts.Length && Contacts[end].A == pair.A && Contacts[end].B == pair.B)
                {
                    var c = Contacts[end++]; impulse += c.Impulse; point += c.Impulse * c.Point; normal += c.Impulse * c.Normal;
                }
                first = end; if (!(impulse > 0) || !math.isfinite(impulse)) continue;
                point /= impulse; normal = math.normalizesafe(normal, pair.Normal);
                ulong key = ((ulong)(uint)pair.A.Index << 32) | (uint)pair.B.Index;
                if (Pairs.TryGetValue(key, out uint last) && Tick - last < Config.RamPairCooldownTicks) continue;
                float energy = DamageMath.RamEnergy(impulse, InverseMass(pair.A), InverseMass(pair.B), Config.RamRestitutionCorrection);
                if (energy < Config.MinRamEnergyJ) continue;
                float hardnessA = Hardness(pair.A, point, -normal), hardnessB = Hardness(pair.B, point, normal);
                // blame goes to the body that drove into the other (DEV-023 playtest: a cruiser that rammed the player turned the
                // whole Hollow hostile): each body's speed into the other along the normal (A to B); the body pushed away is not to
                // blame for the damage it took. Near-equal (head-on) blames both.
                float intoB = math.dot(Velocity(pair.A), normal), intoA = -math.dot(Velocity(pair.B), normal);
                // SPEC-93: a scrape against ram-immune scenery (a station's walls, the den's stone) under ScrapeSpeedMs breaks nothing on
                // either side; a ship flown into it faster still takes the hit (owner 2026-11-10: "avoid ... running into the station").
                // Velocities here are already solved, so the closing speed comes from the contact impulse: J = μ (1 + e) v.
                if ((RamImmune.HasComponent(pair.A) || RamImmune.HasComponent(pair.B))
                    && impulse * (InverseMass(pair.A) + InverseMass(pair.B)) / math.sqrt(Config.RamRestitutionCorrection) < ScrapeSpeedMs) continue;
                Emit(pair.A, pair.B, point, -normal, energy, Share(hardnessB, hardnessA), (uint)ordinal * 2, intoA >= intoB - RamBlameToleranceMs);
                Emit(pair.B, pair.A, point, normal, energy, Share(hardnessA, hardnessB), (uint)ordinal * 2 + 1, intoB >= intoA - RamBlameToleranceMs);
                Pairs[key] = Tick; Data->Current.RamEvents++;
                Fx.Enqueue(new FxEvent { Kind = FxKind.RamImpact, Pos = point, Dir = normal, Scale = math.clamp(math.pow(energy / 1e5f, 1f / 3), .5f, 4) });
                if (Config.CueRamImpact.Value != 0) Sfx.Enqueue(new SfxEvent { Cue = Config.CueRamImpact, Pos = point, Volume = math.clamp(.3f + .15f * math.log10(energy / 2e4f), .3f, 1) });
            }
            Damage.EndForEachIndex();
            if (Tick % 60 == 0)
            {
                using var stale = new NativeList<ulong>(Allocator.Temp);
                foreach (var pair in Pairs) if (Tick - pair.Value > 600) stale.Add(pair.Key);
                stale.Sort(); foreach (ulong key in stale) Pairs.Remove(key);
            }
        }
        static float Share(float other, float own)
        {
            // Zero damage multiplier means infinite hardness; avoid infinity/infinity NaNs.
            if (math.isinf(own)) return math.isinf(other) ? .5f : 0;
            if (math.isinf(other)) return 1;
            return other / (own + other);
        }
        const float RamBlameToleranceMs = 2;
        float3 Velocity(Entity body) => Velocities.HasComponent(body) ? Velocities[body].Linear : float3.zero;
        float InverseMass(Entity body) => Velocities.HasComponent(body) && Masses.HasComponent(body) ? Masses[body].InverseMass : 0;
        float3 LocalPoint(Entity grid, float3 point, float3 into) => Transforms[grid].InverseTransformPoint(point + into * (.5f * Cells.Grids[grid].CellSize));
        float Hardness(Entity grid, float3 point, float3 into)
        {
            if (!Cells.Grids.HasComponent(grid) || !Transforms.HasComponent(grid)) return 100;
            int3 pos = DamageMath.CellOf(LocalPoint(grid, point, into), Cells.Grids[grid].CellSize);
            if (!Cells.TryGetCell(grid, pos, out var cell)) return 100;
            ushort block = cell.IsInstance ? Instances[grid][cell.InstanceIndex].Block : Palettes[grid][cell.PaletteIndex].Block;
            ref var def = ref Blocks.Value.Blocks[block]; float multiplier = Config.Multiplier(DamageType.Ram, def.ArmorClass);
            return multiplier > 0 ? def.CellHp / multiplier : float.PositiveInfinity;
        }
        /// <summary>Ram damage to `grid` from `other`. `otherRammed`: `other` drove into `grid`, so `other` (its owner) is the
        /// instigator; otherwise the damage is `grid`'s own doing and it is its own instigator (no provocation, no kill credit).</summary>
        void Emit(Entity grid, Entity other, float3 point, float3 into, float energy, float share, uint sequence, bool otherRammed)
        {
            if (!Cells.Grids.HasComponent(grid) || !Transforms.HasComponent(grid) || RamImmune.HasComponent(grid)) return;
            var transform = Transforms[grid]; var size = Cells.Grids[grid].CellSize;
            Damage.Write(new DamageEvent { Grid = grid, LocalPoint = LocalPoint(grid, point, into),
                LocalDir = math.rotate(math.inverse(transform.Rotation), into), Amount = energy * Config.RamHpPerJoule * share,
                Radius = math.clamp(Config.RamRadiusBaseCells + Config.RamRadiusCellsPerSqrtMJ * math.sqrt(energy / 1e6f), .75f, Config.RamRadiusMaxCells) * size,
                Type = DamageType.Ram, Source = other, Instigator = !otherRammed ? grid : Owners.HasComponent(other) ? Owners[other].Owner : other,
                Producer = ProducerKind.Impact, Sequence = sequence, Flags = DamageEventFlags.NoImpulse });
        }
    }
}
