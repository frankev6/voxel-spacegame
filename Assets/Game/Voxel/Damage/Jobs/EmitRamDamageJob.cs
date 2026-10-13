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
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        public DamageConfig Config;
        public uint Tick;
        public NativeStream.Writer Damage;
        // Atomic enqueue only; FxEventQueue.Access guards shared writers against drain/disposal.
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        [Unity.Collections.LowLevel.Unsafe.NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Sfx;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;
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
                Emit(pair.A, pair.B, point, -normal, energy, Share(hardnessB, hardnessA), (uint)ordinal * 2);
                Emit(pair.B, pair.A, point, normal, energy, Share(hardnessA, hardnessB), (uint)ordinal * 2 + 1);
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
        void Emit(Entity grid, Entity other, float3 point, float3 into, float energy, float share, uint sequence)
        {
            if (!Cells.Grids.HasComponent(grid) || !Transforms.HasComponent(grid)) return;
            var transform = Transforms[grid]; var size = Cells.Grids[grid].CellSize;
            Damage.Write(new DamageEvent { Grid = grid, LocalPoint = LocalPoint(grid, point, into),
                LocalDir = math.rotate(math.inverse(transform.Rotation), into), Amount = energy * Config.RamHpPerJoule * share,
                Radius = math.clamp(Config.RamRadiusBaseCells + Config.RamRadiusCellsPerSqrtMJ * math.sqrt(energy / 1e6f), .75f, Config.RamRadiusMaxCells) * size,
                Type = DamageType.Ram, Source = other, Instigator = Owners.HasComponent(other) ? Owners[other].Owner : other,
                Producer = ProducerKind.Impact, Sequence = sequence, Flags = DamageEventFlags.NoImpulse });
        }
    }
}
