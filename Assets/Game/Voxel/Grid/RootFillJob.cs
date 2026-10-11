using Game.Core.Components;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace Game.Voxel.Grid
{
    // Pointers borrow pinned source storage only until RootFillJob completes in this initialization update.
    internal unsafe struct RootFillData
    {
        public Entity Entity;
        public VoxelGrid Grid;
        public GridSpawnRequest Request;
        public StructureMassData Mass;
        [NativeDisableUnsafePtrRestriction] public PaletteEntry* Palette;
        [NativeDisableUnsafePtrRestriction] public BlockInstance* Instances;
        public int PaletteLength, InstanceLength, Token;
    }

    /// <summary>Batched root initialization; each iteration owns one root and its buffers (SPEC-10 §5.7).</summary>
    [BurstCompile]
    internal unsafe struct RootFillJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<RootFillData> Data;
        // All destinations are distinct roots selected once from the request query.
        [NativeDisableParallelForRestriction] public ComponentLookup<VoxelGrid> Grids;
        [NativeDisableParallelForRestriction] public ComponentLookup<SpawnReadToken> Tokens;
        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> Transforms;
        [NativeDisableParallelForRestriction] public ComponentLookup<PersistentId> Ids;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridSource> Sources;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridSpawning> Spawning;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridBodyInit> Bodies;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridMass> Masses;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridMassRecompute> Recompute;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridDespawnRequest> Despawn;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridRebuilt> Rebuilt;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridEditRequest> Edit;
        [NativeDisableParallelForRestriction] public ComponentLookup<Dormant> Dormant;
        [NativeDisableParallelForRestriction] public BufferLookup<PaletteEntry> Palettes;
        [NativeDisableParallelForRestriction] public BufferLookup<BlockInstance> Instances;
        [NativeDisableParallelForRestriction] public BufferLookup<BrickSlot> Slots;
        [NativeDisableParallelForRestriction] public BufferLookup<LinkedEntityGroup> Groups;
        public void Execute(int i)
        {
            var d = Data[i]; var e = d.Entity; var r = d.Request;
            Grids[e] = d.Grid; Tokens[e] = new SpawnReadToken { Value = d.Token };
            Transforms[e] = LocalTransform.FromPositionRotationScale(r.Pose.pos, r.Pose.rot, 1);
            Ids[e] = new PersistentId(r.PersistentId);
            Sources[e] = new GridSource { StructureHandle = r.StructureHandle, Blueprint = r.Blueprint, SyncedRevision = 1, Flags = r.Flags };
            Spawning[e] = default; Spawning.SetComponentEnabled(e, true);
            Bodies[e] = new GridBodyInit { Motion = r.Motion, LinearVelocity = r.LinearVelocity, AngularVelocity = r.AngularVelocity };
            Bodies.SetComponentEnabled(e, true);
            Masses[e] = new GridMass { Data = d.Mass }; Recompute.SetComponentEnabled(e, false);
            Despawn.SetComponentEnabled(e, false); Rebuilt.SetComponentEnabled(e, false);
            Edit.SetComponentEnabled(e, false); Dormant.SetComponentEnabled(e, false);
            var palette = Palettes[e]; palette.ResizeUninitialized(d.PaletteLength);
            UnsafeUtility.MemCpy(palette.GetUnsafePtr(), d.Palette, d.PaletteLength * UnsafeUtility.SizeOf<PaletteEntry>());
            var instances = Instances[e]; instances.ResizeUninitialized(d.InstanceLength);
            UnsafeUtility.MemCpy(instances.GetUnsafePtr(), d.Instances, d.InstanceLength * UnsafeUtility.SizeOf<BlockInstance>());
            for (int j = 0; j < instances.Length; j++) { var instance = instances[j]; instance.Functional = Entity.Null; instances[j] = instance; }
            var slots = Slots[e]; slots.ResizeUninitialized(d.Grid.BrickDims.x * d.Grid.BrickDims.y * d.Grid.BrickDims.z);
            UnsafeUtility.MemClear(slots.GetUnsafePtr(), slots.Length * UnsafeUtility.SizeOf<BrickSlot>());
            var group = Groups[e]; group.Clear(); group.Add(new LinkedEntityGroup { Value = e });
        }
    }
}
