using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>Canonical resolver lookups. FND-020 initially executes one ordered job (ADR-0053).</summary>
    internal struct DamageAccess
    {
        public ComponentLookup<VoxelGrid> Grids;
        public ComponentLookup<Brick> Bricks;
        public ComponentLookup<BrickDirty> Dirty;
        public ComponentLookup<GridDamageStats> Stats;
        public ComponentLookup<PendingImpulse> Impulses;
        public BufferLookup<BrickSlot> Slots;
        public BufferLookup<BlockInstance> Instances;
        public BufferLookup<BrickCells> Dense;
        public BufferLookup<BrickSparseCells> Sparse;
        public BufferLookup<BrickSolidMask> Masks;
        [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<DamageImmune> Immune;
        public static DamageAccess Create(ref SystemState s) => new DamageAccess
        {
            Grids = s.GetComponentLookup<VoxelGrid>(), Bricks = s.GetComponentLookup<Brick>(), Dirty = s.GetComponentLookup<BrickDirty>(),
            Stats = s.GetComponentLookup<GridDamageStats>(), Impulses = s.GetComponentLookup<PendingImpulse>(),
            Slots = s.GetBufferLookup<BrickSlot>(), Instances = s.GetBufferLookup<BlockInstance>(), Dense = s.GetBufferLookup<BrickCells>(),
            Sparse = s.GetBufferLookup<BrickSparseCells>(), Masks = s.GetBufferLookup<BrickSolidMask>(), Palettes = s.GetBufferLookup<PaletteEntry>(true),
            Transforms = s.GetComponentLookup<LocalTransform>(true), Masses = s.GetComponentLookup<PhysicsMass>(true),
            Velocities = s.GetComponentLookup<PhysicsVelocity>(true), Immune = s.GetComponentLookup<DamageImmune>(true)
        };
        public void Update(ref SystemState s)
        {
            Grids.Update(ref s); Bricks.Update(ref s); Dirty.Update(ref s); Stats.Update(ref s); Impulses.Update(ref s);
            Slots.Update(ref s); Instances.Update(ref s); Dense.Update(ref s); Sparse.Update(ref s); Masks.Update(ref s); Palettes.Update(ref s);
            Transforms.Update(ref s); Masses.Update(ref s); Velocities.Update(ref s); Immune.Update(ref s);
        }
        public GridCellAccess Cells(Entity root, BlobAssetReference<BlockDefsBlob> blocks) => new GridCellAccess
        {
            Grid = root, Definition = Grids[root], Slots = Slots[root], Palette = Palettes[root], Instances = Instances[root], Blocks = blocks,
            Dense = Dense, Sparse = Sparse, Masks = Masks, Bricks = Bricks, Dirty = Dirty
        };
    }
}
