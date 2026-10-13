using Game.Voxel.Grid;
using Game.Core.Components;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    internal enum SplitTier : byte { Particle, Chunk, Hulk }
    [BurstCompile] internal unsafe struct PlanSplitJob : IJob
    {
        [ReadOnly] public NativeList<IslandRecord> Islands;
        [ReadOnly] public NativeList<IslandMask> Masks;
        public GridCellReader Reader;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        public BufferLookup<IntegritySeed> Seeds;
        [ReadOnly] public ComponentLookup<PersistentIdAllocator> Allocators;
        public Entity AllocatorEntity;
        public bool HulkAvailable;
        public DamageConfig Config;
        public NativeList<IslandRecord> Bodies;
        public NativeList<PendingDetach> Detach;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;
        public static SplitTier Tier(IslandRecord island, DamageConfig config)
        {
            if (island.HasFunctional != 0 || island.CellCount > config.ChunkMaxCells || math.cmax(island.CellMax - island.CellMin + 1) > config.ChunkMaxExtentCells) return SplitTier.Hulk;
            return island.CellCount <= config.ParticleMaxCells ? SplitTier.Particle : SplitTier.Chunk;
        }
        public void Execute()
        {
            using var anchors = new NativeHashMap<Entity, int3>(16, Allocator.Temp);
            Bodies.Clear(); int accepted = 0, cells = 0, hulks = 0; var allocator = Allocators[AllocatorEntity];
            foreach (var island in Islands)
            {
                var tier = Tier(island, Config);
                bool budget = accepted == 0 || (long)cells + island.CellCount <= Config.SplitCellBudgetPerTick;
                bool available = tier == SplitTier.Particle || (tier == SplitTier.Hulk && HulkAvailable && hulks < Config.MaxNewHulksPerTick && allocator.Next < allocator.End);
                // TODO(DMG-011): record pooled Chunk plans. Existing FND-020 hulk recording remains available.
                if (!budget || !available) { Reseed(island, anchors); Data->Current.SplitsDeferred++; continue; }
                accepted++; cells += island.CellCount;
                if (tier == SplitTier.Hulk) { Bodies.Add(island); hulks++; allocator.Next++; }
                else Data->Current.ParticleIslands++;
                for (int m = island.MaskStart; m < island.MaskStart + island.MaskCount; m++)
                { var mask = Masks[m]; Detach.Add(new PendingDetach { Grid = island.Grid, SlotIndex = mask.SlotIndex, Mask = mask.Mask, Reason = tier == SplitTier.Particle ? CellRemovedReason.DetachedParticle : CellRemovedReason.DetachedBody, IslandOrdinal = island.Ordinal }); }
            }
        }
        void Reseed(IslandRecord island, NativeHashMap<Entity, int3> anchors)
        {
            var seeds = Seeds[island.Grid]; var slots = Reader.Slots[island.Grid];
            // An island's AABB minimum need not be occupied, and disconnected islands have no outside solid face neighbour.
            for (int m = island.MaskStart; m < island.MaskStart + island.MaskCount; m++)
            {
                var mask = Masks[m]; bool found = false;
                for (int w = 0; w < 64; w++) if (mask.Mask.Words[w] != 0)
                { Add(seeds, BrickMath.CellOf(Bricks[slots[mask.SlotIndex].Brick].Coord, w * 64 + math.tzcnt(mask.Mask.Words[w]))); found = true; break; }
                if (found) break;
            }
            if (anchors.TryGetValue(island.Grid, out var anchor)) { Add(seeds, anchor); return; }
            using var excluded = new NativeHashMap<int, CellMask>(16, Allocator.Temp);
            foreach (var other in Islands) if (other.Grid == island.Grid)
                for (int m = other.MaskStart; m < other.MaskStart + other.MaskCount; m++)
                {
                    var mask = Masks[m]; excluded.TryGetValue(mask.SlotIndex, out var combined);
                    for (int w = 0; w < 64; w++) combined.Words[w] |= mask.Mask.Words[w];
                    excluded.Remove(mask.SlotIndex); excluded.Add(mask.SlotIndex, combined);
                }
            // Retain a representative of the anchor too: a lone island seed would be mistaken for the survivor next tick.
            for (int slot = 0; slot < slots.Length; slot++)
            {
                var brick = slots[slot].Brick; if (brick == Entity.Null) continue;
                var solid = Reader.Masks[brick]; excluded.TryGetValue(slot, out var islandBits);
                for (int w = 0; w < 64; w++)
                {
                    ulong remaining = solid[w].Bits & ~islandBits.Words[w];
                    if (remaining != 0)
                    {
                        anchor = BrickMath.CellOf(Bricks[brick].Coord, w * 64 + math.tzcnt(remaining));
                        anchors.Add(island.Grid, anchor); Add(seeds, anchor); return;
                    }
                }
            }
        }
        static void Add(DynamicBuffer<IntegritySeed> seeds, int3 p)
        { foreach (var seed in seeds) if (math.all(seed.Cell == p)) return; seeds.Add(new IntegritySeed { Cell = p }); }
    }
}
