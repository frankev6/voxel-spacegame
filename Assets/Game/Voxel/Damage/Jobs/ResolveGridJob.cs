using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.3: each range owns one grid and all its bricks/instances; no two workers write the same entity.</summary>
    [BurstCompile] internal unsafe struct ResolveGridJob : IJobParallelForDefer
    {
        public DamageAccess Access;
        public DamageConfig Config;
        public uint Tick;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public BlobAssetReference<BlockModulesBlob> Modules;
        [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
        [ReadOnly] public NativeArray<DamageGridRange> Ranges;
        [ReadOnly] public NativeList<DamageEvent> Events;
        [ReadOnly] public NativeList<PendingDetach> Pending;
        public NativeArray<ResolvedGrid> Results;
        public NativeStream.Writer Removed, Touched, DestroyList, Explosions;
        public void Execute(int index)
        {
            var range = Ranges[index]; var root = range.Grid;
            var writer = new CellWriter
            {
                RandomIdentity = Access.Ids.HasComponent(root) && Access.Ids[root].Value != 0 ? Access.Ids[root].Value : (ulong)(uint)root.Index,
                Access = Access.Cells(root, Blocks), Config = Config, Tick = Tick, Transform = Access.Transforms[root],
                Velocity = Access.Velocities.HasComponent(root) ? Access.Velocities[root] : default,
                Mass = Access.Masses.HasComponent(root) ? Access.Masses[root] : new PhysicsMass { Transform = RigidTransform.identity },
                Stats = Access.Stats[root], Modules = Modules, Orientations = Orientations, Explosions = Explosions
            };
            // NativeStream checks the writer address after Begin; keep the opened writer in its final CellWriter field.
            writer.Explosions.BeginForEachIndex(index);
            writer.Allocate(Allocator.Temp);
            for (int p = range.DetachStart; p < range.DetachStart + range.DetachCount; p++)
            {
                var pending = Pending[p];
                int3 coord = SlotCoord(pending.SlotIndex, writer.Access.Definition);
                var mask = pending.Mask;
                for (int local = 0; local < 4096; local++)
                    if ((mask.Words[local >> 6] & (1UL << (local & 63))) != 0)
                        writer.RemoveCell(BrickMath.CellOf(coord, local), pending.Reason);
            }
            if (writer.Changed) { writer.Stats.LastDamageSource = Entity.Null; writer.Stats.LastDamageInstigator = Entity.Null; }
            // Instances moved to a new body keep their functional entity alive.
            for (int i = 0; range.DetachCount > 0 && i < writer.Access.Instances.Length; i++)
            {
                var instance = writer.Access.Instances[i]; if (instance.Block == 0 || instance.FunctionHp <= 0) continue;
                ref var def = ref Blocks.Value.Blocks[instance.Block]; bool any = false;
                for (int c = 0; c < def.Cells.Length; c++)
                    if (writer.Access.TryGetCell(instance.Origin + Orientations.Value.Rotate(def.Cells[c], instance.Orient), out var cell, out _, out _) &&
                        cell.IsInstance && cell.InstanceIndex == i) { any = true; break; }
                if (!any)
                {
                    instance.FunctionHp = 0; instance.CellsAlive = 0; writer.Access.Instances[i] = instance;
                    if (writer.Access.Definition.AnchorInstance == i) { var grid = writer.Access.Definition; grid.AnchorInstance = -1; writer.Access.Definition = grid; }
                }
            }
            // TODO(DMG-019): bounded restore pass belongs between detach and damage.
            for (int eIndex = range.EventStart; eIndex < range.EventStart + range.EventCount; eIndex++)
            {
                var e = Events[eIndex]; writer.Event = e;
                writer.Row = Config.Table.IsCreated && (int)e.Type < Config.Table.Value.Types.Length ? Config.Table.Value.Types[(int)e.Type] : default;
                float before = writer.Stats.DamageTaken;
                if (e.Radius > 0) writer.BlastDamage(); else writer.PointDamage();
                if (writer.Stats.DamageTaken > before)
                { writer.Stats.LastDamageSource = e.Source; writer.Stats.LastDamageInstigator = e.Instigator == Entity.Null ? e.Source : e.Instigator; }
                if ((e.Flags & DamageEventFlags.NoImpulse) == 0)
                {
                    var impulse = Access.Impulses[root]; if (impulse.Tick != Tick) impulse = new PendingImpulse { Tick = Tick };
                    impulse.LinearWorld += e.WorldImpulse;
                    impulse.AngularWorld += math.cross(writer.Transform.TransformPoint(e.LocalPoint) - writer.Transform.TransformPoint(writer.Mass.CenterOfMass), e.WorldImpulse);
                    Access.Impulses[root] = impulse;
                }
            }
            if (writer.Changed)
            {
                var grid = writer.Access.Definition; grid.Revision++; Access.Grids[root] = grid;
                writer.Stats.LastDamageTick = Tick;
            }
            var beforeStats = Access.Stats[root];
            Access.Stats[root] = writer.Stats;
            var result = new ResolvedGrid
            {
                Result = new DamageGridResult { Grid = root, CoreLost = writer.CoreLost ? (byte)1 : (byte)0, CoreWorldPos = writer.CoreWorldPos },
                Destroyed = writer.Stats.CellsDestroyed - beforeStats.CellsDestroyed,
                Detached = writer.Stats.CellsDetached - beforeStats.CellsDetached,
                Instances = writer.Stats.InstancesDestroyed - beforeStats.InstancesDestroyed,
                RadiusClamped = writer.BlastRadiusClamped ? (byte)1 : (byte)0
            };
            Results[index] = result;
            Removed.BeginForEachIndex(index); foreach (var cell in writer.Removed) Removed.Write(cell); Removed.EndForEachIndex();
            Touched.BeginForEachIndex(index); foreach (var brick in writer.Touched) Touched.Write(brick); Touched.EndForEachIndex();
            DestroyList.BeginForEachIndex(index); foreach (var entity in writer.DestroyList) DestroyList.Write(entity); DestroyList.EndForEachIndex();
            writer.Explosions.EndForEachIndex();
            writer.Dispose();
        }
        static int3 SlotCoord(int index, VoxelGrid grid) => grid.BrickMin + new int3(index % grid.BrickDims.x,
            index / grid.BrickDims.x % grid.BrickDims.y, index / (grid.BrickDims.x * grid.BrickDims.y));
    }
}
