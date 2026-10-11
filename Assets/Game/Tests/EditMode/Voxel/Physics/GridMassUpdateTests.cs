using System;
using System.Collections.Generic;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Damage;
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
using Unity.Transforms;
using Fixture = Game.Tests.EditMode.Voxel.Physics.GridBodyInitTests.Fixture;

namespace Game.Tests.EditMode.Voxel.Physics
{
    [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    internal partial struct MassJobsTestSystem : ISystem
    {
        public NativeArray<CellRemoved> Cells;
        public NativeArray<GridRemovedRange> Ranges;
        public NativeList<Entity> Changed;
        GridMassRemoveJob remove;
        PhysicsMassApplyJob apply;
        public void OnCreate(ref SystemState state)
        {
            remove.Grids = state.GetComponentLookup<VoxelGrid>(true); remove.Masses = state.GetComponentLookup<GridMass>();
            apply.Masses = state.GetComponentLookup<GridMass>(true); apply.States = state.GetComponentLookup<GridColliderState>(true);
            apply.Grids = state.GetComponentLookup<VoxelGrid>(true); apply.Sources = state.GetComponentLookup<GridSource>(true); apply.Transforms = state.GetComponentLookup<LocalTransform>(true);
            apply.PhysicsMasses = state.GetComponentLookup<PhysicsMass>(); apply.Velocities = state.GetComponentLookup<PhysicsVelocity>();
        }
        public void OnUpdate(ref SystemState state)
        {
            remove.Grids.Update(ref state); remove.Masses.Update(ref state); apply.Masses.Update(ref state); apply.States.Update(ref state);
            apply.Grids.Update(ref state); apply.Sources.Update(ref state); apply.Transforms.Update(ref state); apply.PhysicsMasses.Update(ref state); apply.Velocities.Update(ref state);
            remove.Cells = Cells; remove.Ranges = Ranges; remove.Changed = Changed; remove.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value;
            var removed = remove.Schedule(state.Dependency); apply.Changed = Changed; state.Dependency = apply.Schedule(removed);
        }
    }

    public sealed class GridMassUpdateTests
    {
        static List<CellRemoved> Cells(Fixture f, Entity root)
        {
            var result = new List<CellRemoved>(); var palette = f.Em.GetBuffer<PaletteEntry>(root); var instances = f.Em.GetBuffer<BlockInstance>(root);
            foreach (var slot in f.Em.GetBuffer<BrickSlot>(root))
            {
                if (slot.Brick == Entity.Null) continue;
                var brick = f.Em.GetComponentData<Brick>(slot.Brick); var masks = f.Em.GetBuffer<BrickSolidMask>(slot.Brick);
                var dense = f.Em.GetBuffer<BrickCells>(slot.Brick); var sparse = f.Em.GetBuffer<BrickSparseCells>(slot.Brick);
                for (int w = 0; w < masks.Length; w++)
                {
                    ulong bits = masks[w].Bits;
                    while (bits != 0)
                    {
                        int local = (w << 6) + math.tzcnt(bits); bits &= bits - 1; var cell = BrickWrite.Get(dense, sparse, local);
                        result.Add(new CellRemoved { Grid = root, Cell = BrickMath.CellOf(brick.Coord, local),
                            Block = cell.IsInstance ? instances[cell.InstanceIndex].Block : palette[cell.PaletteIndex].Block, Reason = CellRemovedReason.Destroyed });
                    }
                }
            }
            return result;
        }
        static void Erase(Fixture f, CellRemoved removed)
        {
            var grid = f.Em.GetComponentData<VoxelGrid>(removed.Grid); int slot = BrickMath.SlotIndex(BrickMath.BrickOf(removed.Cell), grid.BrickMin, grid.BrickDims);
            Entity entity = f.Em.GetBuffer<BrickSlot>(removed.Grid)[slot].Brick; var brick = f.Em.GetComponentData<Brick>(entity); var dirty = f.Em.GetComponentData<BrickDirty>(entity);
            BrickWrite.Set(ref brick, f.Em.GetBuffer<BrickCells>(entity), f.Em.GetBuffer<BrickSparseCells>(entity), f.Em.GetBuffer<BrickSolidMask>(entity),
                ref dirty, BrickMath.LocalIndex(BrickMath.LocalOf(removed.Cell)), default, 1);
            f.Em.SetComponentData(entity, brick); f.Em.SetComponentData(entity, dirty);
        }
        static int Remove(Fixture f, Entity root, List<CellRemoved> removed)
        {
            using var cells = new NativeArray<CellRemoved>(removed.ToArray(), Allocator.TempJob);
            var ranges = new NativeArray<GridRemovedRange>(1, Allocator.TempJob); ranges[0] = new GridRemovedRange { Grid = root, Count = cells.Length };
            using var changed = new NativeList<Entity>(Allocator.TempJob);
            try
            {
                var handle = f.Test.World.GetOrCreateSystem<MassJobsTestSystem>();
                ref var system = ref f.Test.World.Unmanaged.GetUnsafeSystemRef<MassJobsTestSystem>(handle);
                system.Cells = cells; system.Ranges = ranges; system.Changed = changed;
                handle.Update(f.Test.World.Unmanaged); f.Test.CompleteAll(); return changed.Length;
            }
            finally { ranges.Dispose(); }
        }
        static double[] Values(StructureMassData d) => new[] { d.Mass, d.FirstMoment.x, d.FirstMoment.y, d.FirstMoment.z,
            d.SecondDiag.x, d.SecondDiag.y, d.SecondDiag.z, d.SecondOff.x, d.SecondOff.y, d.SecondOff.z, d.CellInertia };
        static void Close(StructureMassData a, StructureMassData b)
        {
            Assert.AreEqual(a.CellCount, b.CellCount); var left = Values(a); var right = Values(b);
            for (int i = 0; i < left.Length; i++) Assert.AreEqual(left[i], right[i], math.max(1, math.abs(left[i])) * 1e-9, "Accumulator " + i);
            var ca = a.FirstMoment / a.Mass; var cb = b.FirstMoment / b.Mass;
            for (int i = 0; i < 3; i++) Assert.AreEqual(ca[i], cb[i], math.max(1, math.abs(ca[i])) * 1e-9, "COM " + i);
            var da = a.SecondDiag; var db = b.SecondDiag;
            var ta = new double3(da.y + da.z, da.x + da.z, da.x + da.y) + a.CellInertia - a.Mass * new double3(ca.y * ca.y + ca.z * ca.z, ca.x * ca.x + ca.z * ca.z, ca.x * ca.x + ca.y * ca.y);
            var tb = new double3(db.y + db.z, db.x + db.z, db.x + db.y) + b.CellInertia - b.Mass * new double3(cb.y * cb.y + cb.z * cb.z, cb.x * cb.x + cb.z * cb.z, cb.x * cb.x + cb.y * cb.y);
            for (int i = 0; i < 3; i++) Assert.AreEqual(ta[i], tb[i], math.max(1, math.abs(ta[i])) * 1e-9, "Tensor diagonal " + i);
            var oa = -a.SecondOff + a.Mass * new double3(ca.x * ca.y, ca.y * ca.z, ca.z * ca.x);
            var ob = -b.SecondOff + b.Mass * new double3(cb.x * cb.y, cb.y * cb.z, cb.z * cb.x);
            for (int i = 0; i < 3; i++) Assert.AreEqual(oa[i], ob[i], math.max(1, math.abs(oa[i])) * 1e-9, "Tensor off-diagonal " + i);
        }
        [Test] public void Incremental_EqualsRecompute()
        {
            using var f = new Fixture(extra: new[] { typeof(GridMassUpdateSystem) }); var root = f.Spawn(); f.Tick();
            var all = Cells(f, root); var random = new Unity.Mathematics.Random(773); var removed = new List<CellRemoved>();
            for (int i = 0; i < 1000; i++)
            {
                int index = random.NextInt(all.Count); var cell = all[index]; all[index] = all[all.Count - 1]; all.RemoveAt(all.Count - 1);
                cell.Reason = (CellRemovedReason)(i % 3); removed.Add(cell); Erase(f, cell);
            }
            uint version = f.Em.GetComponentData<GridMass>(root).Version; Assert.AreEqual(1, Remove(f, root, removed));
            var incremental = f.Em.GetComponentData<GridMass>(root); Assert.AreEqual(version + 1, incremental.Version);
            f.Em.SetComponentEnabled<GridMassRecompute>(root, true); f.Tick(); Close(incremental.Data, f.Em.GetComponentData<GridMass>(root).Data);
        }
        [Test] public void Recompute_Deterministic_WorkerCounts()
        {
            using var f = new Fixture(extra: new[] { typeof(GridMassUpdateSystem) }); var root = f.Spawn(GridMotion.Static, station: true); f.Tick();
            int previous = JobsUtility.JobWorkerCount; StructureMassData reference = default;
            try
            {
                for (int run = 0; run < 4; run++)
                {
                    JobsUtility.JobWorkerCount = run % 2 == 0 ? 1 : JobsUtility.JobWorkerMaximumCount;
                    f.Em.SetComponentEnabled<GridMassRecompute>(root, true); f.Tick(); var data = f.Em.GetComponentData<GridMass>(root).Data;
                    if (run == 0) reference = data;
                    else
                    {
                        var a = Values(reference); var b = Values(data); Assert.AreEqual(reference.CellCount, data.CellCount);
                        for (int i = 0; i < a.Length; i++) Assert.AreEqual(BitConverter.DoubleToInt64Bits(a[i]), BitConverter.DoubleToInt64Bits(b[i]), "Field bits " + i);
                    }
                }
                TestContext.WriteLine($"{reference.CellCount} cells: bit-identical mass fields across 1/{JobsUtility.JobWorkerMaximumCount} workers, four recomputes.");
            }
            finally { JobsUtility.JobWorkerCount = previous; }
        }
        [Test] public void Dissolved_Ignored()
        {
            using var f = new Fixture(); var root = f.Spawn(); f.Tick(); var before = f.Em.GetComponentData<GridMass>(root);
            var cell = Cells(f, root)[0]; cell.Reason = CellRemovedReason.Dissolved;
            Assert.AreEqual(0, Remove(f, root, new List<CellRemoved> { cell })); Assert.AreEqual(before, f.Em.GetComponentData<GridMass>(root));
        }
        [Test] public void KinematicStatic_NotWritten()
        {
            using var f = new Fixture();
            foreach (var motion in new[] { GridMotion.Kinematic, GridMotion.Static })
            {
                var root = f.Spawn(motion); f.Tick();
                var sentinel = new PhysicsMass { Transform = RigidTransform.identity, InverseMass = .17f, InverseInertia = new float3(.2f), AngularExpansionFactor = 12 };
                if (!f.Em.HasComponent<PhysicsMass>(root)) f.Em.AddComponent<PhysicsMass>(root); f.Em.SetComponentData(root, sentinel);
                uint count = f.Em.GetComponentData<GridMass>(root).Data.CellCount;
                Assert.AreEqual(1, Remove(f, root, new List<CellRemoved> { Cells(f, root)[0] }));
                Assert.AreEqual(count - 1, f.Em.GetComponentData<GridMass>(root).Data.CellCount); Assert.AreEqual(sentinel, f.Em.GetComponentData<PhysicsMass>(root));
            }
        }
        static float3 PointVelocity(float3 point, LocalTransform transform, PhysicsMass mass, PhysicsVelocity velocity)
        {
            float3 omega = math.rotate(math.mul(transform.Rotation, mass.InertiaOrientation), velocity.Angular);
            float3 com = transform.Position + math.rotate(transform.Rotation, mass.CenterOfMass);
            return velocity.Linear + math.cross(omega, point - com);
        }
        [Test] public void SpinningShip_LosesEnd_PointVelocitiesKept()
        {
            using var f = new Fixture(); var root = f.Spawn(flags: GridSpawnFlags.None, linear: new float3(3, -2, 5), angular: new float3(.2f, -.4f, .8f), rotation: quaternion.EulerXYZ(.2f, .3f, -.4f)); f.Tick();
            var grid = f.Em.GetComponentData<VoxelGrid>(root); var transform = f.Em.GetComponentData<LocalTransform>(root);
            var beforeMass = f.Em.GetComponentData<PhysicsMass>(root); var beforeVelocity = f.Em.GetComponentData<PhysicsVelocity>(root);
            var removed = new List<CellRemoved>(); var retained = new List<CellRemoved>();
            foreach (var cell in Cells(f, root)) { if (cell.Cell.x >= grid.CellMax.x - 8) removed.Add(cell); else retained.Add(cell); }
            Assert.IsNotEmpty(removed); Remove(f, root, removed);
            var afterMass = f.Em.GetComponentData<PhysicsMass>(root); var afterVelocity = f.Em.GetComponentData<PhysicsVelocity>(root); float worst = 0;
            Assert.AreNotEqual(beforeMass.CenterOfMass, afterMass.CenterOfMass);
            foreach (var cell in retained)
            {
                var point = transform.Position + math.rotate(transform.Rotation, ((float3)cell.Cell + .5f) * grid.CellSize);
                worst = math.max(worst, math.distance(PointVelocity(point, transform, beforeMass, beforeVelocity), PointVelocity(point, transform, afterMass, afterVelocity)));
            }
            Assert.Less(worst, 1e-3f); TestContext.WriteLine($"Removed {removed.Count} cells; maximum retained-point velocity change {worst:G6} m/s.");
        }
        [Test] public void CellCountZero_NoWrite()
        {
            using var f = new Fixture(); var root = f.Spawn(); f.Tick(); var mass = f.Em.GetComponentData<PhysicsMass>(root); var velocity = f.Em.GetComponentData<PhysicsVelocity>(root);
            Remove(f, root, Cells(f, root)); Assert.AreEqual(0, f.Em.GetComponentData<GridMass>(root).Data.CellCount);
            Assert.AreEqual(mass, f.Em.GetComponentData<PhysicsMass>(root)); Assert.AreEqual(velocity, f.Em.GetComponentData<PhysicsVelocity>(root));
        }
    }
}
