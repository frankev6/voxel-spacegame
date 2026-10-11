using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Budgeted snapshot-only collider construction outside the fixed step (SPEC-12 §5.8).</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VoxelRebuildGroup))]
    // REN-010 declares UpdateBefore(BrickColliderBuildSystem) when its mesh system is implemented (SPEC-12 Q17).
    public partial struct BrickColliderBuildSystem : ISystem
    {
        static readonly ProfilerMarker Access = new ProfilerMarker(ProfilerCategory.Scripts, "BrickColliderBuildSystem.Access");
        static readonly ProfilerMarker Finalize = new ProfilerMarker(ProfilerCategory.Scripts, "BrickColliderBuildSystem.Finalize");
        static readonly ProfilerMarker Seal = new ProfilerMarker(ProfilerCategory.Scripts, "BrickColliderBuildSystem.Seal");
        static readonly ProfilerMarker Plan = new ProfilerMarker(ProfilerCategory.Scripts, "BrickColliderBuildSystem.Plan");
        static readonly ProfilerMarker Copy = new ProfilerMarker(ProfilerCategory.Scripts, "BrickColliderBuildSystem.Copy");
        static readonly ProfilerMarker Schedule = new ProfilerMarker(ProfilerCategory.Scripts, "BrickColliderBuildSystem.Schedule");
        static readonly ProfilerMarker Deadline = new ProfilerMarker(ProfilerCategory.Scripts, "BrickColliderBuildSystem.Deadline");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<PhysicsVoxelConfig>();
            state.RequireForUpdate<DeferredDisposalQueue>(); state.RequireForUpdate<ColliderDirtyQueue>();
            state.RequireForUpdate<ColliderBuildCache>(); state.RequireForUpdate<ColliderBatchQueue>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var cache = SystemAPI.GetSingleton<ColliderBuildCache>(); var batch = SystemAPI.GetSingleton<ColliderBatchQueue>();
            var config = SystemAPI.GetSingleton<PhysicsVoxelConfig>(); uint frame = SystemAPI.GetSingleton<DeferredDisposalQueue>().Frame;
            using (Access.Auto()) cache.CompleteAccess();
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var status = batch.State.Value;
            if (status.InFlight != 0)
            {
                bool complete = status.Handle.IsCompleted;
                bool deadline = HasUrgentDeadline(cache.C, tick);
                if (!complete && !deadline && frame - status.ScheduledFrame < config.MaxBatchAgeFrames) return;
                using (Finalize.Auto())
                {
                    if (deadline) { using var marker = Deadline.Auto(); CompleteBatch(ref cache, ref batch); }
                    else CompleteBatch(ref cache, ref batch);
                }
            }
            using (Seal.Auto())
                for (int i = 0; i < cache.C.Updates.Length; i++)
                {
                    var update = cache.C.Updates[i];
                    if (update.State == UpdateState.Collecting && ColliderInstallSchedule.IsWindowClosed(update.InstallTick, tick, config.InstallDelayTicks))
                        ColliderPipeline.Seal(ref cache.C, i);
                }
            using (Plan.Auto()) PlanBatch(ref cache.C, ref batch.B, config);
            if (batch.B.Bricks.Length == 0 && batch.B.Assemblies.Length == 0) return;
            using (Schedule.Auto())
            {
                var bricks = new BrickColliderJob
                {
                    Items = batch.B.Bricks.AsArray(), Masks = batch.B.Masks.AsArray(), Results = batch.B.Results.AsArray(),
                    MaxBoxes = config.MaxBoxesPerBrick, Bevel = config.BevelFraction,
                }.Schedule(batch.B.Bricks.Length, 1);
                var assembled = new GridAssembleJob
                {
                    Items = batch.B.Assemblies.AsArray(), Children = batch.B.AssemblyChildren.AsArray(), Apply = batch.B.AssemblyApply.AsArray(),
                    Bricks = batch.B.Bricks.AsArray(), BrickResults = batch.B.Results.AsArray(), Results = batch.B.AssemblyResults.AsArray(),
                }.Schedule(batch.B.Assemblies.Length, 1, bricks);
                batch.State.Value = new ColliderBatchState { Handle = assembled, InFlight = 1, ScheduledFrame = frame };
                var stats = cache.C.Stats.Value; stats.Batches++; cache.C.Stats.Value = stats;
            }
            // ADR-0035: the next frame may run six ticks before this builder runs again.
            // Finish only deadline-critical assemblies here; installing them remains fixed-step work.
            for (int i = 0; i < batch.B.Assemblies.Length; i++)
                if (DueNextFrame(cache.C.Updates[batch.B.Assemblies[i].Update], tick))
                {
                    using var marker = Deadline.Auto();
                    CompleteBatch(ref cache, ref batch);
                    break;
                }
        }
        static bool DueNextFrame(in ColliderUpdate update, uint nextTick)
            => update.InstallTick != 0 && ColliderInstallSchedule.IsUrgent(update.Priority) &&
                update.InstallTick <= (ulong)nextTick + SimTime.MaxTicksPerFrame - 1;
        static bool HasUrgentDeadline(in ColliderCacheContainers cache, uint nextTick)
        {
            for (int i = 0; i < cache.Updates.Length; i++)
            {
                var update = cache.Updates[i];
                if ((update.State == UpdateState.Collecting || update.State == UpdateState.Pending || update.State == UpdateState.Building) &&
                    DueNextFrame(update, nextTick)) return true;
            }
            return false;
        }
        static void CompleteBatch(ref ColliderBuildCache cache, ref ColliderBatchQueue batch)
        {
            var status = batch.State.Value;
            bool forced = !status.Handle.IsCompleted;
            status.Handle.Complete();
            if (forced) { var stats = cache.C.Stats.Value; stats.ForcedBatchCompletes++; cache.C.Stats.Value = stats; }
            ColliderPipeline.Finalize(ref cache, ref batch); batch.State.Value = default;
        }
        struct Candidate : System.IComparable<Candidate>
        {
            public int Index;
            public uint Order;
            public byte Urgent;
            public int CompareTo(Candidate other) => Urgent != other.Urgent ? other.Urgent.CompareTo(Urgent) : Order.CompareTo(other.Order);
        }
        static void PlanBatch(ref ColliderCacheContainers cache, ref ColliderBatchContainers batch, PhysicsVoxelConfig config)
        {
            using var candidates = new NativeList<Candidate>(Allocator.Temp);
            using var handled = new NativeHashSet<int>(math.max(1, cache.Updates.Length), Allocator.Temp);
            using var assembled = new NativeHashSet<int>(math.max(1, cache.Updates.Length), Allocator.Temp);
            for (int i = 0; i < cache.Updates.Length; i++)
            {
                var u = cache.Updates[i];
                if (u.State == UpdateState.Pending || u.State == UpdateState.Building)
                    candidates.Add(new Candidate { Index = i, Order = u.CreateOrder, Urgent = ColliderInstallSchedule.IsUrgent(u.Priority) ? (byte)1 : (byte)0 });
            }
            candidates.Sort(); int bricks = 0, assemblies = 0, urgentBricks = 0;
            // Revisit urgent descendants after an older non-urgent update becomes fully selected in this batch.
            bool progress;
            do
            {
                progress = false;
                for (int i = 0; i < candidates.Length; i++)
                {
                    var candidate = candidates[i]; if (handled.Contains(candidate.Index)) continue;
                    var update = cache.Updates[candidate.Index]; var record = cache.Grids[cache.GridIndex[update.Serial]];
                    bool ready = true;
                    for (int j = 0; j < record.Updates.Length && record.Updates[j] != candidate.Index; j++)
                    {
                        int older = record.Updates[j];
                        if (cache.Updates[older].State != UpdateState.Built && !assembled.Contains(older)) { ready = false; break; }
                    }
                    if (!ready) continue;
                    bool urgent = candidate.Urgent != 0;
                    int remaining = update.Bricks.Length - update.BricksScheduled;
                    int take = urgent ? remaining : math.min(remaining, config.MaxBricksPerBatch - bricks);
                    if (take == 0 && remaining != 0) continue;
                    bool finish = take == remaining;
                    if (!urgent && finish && assemblies >= config.MaxAssembliesPerBatch) continue;
                    using (Copy.Auto()) CopyUpdate(ref cache, ref batch, candidate.Index, take, finish);
                    handled.Add(candidate.Index); progress = true;
                    if (urgent) urgentBricks += take; else { bricks += take; if (finish) assemblies++; }
                    if (finish) assembled.Add(candidate.Index);
                }
            } while (progress);
            if (batch.Bricks.Length == 0 && batch.Assemblies.Length == 0) return;
            var stats = cache.Stats.Value; stats.LastBatchBricks = batch.Bricks.Length; stats.LastBatchAssemblies = batch.Assemblies.Length;
            stats.LastBatchUrgentBricks = urgentBricks; cache.Stats.Value = stats;
        }
        static void CopyUpdate(ref ColliderCacheContainers cache, ref ColliderBatchContainers batch, int index, int count, bool finish)
        {
            var update = cache.Updates[index]; var record = cache.Grids[cache.GridIndex[update.Serial]];
            for (int i = 0; i < count; i++)
            {
                var brick = update.Bricks[update.BricksScheduled + i];
                var mask = ColliderPipeline.TakeNextMask(ref cache, index, out bool removed);
                batch.Bricks.Add(new BrickBuildItem { Update = index, Serial = update.Serial, Coord = brick.Coord, CellSize = record.CellSize,
                    Removed = removed ? (byte)1 : (byte)0, EditorPreview = record.IsEditorPreview });
                batch.Masks.Add(mask); batch.Results.Add(default);
            }
            update = cache.Updates[index]; update.State = UpdateState.Building; cache.Updates[index] = update;
            if (!finish) return;
            var item = new AssemblyItem { Update = index, Serial = update.Serial, CellSize = record.CellSize,
                ChildStart = batch.AssemblyChildren.Length, ChildCount = record.Children.Length, ApplyStart = batch.AssemblyApply.Length };
            for (int i = 0; i < record.Children.Length; i++) batch.AssemblyChildren.Add(record.Children[i]);
            for (int i = 0; i < batch.Bricks.Length; i++) if (batch.Bricks[i].Serial == update.Serial) batch.AssemblyApply.Add(i);
            item.ApplyCount = batch.AssemblyApply.Length - item.ApplyStart;
            batch.Assemblies.Add(item); batch.AssemblyResults.Add(default);
        }
    }
}
