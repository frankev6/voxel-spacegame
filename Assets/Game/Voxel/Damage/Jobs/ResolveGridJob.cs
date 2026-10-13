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
        [ReadOnly] public NativeList<RestoreSpan> Restores;
        [ReadOnly] public NativeList<RestoreHealedInstance> PreviouslyHealed;
        public NativeArray<ResolvedGrid> Results;
        public NativeStream.Writer Removed, Touched, DestroyList, Explosions, Restored, HealedInstances;
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
            int healed = 0, placed = 0, restoreDropped = 0;
            Restored.BeginForEachIndex(index); HealedInstances.BeginForEachIndex(index);
            for (int r = range.RestoreStart; r < range.RestoreStart + range.RestoreCount; r++)
            {
                var span = Restores[r]; var seen = new NativeHashSet<int>(16, Allocator.Temp);
                for (int h = 0; h < PreviouslyHealed.Length; h++)
                    if (PreviouslyHealed[h].RequestId == span.Id) seen.Add(PreviouslyHealed[h].Instance);
                long width = (long)span.Max.x - span.Min.x + 1, height = (long)span.Max.y - span.Min.y + 1;
                for (int n = 0; n < span.Count; n++)
                {
                    long offset = span.Start + n;
                    int3 cell = span.Min + new int3((int)(offset % width), (int)(offset / width % height), (int)(offset / (width * height)));
                    if (span.Request.Mode == CellRestoreMode.Place)
                    {
                        if (writer.PlaceCell(span.Request, out var added)) { Restored.Write(added); placed++; }
                        else restoreDropped++;
                    }
                    else
                    {
                        int instance = writer.Access.TryGetCell(cell, out var prior, out _, out _) && prior.IsInstance ? prior.InstanceIndex : -1;
                        bool first = instance >= 0 && !seen.Contains(instance);
                        if (writer.HealCell(cell, span.Request.HpFraction, ref seen)) healed++;
                        if (first) HealedInstances.Write(new RestoreHealedInstance { RequestId = span.Id, Instance = instance });
                    }
                }
                seen.Dispose();
            }
            Restored.EndForEachIndex(); HealedInstances.EndForEachIndex();
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
                Healed = healed, Placed = placed, RestoreDropped = restoreDropped,
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
