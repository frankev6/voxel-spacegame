using System;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;
using Fixture = Game.Tests.EditMode.Voxel.Physics.GridBodyInitTests.Fixture;

namespace Game.Tests.EditMode.Voxel.Physics
{
    public sealed class GridBodyFactoryContractTests
    {
        /// <summary>
        /// SPEC-13 creator sequence (SPEC-12 §5.6): create the root/brick and voxel buffers in the
        /// gameplay-end ECB; set the island GridMass and pose; leave GridBodyInit and GridMassRecompute
        /// enabled, GridSpawning/GridRebuilt disabled. AddComponent(root, DynamicBodyTypes());
        /// SetComponent PhysicsCollider(CreateProvisionalCollider(island bounds)), PhysicsMass
        /// (CreateDynamicMass(island accumulators)), PhysicsVelocity (angular in inertia frame),
        /// PhysicsDamping (parent's or DefaultDamping), PhysicsGraphicalSmoothing, and
        /// PhysicsGraphicalInterpolationBuffer (hulk pose/velocity); AddSharedComponent(WorldIndex()).
        /// Playback at the end of tick T. The real body-init system adopts ownership on T+1 and queues
        /// Split; physics moves immediately. Retain no creator ownership after adoption.
        /// </summary>
        [Test] public void SplitCreatedRoot_LifecycleToReady()
        {
            TestContext.WriteLine("Creator: gameplay-end ECB CreateEntity(root + brick); populate voxel components/buffers, exact GridMass and pose; " +
                "enable GridBodyInit/GridMassRecompute, disable GridSpawning/GridRebuilt; AddComponent(root, GridBodyFactory.DynamicBodyTypes()); " +
                "SetComponent PhysicsCollider(GridBodyFactory.CreateProvisionalCollider(island bounds)), PhysicsMass(GridMassMath.CreateDynamicMass), " +
                "PhysicsVelocity(inertia-frame angular), PhysicsDamping(GridBodyFactory.DefaultDamping), PhysicsGraphicalSmoothing, " +
                "PhysicsGraphicalInterpolationBuffer(pose/velocity); AddSharedComponent(root, GridBodyFactory.WorldIndex()); " +
                "playback at end T; GridBodyInitSystem adopts on T+1 and queues Split. Creator no longer owns the blob.");
            var previous = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try { Exercise(); UnsafeUtility.CheckForLeaks(); Exercise(); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks()); }
            finally { NativeLeakDetection.Mode = previous; }
        }

