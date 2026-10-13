using Game.Core;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.4: signature-driven functional totals with independent mass/modifier refresh.</summary>
    [BurstCompile]
    internal struct ShipStatsJob : IJobChunk
    {
        public uint LastSystemVersion;
        public BlobAssetReference<BlockDefsBlob> Definitions;
        [ReadOnly] public EntityTypeHandle Entities;
        public ComponentTypeHandle<ShipStats> Stats;
        public ComponentTypeHandle<ShipStatMultipliers> Multipliers;
        [ReadOnly] public BufferTypeHandle<BlockInstance> Instances;
        [ReadOnly] public ComponentTypeHandle<VoxelGrid> Grids;
        [ReadOnly] public ComponentTypeHandle<PhysicsMass> Masses;
        [ReadOnly] public BufferTypeHandle<ShipStatModifier> Modifiers;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<Dormant> Dormancy;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            bool instChanged = chunk.DidChange(ref Instances, LastSystemVersion);
            bool hasMass = chunk.Has(ref Masses), hasModifiers = chunk.Has(ref Modifiers);
            bool massChanged = hasMass && chunk.DidChange(ref Masses, LastSystemVersion);
            bool modChanged = hasModifiers && chunk.DidChange(ref Modifiers, LastSystemVersion);
            var entities = chunk.GetNativeArray(Entities); var stats = chunk.GetNativeArray(ref Stats);
            var multipliers = chunk.GetNativeArray(ref Multipliers); var instances = chunk.GetBufferAccessor(ref Instances);
            var grids = chunk.GetNativeArray(ref Grids); var masses = chunk.GetNativeArray(ref Masses);
            var modifiers = chunk.GetBufferAccessor(ref Modifiers);
            for (int i = 0; i < chunk.Count; i++)
            {
                var entity = entities[i];
                if (Spawning.HasComponent(entity) && Spawning.IsComponentEnabled(entity) || Dormancy.HasComponent(entity) && Dormancy.IsComponentEnabled(entity)) continue;
                var value = stats[i]; bool full = (value.Flags & ShipStatsFlags.Initialized) == 0;
                ulong signature = value.FunctionalSignature; int count = value.AliveFunctionalCount;
                if (full || instChanged)
                {
                    count = 0; ulong hash = 0x9E3779B97F4A7C15;
                    var blocks = instances[i];
                    for (int j = 0; j < blocks.Length; j++)
                    {
                        var block = blocks[j];
                        if (block.Block == 0 || (block.Flags & BlockInstanceFlags.Destroyed) != 0 || Definitions.Value.Blocks[block.Block].Kind != BlockKind.Functional) continue;
                        count++; hash = StableHash.Combine(hash, (ulong)j);
                    }
                    signature = StableHash.Combine(hash, (ulong)count);
                    full |= signature != value.FunctionalSignature;
                }
                if (full)
                {
                    var grid = grids[i]; value.AliveFunctionalCount = count; value.FunctionalSignature = signature;
                    value.BoundsMin = (float3)grid.CellMin * grid.CellSize; value.BoundsMax = ((float3)grid.CellMax + 1) * grid.CellSize;
                    value.BoundsCenter = (value.BoundsMin + value.BoundsMax) * .5f; value.BoundingRadius = math.length(value.BoundsMax - value.BoundsCenter);
                    value.FunctionalRevision++; value.Flags |= ShipStatsFlags.Initialized;
                }
                if (full || massChanged || !hasMass && (value.Flags & ShipStatsFlags.HasMass) != 0)
                {
                    value.Mass = hasMass && masses[i].InverseMass > 0 ? 1 / masses[i].InverseMass : 0;
                    value.Flags &= ~ShipStatsFlags.HasMass;
                    if (value.Mass > 0)
                    {
                        value.Flags |= ShipStatsFlags.HasMass;
                        if (value.InitialMass == 0) value.InitialMass = value.Mass;
                        value.MassFraction = value.Mass / value.InitialMass;
                    }
                    else value.MassFraction = 1;
                }
                if (full || modChanged)
                {
                    var result = new ShipStatMultipliers();
                    for (int stat = 0; stat <= (int)StatId.TurnRate; stat++) result.Values.Add(1);
                    if (hasModifiers)
                        foreach (var modifier in modifiers[i]) result.Values[(int)modifier.Stat] *= modifier.Multiplier;
                    multipliers[i] = result;
                }
                stats[i] = value;
            }
        }
    }
}
