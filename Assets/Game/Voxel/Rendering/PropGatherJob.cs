using System;
using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Voxel.Rendering
{
    internal struct PropEntry : IComparable<PropEntry>
    {
        public int Mesh;
        public PropInstanceGpu Instance;
        public int CompareTo(PropEntry other) => Mesh.CompareTo(other.Mesh);
    }

    /// <summary>Rebuild a flat, mesh-sorted instance stream only when applied prop lists change.</summary>
    [BurstCompile]
    internal struct PropGatherJob : IJob
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<LocalToWorld> Transforms;
        [ReadOnly] public BufferLookup<BrickPropInstance> Props;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
        [ReadOnly] public NativeArray<AABB> MeshBounds;
        public NativeList<PropEntry> Entries;
        public NativeList<PropInstanceGpu> Instances;
        public NativeHashMap<Entity, int> Slots;
        public NativeList<Entity> Roots;
        public NativeList<AABB> LocalBounds;
        public NativeArray<int> Counts, Starts;

        public void Execute()
        {
            Entries.Clear(); Instances.Clear(); Slots.Clear(); Roots.Clear(); LocalBounds.Clear();
            for (int m = 0; m < Counts.Length; m++) { Counts[m] = 0; Starts[m] = 0; }
            foreach (var entity in Entities)
            {
                var brick = Bricks[entity];
                if (!Grids.HasComponent(brick.Grid) || !Transforms.HasComponent(brick.Grid)) continue;
                var props = Props[entity];
                foreach (var prop in props)
                {
                    if (prop.Block == 0 || prop.Block >= Blocks.Value.Blocks.Length || prop.Orient >= 24) continue;
                    ref var def = ref Blocks.Value.Blocks[prop.Block]; int mesh = def.PropMeshIndex;
                    if (def.Kind != BlockKind.Prop || mesh < 0 || mesh >= Counts.Length) continue;
                    if (!Slots.TryGetValue(brick.Grid, out int slot))
                    {
                        // The shader contract has a 16-bit grid slot. Never wrap onto another grid.
                        if (Roots.Length == 65536) continue;
                        slot = Roots.Length; Slots.Add(brick.Grid, slot); Roots.Add(brick.Grid);
                        LocalBounds.Add(new AABB { Center = 0, Extents = -1 });
                    }
                    int index = prop.LocalIndex;
                    float3 center = (float3)(brick.Coord * 16 + new int3(index & 15, (index >> 4) & 15, index >> 8)) + .5f;
                    var rotation = new float3x3(Orientations.Value.Rotations[prop.Orient]); var bounds = MeshBounds[mesh];
                    float3 c = center + math.mul(rotation, bounds.Center);
                    float3 e = math.abs(rotation.c0) * bounds.Extents.x + math.abs(rotation.c1) * bounds.Extents.y + math.abs(rotation.c2) * bounds.Extents.z;
                    var previous = LocalBounds[slot];
                    float3 min = c - e, max = c + e;
                    if (previous.Extents.x >= 0) { min = math.min(min, previous.Min); max = math.max(max, previous.Max); }
                    LocalBounds[slot] = new AABB { Center = (min + max) * .5f, Extents = (max - min) * .5f };
                    Entries.Add(new PropEntry { Mesh = mesh, Instance = new PropInstanceGpu {
                        LocalCellCenter = center, Packed = (uint)prop.Orient | ((uint)prop.Color << 5) | ((uint)slot << 13) } });
                    Counts[mesh]++;
                }
            }
            Entries.AsArray().Sort();
            for (int m = 1; m < Starts.Length; m++) Starts[m] = Starts[m - 1] + Counts[m - 1];
            Instances.ResizeUninitialized(Entries.Length);
            for (int i = 0; i < Entries.Length; i++) Instances[i] = Entries[i].Instance;
        }
    }
}