        static void Exercise()
        {
            using var f = new Fixture(physics: true, extra: new[] { typeof(GridMassUpdateSystem), typeof(BrickDirtyScheduleSystem),
                typeof(ColliderSnapshotSystem), typeof(BrickColliderBuildSystem), typeof(GridColliderCleanupSystem), typeof(DeferredDisposalSystem) });
            var em = f.Em; var grid = new VoxelGrid { Serial = 1, CellSize = .5f, BrickDims = 1, Kind = GridKind.Hulk, AnchorInstance = -1 };
            var mass = new StructureMassData(); ushort block = f.Content.Index("block_hull");
            MassAccumulator.AddCell(ref mass, 0, grid.CellSize, f.Singleton<BlockDefsRef>().Value.Value.Blocks[block].CellMass);
            var provisional = GridBodyFactory.CreateProvisionalCollider(0, 0, grid.CellSize, false);
            bool adopted = false;
            try
            {
                using var serialsQuery = em.CreateEntityQuery(typeof(GridSerialAllocator));
                serialsQuery.SetSingleton(new GridSerialAllocator { Next = 2 });
                var ecb = f.Test.World.GetExistingSystemManaged<GameplayEndCommandBufferSystem>().CreateCommandBuffer();
                Entity root = ecb.CreateEntity(); Entity brick = ecb.CreateEntity();
                ecb.AddComponent(root, grid); ecb.AddComponent(root, LocalTransform.Identity); ecb.AddComponent(root, new LocalToWorld { Value = float4x4.identity });
                ecb.AddComponent(root, new GridSource { StructureHandle = -1, Flags = GridSpawnFlags.PlanarInertia | GridSpawnFlags.Transient | GridSpawnFlags.NoRender });
                ecb.AddComponent(root, new GridMass { Data = mass }); ecb.AddComponent(root, new GridColliderState { Motion = GridMotion.Dynamic });
                ecb.AddComponent(root, new GridBodyInit { Motion = GridMotion.Dynamic }); ecb.AddComponent<GridMassRecompute>(root);
                ecb.AddComponent<GridSpawning>(root); ecb.SetComponentEnabled<GridSpawning>(root, false);
                ecb.AddComponent<GridRebuilt>(root); ecb.SetComponentEnabled<GridRebuilt>(root, false);
                foreach (var type in DamageSetup.GridRootComponents()) ecb.AddComponent(root, type);
                ecb.AddBuffer<BrickSlot>(root).Add(new BrickSlot { Brick = brick }); ecb.AddBuffer<BlockInstance>(root);
                var palette = ecb.AddBuffer<PaletteEntry>(root); palette.Add(default); palette.Add(new PaletteEntry { Block = block });
                var linked = ecb.AddBuffer<LinkedEntityGroup>(root); linked.Add(new LinkedEntityGroup { Value = root }); linked.Add(new LinkedEntityGroup { Value = brick });
                ecb.AddComponent(brick, new Brick { Grid = root, SolidCount = 1 });
                ecb.AddComponent(brick, new Parent { Value = root }); ecb.AddComponent(brick, LocalTransform.FromScale(grid.CellSize)); ecb.AddComponent<LocalToWorld>(brick);
                foreach (var type in DamageSetup.BrickComponents()) ecb.AddComponent(brick, type);
                ecb.AddComponent<BrickDirty>(brick); ecb.SetComponentEnabled<BrickDirty>(brick, false); // Initial build is queued by body adoption.
                ecb.AddBuffer<BrickCells>(brick); ecb.AddBuffer<BrickSparseCells>(brick).Add(new BrickSparseCells { Value = Cell.Palette(1) });
                var mask = ecb.AddBuffer<BrickSolidMask>(brick); mask.ResizeUninitialized(64); for (int i = 0; i < 64; i++) mask[i] = new BrickSolidMask { Bits = i == 0 ? 1UL : 0UL };

                var velocity = new PhysicsVelocity { Linear = new float3(6, 0, 0) };
                ecb.AddComponent(root, GridBodyFactory.DynamicBodyTypes());
                ecb.SetComponent(root, new PhysicsCollider { Value = provisional });
                ecb.SetComponent(root, GridMassMath.CreateDynamicMass(mass, grid.CellMin, grid.CellMax, grid.CellSize, true));
                ecb.SetComponent(root, velocity); ecb.SetComponent(root, GridBodyFactory.DefaultDamping(PhysicsVoxelConfig.Default));
                ecb.SetComponent(root, new PhysicsGraphicalSmoothing { ApplySmoothing = 1 });
                ecb.SetComponent(root, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = RigidTransform.identity, PreviousVelocity = velocity });
                ecb.AddSharedComponent(root, GridBodyFactory.WorldIndex());
                f.Tick(); // Tick 20: creator's ECB plays after physics.
                using var roots = em.CreateEntityQuery(typeof(VoxelGrid), typeof(PhysicsCollider)); root = roots.GetSingletonEntity();
                Assert.AreEqual(float3.zero, em.GetComponentData<LocalTransform>(root).Position);
                Assert.AreEqual(0, f.Cache.C.Grids.Length); Assert.IsTrue(em.IsComponentEnabled<GridBodyInit>(root));
                const uint installTick = 28; // RoundUp((creation tick 20 + 1) + delay 7, bucket 4).
                int retirementStarts = 0; bool wasQueued = false; float previousX = 0;
                for (int i = 0; i < 20; i++)
                {
                    uint tick = f.Singleton<SimClock>().Tick; f.Tick(); adopted = f.Cache.C.GridIndex.ContainsKey(grid.Serial);
                    Assert.IsTrue(adopted); Assert.IsFalse(em.IsComponentEnabled<GridBodyInit>(root)); Assert.IsFalse(em.IsComponentEnabled<GridMassRecompute>(root));
                    float x = em.GetComponentData<LocalTransform>(root).Position.x; Assert.Greater(x, previousX, "Body moves on tick " + tick); previousX = x;
                    var state = em.GetComponentData<GridColliderState>(root);
                    if (tick < installTick)
                    {
                        Assert.AreEqual(GridColliderPhase.Provisional, state.Phase); Assert.AreEqual(provisional, em.GetComponentData<PhysicsCollider>(root).Value);
                        if (i == 0)
                        {
                            var record = f.Cache.C.Grids[0]; Assert.AreEqual(provisional, record.Installed); Assert.AreEqual(1, record.Updates.Length);
                            var update = f.Cache.C.Updates[record.Updates[0]]; Assert.AreEqual(ColliderPriority.Split, update.Priority); Assert.AreEqual(installTick, update.InstallTick);
                        }
                    }
                    else
                    {
                        Assert.AreEqual(GridColliderPhase.Ready, state.Phase); Assert.AreEqual(installTick, state.InstalledTick);
                        Assert.AreEqual(ColliderType.Compound, em.GetComponentData<PhysicsCollider>(root).Value.Value.Type);
                    }
                    int queued = 0; foreach (var retired in f.Singleton<DeferredDisposalQueue>().Blobs) if (retired.Blob == provisional) queued++;
                    Assert.LessOrEqual(queued, 1); if (queued == 1 && !wasQueued) retirementStarts++; wasQueued = queued == 1;
                    if (tick == installTick) Assert.AreEqual(1, queued, "Old shape retires exactly when the compound installs");
                }
                Assert.AreEqual(1, retirementStarts); Assert.IsFalse(wasQueued);
                Assert.Throws<InvalidOperationException>(() => { var unused = provisional.Value.Type; });
                Assert.AreEqual(1, f.Cache.C.Stats.Value.Installs); Assert.AreEqual(0, f.Cache.C.Stats.Value.LateInstalls);
                f.Batch.State.Value.Handle.Complete(); ColliderPipelineInvariants.Check(f.Cache, f.Batch);
                em.DestroyEntity(root); f.Tick(); f.Tick(); f.Tick(); f.Tick();
                Assert.AreEqual(0, f.Cache.C.Grids.Length); Assert.AreEqual(0, f.Singleton<DeferredDisposalQueue>().Blobs.Length);
                TestContext.WriteLine("Created at tick 20; moved at 21; compound installed at 28; provisional retired once; 20 lifecycle ticks passed.");
            }
            finally { if (!adopted) { f.Test.CompleteAll(); if (f.Cache.C.Grids.Length == 0) provisional.Dispose(); } }
        }
    }
}
