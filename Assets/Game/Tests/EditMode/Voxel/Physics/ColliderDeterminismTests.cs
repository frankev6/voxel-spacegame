using Game.App.Systems;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;
using Fixture = Game.Tests.EditMode.Voxel.Physics.GridBodyInitTests.Fixture;

namespace Game.Tests.EditMode.Voxel.Physics
{
    internal struct DeterminismRun : IComponentData { public uint Start, End; }
    internal struct PhysicsTickRecord
    {
        public uint Tick, Serial, InstalledTick;
        public ulong ColliderHash;
        public PhysicsMass Mass;
        public PhysicsVelocity Velocity;
    }

    [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateBefore(typeof(GridMassUpdateSystem))]
    internal partial struct DeterminismDamageSystem : ISystem
    {
        public NativeArray<Entity> Roots;
        DamageJob job;
        public void OnCreate(ref SystemState state)
        {
            Roots = new NativeArray<Entity>(3, Allocator.Persistent);
            job.Grids = state.GetComponentLookup<VoxelGrid>(); job.Bricks = state.GetComponentLookup<Brick>(); job.Dirty = state.GetComponentLookup<BrickDirty>();
            job.Recompute = state.GetComponentLookup<GridMassRecompute>(); job.Slots = state.GetBufferLookup<BrickSlot>(true);
            job.Cells = state.GetBufferLookup<BrickCells>(); job.Sparse = state.GetBufferLookup<BrickSparseCells>(); job.Masks = state.GetBufferLookup<BrickSolidMask>();
            state.RequireForUpdate<DeterminismRun>();
        }
        public void OnDestroy(ref SystemState state) { state.Dependency.Complete(); Roots.Dispose(); }
        public void OnUpdate(ref SystemState state)
        {
            uint relative = SystemAPI.GetSingleton<SimClock>().Tick - SystemAPI.GetSingleton<DeterminismRun>().Start;
            int index = relative switch { 0 => 0, 1 => 1, 4 => 2, 7 => 3, 12 => 4, 25 => 5, 49 => 6, 73 => 7, _ => -1 };
            if (index < 0) return;
            job.Grids.Update(ref state); job.Bricks.Update(ref state); job.Dirty.Update(ref state); job.Recompute.Update(ref state);
            job.Slots.Update(ref state); job.Cells.Update(ref state); job.Sparse.Update(ref state); job.Masks.Update(ref state);
            job.Roots = Roots; job.Event = index; state.Dependency = job.Schedule(state.Dependency);
        }
        [BurstCompile(CompileSynchronously = true)] struct DamageJob : IJob
        {
            [ReadOnly] public NativeArray<Entity> Roots;
            [ReadOnly] public BufferLookup<BrickSlot> Slots;
            public ComponentLookup<VoxelGrid> Grids;
            public ComponentLookup<Brick> Bricks;
            public ComponentLookup<BrickDirty> Dirty;
            public ComponentLookup<GridMassRecompute> Recompute;
            public BufferLookup<BrickCells> Cells;
            public BufferLookup<BrickSparseCells> Sparse;
            public BufferLookup<BrickSolidMask> Masks;
            public int Event;
            public void Execute()
            {
                for (int r = 0; r < Roots.Length; r++)
                {
                    Entity root = Roots[r]; var grid = Grids[root]; grid.Revision++;
                    int3 start = r == 0 ? new int3(0, 8 + Event % 4 * 8, 8 + Event / 4 * 24) : new int3(4 + Event * 8, 2, 4 + r * 8);
                    for (int z = 0; z < 4; z++) for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
                    {
                        int3 cell = start + new int3(x, y, z); Entity entity = Slots[root][BrickMath.SlotIndex(BrickMath.BrickOf(cell), grid.BrickMin, grid.BrickDims)].Brick;
                        var brick = Bricks[entity]; var dirty = Dirty[entity];
                        BrickWrite.Set(ref brick, Cells[entity], Sparse[entity], Masks[entity], ref dirty,
                            BrickMath.LocalIndex(BrickMath.LocalOf(cell)), default, grid.Revision);
                        Bricks[entity] = brick; Dirty[entity] = dirty; Dirty.SetComponentEnabled(entity, true);
                    }
                    Grids[root] = grid; Recompute.SetComponentEnabled(root, true);
                }
            }
        }
    }

