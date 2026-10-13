using Game.Core;
using Game.Data.Blocks;
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
    /// <summary>SPEC-13 §5.6: bounded exposed-cell presentation; never removes or changes cells.</summary>
    [BurstCompile] internal struct DissolveJob : IJob
    {
        public GridCellReader Reader;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
        [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public ComponentLookup<GridDissolveRequest> Requests;
        public ComponentLookup<GridDissolving> Progress;
        public NativeArray<Entity> RequestEntities, DissolvingEntities;
        public NativeList<Entity> Destroy;
        public BlobAssetReference<BlockDefsBlob> Blocks;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        public EntityCommandBuffer Ecb;
        public DamageConfig Config;
        public uint Tick;
        public void Execute()
        {
            RequestEntities.Sort(new EntityOrder()); DissolvingEntities.Sort(new EntityOrder());
            using var started = new NativeList<Entity>(Allocator.Temp);
            foreach (var entity in RequestEntities)
            {
                Destroy.Add(entity);
                var request = Requests[entity]; Entity root = request.Grid;
                if (!Reader.Grids.HasComponent(root)) continue;
                if (request.Mode == GridDissolveMode.Silent) { Destroy.Add(root); continue; }
                if (Progress.HasComponent(root) || started.Contains(root)) continue;
                started.Add(root);
                int estimate = 0;
                if (Reader.Slots.HasBuffer(root))
                    foreach (var slot in Reader.Slots[root])
                        if (Bricks.HasComponent(slot.Brick)) estimate += math.min((int)Bricks[slot.Brick].SolidCount, 1536);
                var progress = new GridDissolving { StartTick = Tick, Stride = math.max(1, (estimate - 1) / math.max(1, Config.DissolveParticleCap) + 1) };
                if (Advance(root, ref progress)) Destroy.Add(root);
                else Ecb.AddComponent(root, progress);
            }
            foreach (var root in DissolvingEntities)
            {
                if (Destroy.Contains(root)) continue;
                var progress = Progress[root];
                if (Advance(root, ref progress)) Destroy.Add(root);
                Progress[root] = progress;
            }
        }
        bool Advance(Entity root, ref GridDissolving progress)
        {
            if (!Reader.Grids.HasComponent(root) || !Reader.Slots.HasBuffer(root) || !Transforms.HasComponent(root)) return true;
            var slots = Reader.Slots[root]; var grid = Reader.Grids[root]; var transform = Transforms[root];
            var velocity = Velocities.HasComponent(root) ? Velocities[root] : default;
            var mass = Masses.HasComponent(root) ? Masses[root] : default;
            int emitted = 0, ordinal = 0, stride = math.max(1, progress.Stride);
            // Q35: NextSlot packs slot/local index so a budget can stop inside a brick.
            // A pause follows an emitted sample, so the next exposed-cell ordinal starts at a stride boundary.
            while ((progress.NextSlot >> 12) < slots.Length && progress.Emitted < Config.DissolveParticleCap)
            {
                int slotIndex = progress.NextSlot >> 12, local = progress.NextSlot & 4095;
                Entity brick = slots[slotIndex].Brick;
                if (!Bricks.HasComponent(brick)) { progress.NextSlot = (slotIndex + 1) << 12; continue; }
                progress.NextSlot++;
                int3 cellPosition = BrickMath.CellOf(Bricks[brick].Coord, local);
                if (!Reader.TryGetCell(root, cellPosition, out var cell) || !Exposed(root, cellPosition)) continue;
                if (++ordinal % stride != 0) continue;
                ushort block; byte color;
                if (cell.IsInstance) { var value = Instances[root][cell.InstanceIndex]; block = value.Block; color = value.Color; }
                else { var value = Palettes[root][cell.PaletteIndex]; block = value.Block; color = value.Color; }
                float3 point = transform.TransformPoint(BrickMath.CellCenter(cellPosition, grid.CellSize));
                var random = Unity.Mathematics.Random.CreateFromIndex((uint)StableHash.Combine(StableHash.Combine(Tick, (ulong)root.Index), DamageMath.Packed(cellPosition)));
                float3 direction = math.normalizesafe((point - transform.TransformPoint(mass.CenterOfMass)) * (1 - Config.KickRandomFraction) + random.NextFloat3Direction() * Config.KickRandomFraction);
                Fx.Enqueue(new FxEvent { Kind = FxKind.Debris, Flags = FxEventFlags.PaletteColor, Pos = point, Dir = direction,
                    Vel = DamageMath.VelocityAtPoint(velocity, mass, transform, point) + direction * Config.KickDissolve,
                    Scale = grid.CellSize, Color = (uint)(color | (Blocks.Value.Blocks[block].MaterialLayer << 8)) });
                progress.Emitted++;
                if (++emitted >= math.max(1, Config.DissolveParticlesPerTick)) break;
            }
            return (progress.NextSlot >> 12) >= slots.Length || progress.Emitted >= Config.DissolveParticleCap;
        }
        bool Exposed(Entity root, int3 c) => !Reader.IsSolid(root, c + new int3(1,0,0)) || !Reader.IsSolid(root, c - new int3(1,0,0)) ||
            !Reader.IsSolid(root, c + new int3(0,1,0)) || !Reader.IsSolid(root, c - new int3(0,1,0)) ||
            !Reader.IsSolid(root, c + new int3(0,0,1)) || !Reader.IsSolid(root, c - new int3(0,0,1));
        internal struct EntityOrder : System.Collections.Generic.IComparer<Entity>
        { public int Compare(Entity a, Entity b) { int c = a.Index.CompareTo(b.Index); return c == 0 ? a.Version.CompareTo(b.Version) : c; } }
    }
}
