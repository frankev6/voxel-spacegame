using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Streams;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;

namespace Game.Voxel.Damage
{
    /// <summary>FND-020 / ADR-0053: canonical, ordered damage resolution; DMG-006 adds parallel range execution.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(GridMassUpdateSystem)), UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public unsafe partial struct DamageResolveSystem : ISystem
    {
        DamageAccess access;
        static readonly ProfilerMarker Resolve = new ProfilerMarker(ProfilerCategory.Scripts, "DamageResolveSystem.Resolve");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            access = DamageAccess.Create(ref state);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<BlockModulesRef>(); state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate<SimClock>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            using var marker = Resolve.Auto(); access.Update(ref state);
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var data = streams.Data;
            state.Dependency = data->AllJobs(state.Dependency);
            var set = streams.Damage.BeginRead(ref state);
            var cleared = new ClearEventsJob { Events = data->SortedEvents }.Schedule(state.Dependency);
            var gathered = SimEventGather.Schedule(set, data->SortedEvents, cleared); streams.Damage.EndRead(gathered);
            var explosions = streams.Explosions.BeginWrite(ref state, ProducerKind.Explosion, 1);
            var handle = new ResolveJob
            {
                Access = access, Config = SystemAPI.GetSingleton<DamageConfig>(), Tick = SystemAPI.GetSingleton<SimClock>().Tick,
                Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value, Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value,
                Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value, Events = data->SortedEvents,
                Pending = data->PendingDetachCurrent, Removed = data->Removed, Ranges = data->RemovedGrids,
                Restored = data->Restored, RestoredRanges = data->RestoredGrids, Touched = data->TouchedBricks,
                DestroyList = data->DestroyList, Results = data->GridResults, Explosions = explosions, Stats = data
            }.Schedule(JobHandle.CombineDependencies(gathered, state.Dependency));
            streams.Explosions.EndWrite(handle); data->ResolverHandle = handle; data->RemovedWriteHandle = handle; state.Dependency = handle;
        }
        [BurstCompile] struct ClearEventsJob : IJob
        { public NativeList<DamageEvent> Events; public void Execute() => Events.Clear(); }
        [BurstCompile] struct ResolveJob : IJob
        {
            public DamageAccess Access;
            public DamageConfig Config;
            public uint Tick;
            public BlobAssetReference<Game.Data.Blocks.BlockDefsBlob> Blocks;
            public BlobAssetReference<Game.Data.Blocks.BlockModulesBlob> Modules;
            public BlobAssetReference<Game.Data.Blocks.OrientationTablesBlob> Orientations;
            public NativeList<DamageEvent> Events;
            [ReadOnly] public NativeList<PendingDetach> Pending;
            public NativeList<CellRemoved> Removed;
            public NativeList<CellRestored> Restored;
            public NativeList<GridRemovedRange> Ranges, RestoredRanges;
            public NativeList<TouchedBrick> Touched;
            public NativeList<Entity> DestroyList;
            public NativeList<DamageGridResult> Results;
            public NativeStream.Writer Explosions;
            [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Stats;
            struct Ordered : System.IComparable<Ordered>
            {
                public DamageEvent Event; public int Position;
                public int CompareTo(Ordered other)
                {
                    int c = Event.Grid.Index.CompareTo(other.Event.Grid.Index);
                    if (c == 0) c = Event.Source.Index.CompareTo(other.Event.Source.Index);
                    if (c == 0) c = ((byte)Event.Producer).CompareTo((byte)other.Event.Producer);
                    if (c == 0) c = Event.Sequence.CompareTo(other.Event.Sequence);
                    return c == 0 ? Position.CompareTo(other.Position) : c;
                }
            }
            struct RootOrder : System.Collections.Generic.IComparer<Entity>
            { public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index); }
            public void Execute()
            {
                Removed.Clear(); Ranges.Clear(); Restored.Clear(); RestoredRanges.Clear(); Touched.Clear(); DestroyList.Clear(); Results.Clear();
                using var ordered = new NativeList<Ordered>(Allocator.Temp);
                using var roots = new NativeList<Entity>(Allocator.Temp);
                for (int i = 0; i < Events.Length; i++)
                {
                    var e = Events[i]; Stats->Current.EventsIn++;
                    if (!Access.Grids.HasComponent(e.Grid) || Access.Immune.HasComponent(e.Grid) || e.Amount <= 0 ||
                        !math.isfinite(e.Amount) || !math.isfinite(e.Radius) || !math.isfinite(e.Penetration) ||
                        !math.all(math.isfinite(e.LocalPoint)) || !math.all(math.isfinite(e.LocalDir)) || !math.all(math.isfinite(e.WorldImpulse)))
                    { Stats->Current.EventsDropped++; continue; }
                    ordered.Add(new Ordered { Event = e, Position = i }); roots.Add(e.Grid);
                }
                ordered.Sort();
                foreach (var pending in Pending) if (Access.Grids.HasComponent(pending.Grid)) roots.Add(pending.Grid);
                roots.Sort(new RootOrder()); Explosions.BeginForEachIndex(0);
                int eventIndex = 0;
                for (int r = 0; r < roots.Length; r++)
                {
                    Entity root = roots[r]; if (r > 0 && root == roots[r - 1]) continue;
                    var writer = new CellWriter
                    {
                        Access = Access.Cells(root, Blocks), Config = Config, Tick = Tick, Transform = Access.Transforms[root],
                        Velocity = Access.Velocities.HasComponent(root) ? Access.Velocities[root] : default,
                        Mass = Access.Masses.HasComponent(root) ? Access.Masses[root] : new PhysicsMass { Transform = RigidTransform.identity },
                        Stats = Access.Stats[root], Modules = Modules, Orientations = Orientations, Explosions = Explosions
                    };
                    writer.Allocate(Allocator.Temp);
                    foreach (var pending in Pending)
                    {
                        if (pending.Grid != root) continue;
                        int3 coord = SlotCoord(pending.SlotIndex, writer.Access.Definition);
                        var mask = pending.Mask;
                        for (int local = 0; local < 4096; local++)
                            if ((mask.Words[local >> 6] & (1UL << (local & 63))) != 0)
                                writer.RemoveCell(BrickMath.CellOf(coord, local), pending.Reason);
                    }
                    if (writer.Changed) { writer.Stats.LastDamageSource = Entity.Null; writer.Stats.LastDamageInstigator = Entity.Null; }
                    // Instances moved to a new body keep their functional entity alive.
                    for (int i = 0; i < writer.Access.Instances.Length; i++)
                    {
                        var instance = writer.Access.Instances[i]; if (instance.Block == 0 || instance.FunctionHp <= 0) continue;
                        ref var def = ref Blocks.Value.Blocks[instance.Block]; bool any = false;
                        for (int c = 0; c < def.Cells.Length; c++)
                            if (writer.Access.TryGetCell(instance.Origin + Orientations.Value.Rotate(def.Cells[c], instance.Orient), out var cell, out _, out _) &&
                                cell.IsInstance && cell.InstanceIndex == i) { any = true; break; }
                        if (!any) { instance.FunctionHp = 0; instance.CellsAlive = 0; writer.Access.Instances[i] = instance; }
                    }
                    while (eventIndex < ordered.Length && ordered[eventIndex].Event.Grid.Index < root.Index) eventIndex++;
                    while (eventIndex < ordered.Length && ordered[eventIndex].Event.Grid == root)
                    {
                        var e = ordered[eventIndex++].Event; writer.Event = e;
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
                        Stats->Current.CellsDestroyed += writer.Stats.CellsDestroyed - Access.Stats[root].CellsDestroyed;
                        Stats->Current.CellsDetached += writer.Stats.CellsDetached - Access.Stats[root].CellsDetached;
                        Stats->Current.InstancesDestroyed += writer.Stats.InstancesDestroyed - Access.Stats[root].InstancesDestroyed;
                    }
                    Access.Stats[root] = writer.Stats; Stats->Current.GridsResolved++;
                    if (writer.Removed.Length > 0) Ranges.Add(new GridRemovedRange { Grid = root, Start = Removed.Length, Count = writer.Removed.Length });
                    Removed.AddRange(writer.Removed.AsArray());
                    int touchedStart = Touched.Length; Touched.AddRange(writer.Touched.AsArray()); DestroyList.AddRange(writer.DestroyList.AsArray());
                    Results.Add(new DamageGridResult { Grid = root, TouchedBrickStart = touchedStart, TouchedBrickCount = writer.Touched.Length,
                        CoreLost = writer.CoreLost ? (byte)1 : (byte)0, CoreWorldPos = writer.CoreWorldPos });
                    Explosions = writer.Explosions;
                    writer.Dispose();
                }
                Explosions.EndForEachIndex();
                foreach (var touch in Touched) Access.Dirty.SetComponentEnabled(touch.Brick, true);
            }
            static int3 SlotCoord(int index, VoxelGrid grid) => grid.BrickMin + new int3(index % grid.BrickDims.x,
                index / grid.BrickDims.x % grid.BrickDims.y, index / (grid.BrickDims.x * grid.BrickDims.y));
        }
    }
}
