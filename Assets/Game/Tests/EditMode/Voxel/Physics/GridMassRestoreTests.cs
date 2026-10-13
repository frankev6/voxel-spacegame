using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Voxel.Physics
{
    /// <summary>PHY-022: production mass consumption of canonical removal/restoration views (SPEC-12 §5.11, ADR-0016).</summary>
    public sealed class GridMassRestoreTests : GameTestBase
    {
        [BurstCompile]
        struct PublishMassEventsJob : IJob
        {
            [ReadOnly] public NativeArray<CellRestored> Input;
            public NativeList<CellRemoved> Removed;
            public NativeList<GridRemovedRange> RemovedRanges;
            public NativeList<CellRestored> Restored;
            public NativeList<GridRemovedRange> RestoredRanges;
            public Entity Root;
            public byte Restore;

            public void Execute()
            {
                Removed.Clear(); RemovedRanges.Clear(); Restored.Clear(); RestoredRanges.Clear();
                for (int i = 0; i < Input.Length; i++)
                {
                    var cell = Input[i];
                    if (Restore == 1) Restored.Add(cell);
                    else Removed.Add(new CellRemoved { Grid = Root, Cell = cell.Cell, Block = cell.Block, Reason = CellRemovedReason.Destroyed });
                }
                var range = new GridRemovedRange { Grid = Root, Count = Input.Length };
                if (Restore == 1) RestoredRanges.Add(range);
                else RemovedRanges.Add(range);
            }
        }

        static unsafe void Publish(EcsTestWorld world, Entity root, NativeArray<CellRestored> input, bool restore)
        {
            using var query = world.EntityManager.CreateEntityQuery(typeof(DamageStreams));
            var data = query.GetSingleton<DamageStreams>().Data;
            // Test-only producer substitutes for DMG-019 while exercising the real reader dependency and lifetime contract.
            var dependency = data->AllJobs(default);
            data->RemovedReaderHandles.Clear();
            data->RemovedWriteHandle = new PublishMassEventsJob
            {
                Root = root, Input = input, Restore = (byte)(restore ? 1 : 0),
                Removed = data->Removed, RemovedRanges = data->RemovedGrids,
                Restored = data->Restored, RestoredRanges = data->RestoredGrids
            }.Schedule(dependency);
            world.UpdateSystem<GridMassUpdateSystem>();
            world.CompleteAll();
        }

        [Test]
        public void RemoveThenRestore_RoundTrips()
        {
            using var content = new VoxelTestContent();
            var world = CreateWorld(typeof(DamageStreamsSystem), typeof(GridMassUpdateSystem));
            world.AddGameplayActive(); world.AddSimClock();
            var em = world.EntityManager;
            content.Service.CreateSingletons(em);
            using var restored = new NativeArray<CellRestored>(8, Allocator.TempJob);
            try
            {
                var root = em.CreateEntity(typeof(VoxelGrid), typeof(GridMass), typeof(GridMassRecompute), typeof(GridSpawning),
                    typeof(BrickSlot), typeof(GridColliderState), typeof(GridSource), typeof(LocalTransform), typeof(PhysicsMass), typeof(PhysicsVelocity));
                const float cellSize = .5f;
                var grid = new VoxelGrid { CellSize = cellSize, CellMin = new int3(-4, -2, -3), CellMax = new int3(5, 3, 6) };
                em.SetComponentData(root, grid);
                em.SetComponentEnabled<GridSpawning>(root, false);
                em.SetComponentEnabled<GridMassRecompute>(root, false);
                em.SetComponentData(root, new GridColliderState { Motion = GridMotion.Dynamic });
                em.SetComponentData(root, LocalTransform.Identity);
                ushort hull = content.Index("block_hull"), slope = content.Index("block_hull_angled");
                var initial = new StructureMassData();
                for (int i = 0; i < 24; i++)
                {
                    var cell = new int3(i % 6 - 3, i % 5 - 2, i / 6 - 1);
                    ushort block = i % 2 == 0 ? hull : slope;
                    MassAccumulator.AddCell(ref initial, cell, cellSize, content.Content.Blocks.Value.Blocks[block].CellMass);
                    if (i < restored.Length) restored[i] = new CellRestored { Grid = root, Cell = cell, Block = block };
                }
                em.SetComponentData(root, new GridMass { Data = initial, Version = 7 });
                em.SetComponentData(root, GridMassMath.CreateDynamicMass(initial, grid.CellMin, grid.CellMax, cellSize, false));

                Publish(world, root, restored, false);
                var removed = em.GetComponentData<GridMass>(root);
                Assert.AreEqual(initial.CellCount - restored.Length, removed.Data.CellCount);
                Assert.Less(removed.Data.Mass, initial.Mass);
                Assert.AreEqual(8, removed.Version);

                Publish(world, root, restored, true);
                var roundTrip = em.GetComponentData<GridMass>(root);
                Assert.AreEqual(9, roundTrip.Version);
                Assert.AreEqual(initial.CellCount, roundTrip.Data.CellCount);
                Close(initial.Mass, roundTrip.Data.Mass, "Mass");
                var expectedCom = initial.FirstMoment / initial.Mass;
                var actualCom = roundTrip.Data.FirstMoment / roundTrip.Data.Mass;
                for (int axis = 0; axis < 3; axis++) Close(expectedCom[axis], actualCom[axis], "COM " + axis);
                var expectedTensor = Tensor(initial);
                var actualTensor = Tensor(roundTrip.Data);
                for (int column = 0; column < 3; column++)
                    for (int row = 0; row < 3; row++) Close(expectedTensor[column][row], actualTensor[column][row], "Tensor " + column + "," + row);
                Assert.AreEqual(GridMassMath.CreateDynamicMass(roundTrip.Data, grid.CellMin, grid.CellMax, cellSize, false), em.GetComponentData<PhysicsMass>(root));
            }
            finally
            {
                // Destroy the channel owner and finish its readers before disposing the content blobs and producer input.
                DisposeTrackedWorlds();
            }
        }

        static double3x3 Tensor(in StructureMassData data)
        {
            var c = data.FirstMoment / data.Mass;
            var diagonal = new double3(data.SecondDiag.y + data.SecondDiag.z, data.SecondDiag.x + data.SecondDiag.z,
                data.SecondDiag.x + data.SecondDiag.y) + data.CellInertia - data.Mass * new double3(c.y * c.y + c.z * c.z, c.x * c.x + c.z * c.z, c.x * c.x + c.y * c.y);
            var off = -data.SecondOff + data.Mass * new double3(c.x * c.y, c.y * c.z, c.z * c.x);
            return new double3x3(new double3(diagonal.x, off.x, off.z), new double3(off.x, diagonal.y, off.y), new double3(off.z, off.y, diagonal.z));
        }

        static void Close(double expected, double actual, string field)
            => Assert.AreEqual(expected, actual, math.max(1d, math.abs(expected)) * 1e-9, field);
    }
}
