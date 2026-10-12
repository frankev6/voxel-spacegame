using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>Resolver lookups: each grid range exclusively owns its root, bricks and instances (SPEC-13 §3 decision 4).</summary>
    internal struct DamageAccess
    {
        [NativeDisableParallelForRestriction] public ComponentLookup<VoxelGrid> Grids;
        [NativeDisableParallelForRestriction] public ComponentLookup<Brick> Bricks;
        [NativeDisableParallelForRestriction] public ComponentLookup<BrickDirty> Dirty;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridDamageStats> Stats;
        [NativeDisableParallelForRestriction] public ComponentLookup<PendingImpulse> Impulses;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickSlot> Slots;
        [NativeDisableParallelForRestriction] public BufferLookup<BlockInstance> Instances;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickCells> Dense;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickSparseCells> Sparse;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickSolidMask> Masks;
        [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<DamageImmune> Immune;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        public static DamageAccess Create(ref SystemState s) => new DamageAccess
        {
            Grids = s.GetComponentLookup<VoxelGrid>(), Bricks = s.GetComponentLookup<Brick>(), Dirty = s.GetComponentLookup<BrickDirty>(),
            Stats = s.GetComponentLookup<GridDamageStats>(), Impulses = s.GetComponentLookup<PendingImpulse>(),
            Slots = s.GetBufferLookup<BrickSlot>(), Instances = s.GetBufferLookup<BlockInstance>(), Dense = s.GetBufferLookup<BrickCells>(),
            Sparse = s.GetBufferLookup<BrickSparseCells>(), Masks = s.GetBufferLookup<BrickSolidMask>(), Palettes = s.GetBufferLookup<PaletteEntry>(true),
            Transforms = s.GetComponentLookup<LocalTransform>(true), Masses = s.GetComponentLookup<PhysicsMass>(true),
            Velocities = s.GetComponentLookup<PhysicsVelocity>(true), Immune = s.GetComponentLookup<DamageImmune>(true), Spawning = s.GetComponentLookup<GridSpawning>(true)
        };
        public void Update(ref SystemState s)
        {
            Grids.Update(ref s); Bricks.Update(ref s); Dirty.Update(ref s); Stats.Update(ref s); Impulses.Update(ref s);
            Slots.Update(ref s); Instances.Update(ref s); Dense.Update(ref s); Sparse.Update(ref s); Masks.Update(ref s); Palettes.Update(ref s);
            Transforms.Update(ref s); Masses.Update(ref s); Velocities.Update(ref s); Immune.Update(ref s); Spawning.Update(ref s);
        }
        public GridCellAccess Cells(Entity root, BlobAssetReference<BlockDefsBlob> blocks) => new GridCellAccess
        {
            Grid = root, Definition = Grids[root], Slots = Slots[root], Palette = Palettes[root], Instances = Instances[root], Blocks = blocks,
            Dense = Dense, Sparse = Sparse, Masks = Masks, Bricks = Bricks, Dirty = Dirty
        };
    }
}
