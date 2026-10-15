using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Voxel.Grid;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.PresentationStreams;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
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
    /// <summary>SPEC-13 §5.5 step 5: same-tick hulk creation, instance remap and functional ownership transfer.</summary>
    [BurstCompile] internal unsafe struct RecordHulkJob : IJob
    {
        public EntityArchetype RootType;
        [ReadOnly] public EntityStorageInfoLookup Entities;
        public GridCellReader Reader;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<PhysicsDamping> Damping;
        [ReadOnly] public ComponentLookup<PlanarConstraint> Planar;
        [ReadOnly] public ComponentLookup<PersistentId> Ids;
        [ReadOnly] public ComponentLookup<GridSource> Sources;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [ReadOnly] public BufferLookup<BlockInstance> Instances;
        [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
        [ReadOnly] public BufferLookup<LinkedEntityGroup> Groups;
        public BufferLookup<IntegritySeed> Seeds;
        public ComponentLookup<PersistentIdAllocator> Allocators;
        public ComponentLookup<GridSerialAllocator> Serials;
        [ReadOnly] public NativeList<IslandRecord> Islands;
        [ReadOnly] public NativeList<IslandMask> Masks;
        [ReadOnly] public NativeList<Entity> Destroy;
        public NativeList<PendingDetach> Detach;
        public Entity AllocatorEntity, SerialEntity, BrickPrototype;
        public EntityCommandBuffer Ecb;
        public DamageConfig Config; public PhysicsVoxelConfig PhysicsConfig;
        public BlobAssetReference<BlockDefsBlob> Blocks;
        public uint Tick;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Stats;
        public void Execute()
        {
            var allocator = Allocators[AllocatorEntity]; var serials = Serials[SerialEntity];
            using var moved = new NativeHashSet<Entity>(16, Allocator.Temp);
            foreach (var island in Islands)
            {
                Create(island, allocator.Next++, BrickPrototype, serials.Next++, moved);
                Stats->Current.HulksCreated++;
            }
            // Rewrite each old LEG once, after all of its moved functionals have been collected.
            Entity previous = Entity.Null;
            foreach (var island in Islands)
            {
                if (island.Grid == previous) continue; previous = island.Grid;
                var group = Ecb.SetBuffer<LinkedEntityGroup>(island.Grid);
                foreach (var member in Groups[island.Grid]) if (Entities.Exists(member.Value) && !moved.Contains(member.Value) && !Destroy.Contains(member.Value)) group.Add(member);
            }
            Allocators[AllocatorEntity] = allocator; Serials[SerialEntity] = serials;
        }
        void Create(IslandRecord island, ulong id, Entity brickPrototype, uint serial, NativeHashSet<Entity> moved)
        {
            Entity parent = island.Grid; var source = Reader.Grids[parent]; var transform = Transforms[parent];
            var oldInstances = Instances[parent]; var palette = Palettes[parent];
            var remap = new NativeArray<int>(oldInstances.Length, Allocator.Temp);
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;
            StructureMassData massData = default;
            for (int m = island.MaskStart; m < island.MaskStart + island.MaskCount; m++)
            {
                var mask = Masks[m]; Entity brick = Reader.Slots[parent][mask.SlotIndex].Brick; var coord = Bricks[brick].Coord;
                for (int c = 0; c < 4096; c++) if ((mask.Mask.Words[c >> 6] & (1UL << (c & 63))) != 0)
                {
                    var cell = BrickWrite.Get(Reader.Cells[brick], Reader.Sparse[brick], c);
                    ushort block = cell.IsInstance ? oldInstances[cell.InstanceIndex].Block : palette[cell.PaletteIndex].Block;
                    if (cell.IsInstance) remap[cell.InstanceIndex] = 0;
                    MassAccumulator.AddCell(ref massData, BrickMath.CellOf(coord, c), source.CellSize, Blocks.Value.Blocks[block].CellMass);
                }
            }
            Entity hulk = Ecb.CreateEntity(RootType);
            var grid = new VoxelGrid { BrickMin = BrickMath.BrickOf(island.CellMin), CellMin = island.CellMin, CellMax = island.CellMax,
                CellSize = source.CellSize, Revision = 1, AnchorInstance = -1, Kind = source.Kind == GridKind.Asteroid ? GridKind.Asteroid : GridKind.Hulk, Serial = serial };
            grid.BrickDims = BrickMath.BrickOf(island.CellMax) - grid.BrickMin + 1;
            Ecb.SetComponent(hulk, grid); Ecb.SetComponent(hulk, transform); Ecb.SetComponent(hulk, new PersistentId(id));
            Ecb.SetComponent(hulk, new GridSource { StructureHandle = -1, SyncedRevision = 1,
                Flags = Sources[parent].Flags & (GridSpawnFlags.NoRender | GridSpawnFlags.PlanarInertia) });
            Ecb.SetComponentEnabled<GridSpawning>(hulk, false); Ecb.SetComponentEnabled<GridDespawnRequest>(hulk, false);
            Ecb.SetComponentEnabled<GridRebuilt>(hulk, false); Ecb.SetComponentEnabled<GridEditRequest>(hulk, false); Ecb.SetComponentEnabled<Dormant>(hulk, false);
            Ecb.SetComponent(hulk, new GridMass { Data = massData }); Ecb.SetComponent(hulk, new GridBodyInit { Motion = GridMotion.Dynamic });
            Ecb.AddComponent(hulk, new SplitGrace { Other = parent, UntilTick = Tick + (uint)Config.SplitGraceTicks });
            Ecb.AddComponent(hulk, new SplitOrigin { Parent = parent });
            if (grid.Kind == GridKind.Hulk)
            {
                HulkSetup.Add(ref Ecb, hulk, new HulkInfo { Cause = HulkCause.Split, Tick = Tick,
                    OriginPersistentId = Ids[parent].Value, FormerKind = source.Kind });
            }
            var group = Ecb.SetBuffer<LinkedEntityGroup>(hulk); group.Add(new LinkedEntityGroup { Value = hulk });
            var newPalette = Ecb.SetBuffer<PaletteEntry>(hulk); newPalette.CopyFrom(palette);
            var instances = Ecb.SetBuffer<BlockInstance>(hulk);
            for (int i = 0; i < remap.Length; i++) if (remap[i] >= 0)
            {
                var instance = oldInstances[i]; remap[i] = instances.Length; instances.Add(instance);
                if (instance.Functional != Entity.Null)
                {
                    Ecb.SetComponent(instance.Functional, new FunctionalBlock { Grid = hulk, InstanceIndex = remap[i] });
                    Ecb.SetComponent(instance.Functional, new Parent { Value = hulk }); moved.Add(instance.Functional);
                }
            }
            var slots = Ecb.SetBuffer<BrickSlot>(hulk); slots.Resize(grid.BrickDims.x * grid.BrickDims.y * grid.BrickDims.z, NativeArrayOptions.ClearMemory);
            var baseline = Ecb.SetBuffer<GridSpawnBrick>(hulk);
            for (int m = island.MaskStart; m < island.MaskStart + island.MaskCount; m++)
            {
                var mask = Masks[m]; Entity old = Reader.Slots[parent][mask.SlotIndex].Brick; var coord = Bricks[old].Coord;
                int count = 0; for (int w = 0; w < 64; w++) count += math.countbits(mask.Mask.Words[w]);
                Entity brick = Ecb.Instantiate(brickPrototype);
                // Cleanup components are not copied by Instantiate. Match GridSpawnSystem's renderer registration.
                if ((Sources[parent].Flags & GridSpawnFlags.NoRender) == 0) Ecb.AddComponent<BrickRenderState>(brick);
                Ecb.SetComponent(brick, new Brick { Grid = hulk, Coord = coord, SolidCount = (ushort)count, ChangedRevision = 1 });
                Ecb.SetComponent(brick, LocalTransform.FromPositionRotationScale((float3)coord * (16 * grid.CellSize), quaternion.identity, grid.CellSize));
                Ecb.SetComponent(brick, new Parent { Value = hulk });
                Ecb.SetComponent(brick, new BrickDirty { Flags = BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider }); Ecb.SetComponentEnabled<BrickDirty>(brick, true);
                var dense = Ecb.SetBuffer<BrickCells>(brick); var sparse = Ecb.SetBuffer<BrickSparseCells>(brick);
                if (count > SparseCellStorage.Limit) dense.Resize(4096, NativeArrayOptions.ClearMemory);
                var solid = Ecb.SetBuffer<BrickSolidMask>(brick);
                for (int w = 0; w < 64; w++) solid.Add(new BrickSolidMask { Bits = mask.Mask.Words[w] });
                for (int c = 0; c < 4096; c++) if ((mask.Mask.Words[c >> 6] & (1UL << (c & 63))) != 0)
                {
                    var cell = BrickWrite.Get(Reader.Cells[old], Reader.Sparse[old], c);
                    if (cell.IsInstance) cell.Ref = (ushort)(Cell.InstanceBit | remap[cell.InstanceIndex]);
                    if (count > SparseCellStorage.Limit) dense[c] = new BrickCells { Value = cell };
                    else sparse.Add(new BrickSparseCells { Local = (ushort)c, Value = cell });
                }
                slots[BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims)] = new BrickSlot { Brick = brick };
                group.Add(new LinkedEntityGroup { Value = brick }); baseline.Add(new GridSpawnBrick { Coord = coord });
            }
            // Canonical LEG order: root, bricks in island mask order, then functionals in old instance order.
            for (int i = 0; i < remap.Length; i++)
                if (remap[i] >= 0 && oldInstances[i].Functional != Entity.Null)
                    group.Add(new LinkedEntityGroup { Value = oldInstances[i].Functional });
            bool planar = Planar.HasComponent(parent);
            var mass = GridMassMath.CreateDynamicMass(massData, grid.CellMin, grid.CellMax, grid.CellSize, planar);
            var oldMass = Masses.HasComponent(parent) ? Masses[parent] : new PhysicsMass { Transform = RigidTransform.identity };
            var oldVelocity = Velocities.HasComponent(parent) ? Velocities[parent] : default;
            float3 com = transform.TransformPoint(mass.CenterOfMass), parentCom = transform.TransformPoint(oldMass.CenterOfMass);
            float3 direction = com - parentCom; direction.y = 0; direction = math.normalizesafe(direction, math.rotate(transform.Rotation, new float3(1, 0, 0)));
            float3 omega = math.rotate(math.mul(transform.Rotation, oldMass.InertiaOrientation), oldVelocity.Angular);
            var velocity = new PhysicsVelocity { Linear = DamageMath.VelocityAtPoint(oldVelocity, oldMass, transform, com) + direction * Config.HulkSeparationSpeed,
                Angular = math.rotate(math.inverse(math.mul(transform.Rotation, mass.InertiaOrientation)), omega) };
            Ecb.AddComponent(hulk, GridBodyFactory.DynamicBodyTypes()); Ecb.AddSharedComponent(hulk, GridBodyFactory.WorldIndex());
            var provisional = GridBodyFactory.CreateProvisionalCollider(grid.CellMin, grid.CellMax, grid.CellSize, false);
            Ecb.SetComponent(hulk, new PhysicsCollider { Value = provisional });
            Ecb.AddComponent(hulk, new GridUnadoptedCollider { Value = provisional });
            Ecb.SetComponent(hulk, mass); Ecb.SetComponent(hulk, velocity);
            Ecb.SetComponent(hulk, Damping.HasComponent(parent) ? Damping[parent] : GridBodyFactory.DefaultDamping(PhysicsConfig));
            Ecb.SetComponent(hulk, new PhysicsGraphicalSmoothing { ApplySmoothing = 1 });
            Ecb.SetComponent(hulk, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = new RigidTransform(transform.Rotation, transform.Position), PreviousVelocity = velocity });
            if (planar) Ecb.AddComponent(hulk, Planar[parent]);
            remap.Dispose();
        }
    }
}
