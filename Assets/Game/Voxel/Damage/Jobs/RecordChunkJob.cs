using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.5: serial canonical pool selection, eviction and chunk ECB recording.</summary>
    [BurstCompile] internal unsafe struct RecordChunkJob : IJob
    {
        [ReadOnly] public NativeList<IslandRecord> Islands;
        [ReadOnly] public NativeList<IslandMask> Masks;
        public GridCellReader Reader;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<ChunkColliderBlob> Colliders;
        [ReadOnly] public BufferLookup<ChunkCell> OldCells;
        [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        public NativeArray<Entity> Slots;
        public NativeArray<ChunkBody> Bodies;
        public NativeList<BlobAssetReference<Collider>> Retired;
        public EntityArchetype Archetype;
        public BlobAssetReference<BlockDefsBlob> Blocks;
        public DamageConfig Config;
        public uint Tick;
        public EntityCommandBuffer Ecb;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;

        public void Execute()
        {
            using var blobsStorage = new NativeArray<BlobAssetReference<Collider>>(Slots.Length, Allocator.Temp);
            var blobs = blobsStorage;
            using var offsetsStorage = new NativeArray<int>(Slots.Length, Allocator.Temp);
            var offsets = offsetsStorage;
            using var posesStorage = new NativeArray<LocalTransform>(Slots.Length, Allocator.Temp);
            var poses = posesStorage;
            using var velocitiesStorage = new NativeArray<PhysicsVelocity>(Slots.Length, Allocator.Temp);
            var velocities = velocitiesStorage;
            using var cached = new NativeList<ChunkCell>(Allocator.Temp);
            for (int i = 0; i < Slots.Length; i++)
            {
                var entity = Slots[i]; if (entity == Entity.Null) continue;
                blobs[i] = Colliders[entity].Value; poses[i] = Transforms[entity]; velocities[i] = Velocities[entity];
                offsets[i] = cached.Length;
                foreach (var cell in OldCells[entity]) cached.Add(cell);
            }
            foreach (var island in Islands)
            {
                int slot = SelectSlot(Bodies);
                var old = Bodies[slot];
                if (old.Live != 0)
                {
                    int stride = math.max(1, (old.CellCount + 63) / 64);
                    for (int i = 0; i < old.CellCount; i += stride)
                    {
                        var cell = cached[offsets[slot] + i];
                        float3 local = old.LocalOrigin + (new float3(cell.X, cell.Y, cell.Z) + .5f) * old.CellSize;
                        Fx.Enqueue(new FxEvent { Kind = FxKind.Debris, Flags = FxEventFlags.PaletteColor,
                            Pos = poses[slot].TransformPoint(local), Vel = velocities[slot].Linear,
                            Scale = old.CellSize, Color = (uint)(cell.Color | (Blocks.Value.Blocks[cell.Block].MaterialLayer << 8)) });
                    }
                }
                if (blobs[slot].IsCreated) Retired.Add(blobs[slot]);
                var grid = Reader.Grids[island.Grid]; var parent = Transforms[island.Grid];
                var palette = Palettes[island.Grid];
                using var cells = new NativeList<ChunkCell>(island.CellCount, Allocator.Temp);
                StructureMassData accumulator = default;
                for (int m = island.MaskStart; m < island.MaskStart + island.MaskCount; m++)
                {
                    var mask = Masks[m]; var brick = Reader.Slots[island.Grid][mask.SlotIndex].Brick;
                    for (int w = 0; w < 64; w++)
                    {
                        ulong bits = mask.Mask.Words[w];
                        while (bits != 0)
                        {
                            int index = w * 64 + math.tzcnt(bits); bits &= bits - 1;
                            var cell = BrickWrite.Get(Reader.Cells[brick], Reader.Sparse[brick], index);
                            var p = BrickMath.CellOf(Bricks[brick].Coord, index); var relative = p - island.CellMin;
                            var paint = cell.IsInstance ? new PaletteEntry { Block = Instances[island.Grid][cell.InstanceIndex].Block,
                                Orient = Instances[island.Grid][cell.InstanceIndex].Orient, Color = Instances[island.Grid][cell.InstanceIndex].Color } : palette[cell.PaletteIndex];
                            ushort block = paint.Block;
                            MassAccumulator.AddCell(ref accumulator, p, grid.CellSize, Blocks.Value.Blocks[block].CellMass);
                            cells.Add(new ChunkCell { X = (byte)relative.x, Y = (byte)relative.y, Z = (byte)relative.z,
                                Block = block, Color = paint.Color, Orient = paint.Orient });
                        }
                    }
                }
                var parentMass = Masses.HasComponent(island.Grid) ? Masses[island.Grid] : new PhysicsMass { Transform = RigidTransform.identity };
                var motion = IslandKinematics.Calculate(accumulator, island.CellMin, island.CellMax, grid.CellSize, parent,
                    parentMass, Velocities.HasComponent(island.Grid) ? Velocities[island.Grid] : default, Config.ChunkSeparationSpeed);
                float3 origin = (float3)island.CellMin * grid.CellSize - motion.LocalCenter;
                var blob = ChunkBoxBuilder.CreateCollider(cells.AsArray(), grid.CellSize, origin, Config.ChunkMaxBoxes);
                var random = Unity.Mathematics.Random.CreateFromIndex((uint)StableHash.Combine(Tick, (ulong)(uint)(island.Grid.Index ^ island.Ordinal)));
                motion.Velocity.Angular += random.NextFloat3Direction() * random.NextFloat(Config.ChunkTumbleMin, Config.ChunkTumbleMax);
                var pose = LocalTransform.FromPositionRotation(motion.WorldCenter, parent.Rotation);
                uint duration = (uint)(60 * math.lerp(Config.ChunkLifetimeMinSeconds, Config.ChunkLifetimeMaxSeconds,
                    math.saturate((island.CellCount - 9f) / math.max(1, Config.ChunkMaxCells - 9))));
                uint end = Tick + math.max(1u, duration);
                var body = new ChunkBody { SpawnTick = Tick, EndTick = end,
                    DissolveStartTick = end - math.min(duration, (uint)(60 * Config.ChunkDissolveSeconds)),
                    CellSize = grid.CellSize, LocalOrigin = origin, CellCount = (ushort)cells.Length, Slot = (byte)slot, Live = 1 };
                Entity entity = Slots[slot];
                if (entity == Entity.Null) { entity = Ecb.CreateEntity(Archetype); Ecb.SetSharedComponent(entity, GridBodyFactory.WorldIndex()); }
                ChunkBodySetup.Write(ref Ecb, entity, pose, motion.Mass, motion.Velocity, blob, body, cells.AsArray(), island.Grid,
                    Tick + (uint)Config.SplitGraceTicks, 0);
                Slots[slot] = entity; Bodies[slot] = body; blobs[slot] = blob; poses[slot] = pose; velocities[slot] = motion.Velocity;
                offsets[slot] = cached.Length; cached.AddRange(cells.AsArray());
                Data->Current.ChunksCreated++;
            }
        }
        internal static int SelectSlot(NativeArray<ChunkBody> bodies)
        {
            int slot = 0;
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].Live == 0) return i;
                if (bodies[i].EndTick < bodies[slot].EndTick) slot = i;
            }
            return slot;
        }
    }
}