    [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(GridMassUpdateSystem)), UpdateAfter(typeof(ColliderSnapshotSystem))]
    internal partial struct DeterminismRecordSystem : ISystem
    {
        public NativeArray<Entity> Roots;
        public NativeList<PhysicsTickRecord> Records;
        RecordJob job;
        JobHandle last;
        public void OnCreate(ref SystemState state)
        {
            Roots = new NativeArray<Entity>(3, Allocator.Persistent); Records = new NativeList<PhysicsTickRecord>(512, Allocator.Persistent);
            job.Grids = state.GetComponentLookup<VoxelGrid>(true); job.States = state.GetComponentLookup<GridColliderState>(true);
            job.Colliders = state.GetComponentLookup<PhysicsCollider>(true); job.Masses = state.GetComponentLookup<PhysicsMass>(true);
            job.Velocities = state.GetComponentLookup<PhysicsVelocity>(true); state.RequireForUpdate<DeterminismRun>();
        }
        public void OnDestroy(ref SystemState state) { last.Complete(); Roots.Dispose(); Records.Dispose(); }
        public void OnUpdate(ref SystemState state)
        {
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick; if (tick >= SystemAPI.GetSingleton<DeterminismRun>().End) return;
            job.Grids.Update(ref state); job.States.Update(ref state); job.Colliders.Update(ref state); job.Masses.Update(ref state); job.Velocities.Update(ref state);
            job.Roots = Roots; job.Records = Records; job.Tick = tick;
            // Preserve output ownership across catch-up ticks without adding a fixed-step completion.
            last = job.Schedule(JobHandle.CombineDependencies(last, state.Dependency)); state.Dependency = last;
        }
        [BurstCompile(CompileSynchronously = true)] unsafe struct RecordJob : IJob
        {
            [ReadOnly] public NativeArray<Entity> Roots;
            [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
            [ReadOnly] public ComponentLookup<GridColliderState> States;
            [ReadOnly] public ComponentLookup<PhysicsCollider> Colliders;
            [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            public NativeList<PhysicsTickRecord> Records;
            public uint Tick;
            public void Execute()
            {
                foreach (var root in Roots)
                {
                    var blob = Colliders[root].Value;
                    Records.Add(new PhysicsTickRecord { Tick = Tick, Serial = Grids[root].Serial, InstalledTick = States[root].InstalledTick,
                        ColliderHash = StableHash.Hash64((byte*)blob.GetUnsafePtr(), blob.Value.MemorySize),
                        Mass = Masses.HasComponent(root) ? Masses[root] : default, Velocity = Velocities.HasComponent(root) ? Velocities[root] : default });
                }
            }
        }
    }

    public sealed class ColliderDeterminismTests
    {
        static PhysicsTickRecord[] Run(int pattern, int workers)
        {
            int previous = JobsUtility.JobWorkerCount; JobsUtility.JobWorkerCount = workers;
            try
            {
                using var f = new Fixture(physics: true, extra: new[] { typeof(GridSpawnBodyInitSystem), typeof(SimClockAdvanceSystem), typeof(GridMassUpdateSystem),
                    typeof(BrickDirtyScheduleSystem), typeof(ColliderSnapshotSystem), typeof(BrickColliderBuildSystem), typeof(GridColliderCleanupSystem),
                    typeof(DeferredDisposalSystem), typeof(DeterminismDamageSystem), typeof(DeterminismRecordSystem) });
                using (var physics = f.Em.CreateEntityQuery(typeof(PhysicsStep)))
                {
                    var step = physics.GetSingleton<PhysicsStep>(); step.SolverIterationCount = 4; step.MultiThreaded = 1;
                    step.CollisionTolerance = .1f; step.SolverStabilizationHeuristicSettings.EnableSolverStabilization = false;
                    step.IncrementalDynamicBroadphase = false; step.IncrementalStaticBroadphase = true; physics.SetSingleton(step);
                }
                Entity station = f.Spawn(GridMotion.Static, station: true);
                Entity a = f.Spawn(flags: GridSpawnFlags.None, linear: new float3(1, 2, 3), angular: new float3(.1f, .2f, .3f));
                Entity b = f.Spawn(linear: new float3(-2, 0, 1), angular: new float3(0, .3f, 0));
                f.Em.SetComponentData(a, LocalTransform.FromPosition(new float3(200, 0, 0)));
                f.Em.SetComponentData(b, LocalTransform.FromPosition(new float3(400, 0, 0)));
                // Non-urgent spawn is expressly frame-driven. Prepare the same ready state before measuring simulation changes.
                // Only warm-up waits at frame boundaries; measured catch-up ticks retain the actual asynchronous pipeline.
                for (int i = 0; i < 32; i++) { f.Tick(); f.Batch.State.Value.Handle.Complete(); }
                Entity[] roots = { station, a, b };
                ref var damage = ref f.Test.World.Unmanaged.GetUnsafeSystemRef<DeterminismDamageSystem>(f.Test.World.GetExistingSystem<DeterminismDamageSystem>());
                ref var record = ref f.Test.World.Unmanaged.GetUnsafeSystemRef<DeterminismRecordSystem>(f.Test.World.GetExistingSystem<DeterminismRecordSystem>());
                var initialTicks = new uint[3];
                for (int i = 0; i < roots.Length; i++)
                {
                    initialTicks[i] = f.Em.GetComponentData<GridColliderState>(roots[i]).InstalledTick;
                    Assert.AreEqual(GridColliderPhase.Ready, f.Em.GetComponentData<GridColliderState>(roots[i]).Phase);
                    damage.Roots[i] = record.Roots[i] = roots[i];
                }
                uint start = f.Singleton<SimClock>().Tick; const int ticks = 96;
                f.Em.CreateSingleton(new DeterminismRun { Start = start, End = start + ticks }); GridColliderStats.ResetCounters(f.Em);
                using var deadline = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BrickColliderBuildSystem.Deadline");
                Assert.IsTrue(deadline.Valid); Assert.IsTrue(deadline.IsRunning);
                long deadlineTotal = 0, deadlineMax = 0;
                int frame = 0;
                while (f.Singleton<SimClock>().Tick < start + ticks)
                {
                    int count = pattern switch { 0 => 1, 1 => frame % 2 == 0 ? 2 : 3, 2 => frame % 2, _ => 6 };
                    long before = deadline.CurrentValue;
                    f.Test.StepFrame(count * Game.Tests.Common.EcsTestWorld.TickSeconds); frame++;
                    long elapsed = deadline.CurrentValue - before; deadlineTotal += elapsed;
                    if (elapsed > deadlineMax) deadlineMax = elapsed;
                    Assert.Less(frame, 300);
                }
                Assert.AreEqual(0, GridColliderStats.Gather(f.Em).LateInstalls, $"Pattern {pattern}, workers {workers}");
                Assert.AreEqual(ticks * 3, record.Records.Length);
                Assert.AreEqual(1008128 - 512, f.Em.GetComponentData<GridMass>(station).Data.CellCount);
                Assert.AreEqual(20000 - 512, f.Em.GetComponentData<GridMass>(a).Data.CellCount);
                Assert.AreEqual(20000 - 512, f.Em.GetComponentData<GridMass>(b).Data.CellCount);
                TestContext.WriteLine($"Pattern {pattern}, workers {workers}: {frame} frames, {ticks} sampled ticks, {record.Records.Length} records, no late installs");
                TestContext.WriteLine($"Deadline completion total/max {deadlineTotal * 1e-6:F6}/{deadlineMax * 1e-6:F6} ms across the run; default safety settings, not a reference performance gate.");
                var result = record.Records.AsArray().ToArray();
                uint[] events = { 0, 1, 4, 7, 12, 25, 49, 73 };
                for (int i = 0; i < result.Length; i++)
                {
                    var sample = result[i]; uint expectedTick = initialTicks[i % 3];
                    foreach (uint e in events)
                    {
                        uint due = ColliderInstallSchedule.InstallTick(ColliderPriority.Damage, start + e, 7, 4);
                        if (due <= sample.Tick) expectedTick = due;
                    }
                    Assert.AreEqual(expectedTick, sample.InstalledTick, $"Scheduled install at {sample.Tick}, serial {sample.Serial}");
                }
                for (int i = 0; i < 3; i++) Assert.AreNotEqual(result[i].ColliderHash, result[result.Length - 3 + i].ColliderHash, "Damage changed each collider");
                return result;
            }
            finally { JobsUtility.JobWorkerCount = previous; }
        }
        static uint[] Fields(PhysicsTickRecord r)
        {
            var m = r.Mass; var v = r.Velocity;
            return new[] { math.asuint(m.Transform.rot.value.x), math.asuint(m.Transform.rot.value.y), math.asuint(m.Transform.rot.value.z), math.asuint(m.Transform.rot.value.w),
                math.asuint(m.Transform.pos.x), math.asuint(m.Transform.pos.y), math.asuint(m.Transform.pos.z), math.asuint(m.InverseMass),
                math.asuint(m.InverseInertia.x), math.asuint(m.InverseInertia.y), math.asuint(m.InverseInertia.z), math.asuint(m.AngularExpansionFactor),
                math.asuint(v.Linear.x), math.asuint(v.Linear.y), math.asuint(v.Linear.z), math.asuint(v.Angular.x), math.asuint(v.Angular.y), math.asuint(v.Angular.z) };
        }
        static void Equal(PhysicsTickRecord[] expected, PhysicsTickRecord[] actual, bool mass)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var a = expected[i]; var b = actual[i]; string where = $"tick {a.Tick}, serial {a.Serial}";
                Assert.AreEqual(a.Tick, b.Tick, where); Assert.AreEqual(a.Serial, b.Serial, where);
                Assert.AreEqual(a.InstalledTick, b.InstalledTick, where); Assert.AreEqual(a.ColliderHash, b.ColliderHash, where);
                if (mass) CollectionAssert.AreEqual(Fields(a), Fields(b), where); // Defined fields, never struct padding (T2.11).
            }
        }
        [Test] public void InstallTicks_IndependentOfFramePattern()
        {
            int workers = JobsUtility.JobWorkerMaximumCount; var expected = Run(0, workers);
            for (int pattern = 1; pattern < 4; pattern++) Equal(expected, Run(pattern, workers), true);
        }
        [Test] public void Mass_IndependentOfWorkerCount()
        {
            var expected = Run(0, 1);
            for (int pattern = 0; pattern < 4; pattern++)
            {
                if (pattern != 0) Equal(expected, Run(pattern, 1), true);
                Equal(expected, Run(pattern, JobsUtility.JobWorkerMaximumCount), true);
            }
        }
    }
}
