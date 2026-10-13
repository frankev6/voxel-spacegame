using System;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Voxel.Damage
{
    /// <summary>DMG-011: production split recording, bounded pool and deferred blob lifetime.</summary>
    public unsafe sealed class ChunkBodyTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory F = new TestGridFactory();
            public EntityManager Em => F.EM;
            public readonly SystemHandle Split, Cleanup, Disposal;
            public readonly GameplayEndCommandBufferSystem End;
            public readonly Entity Clock;
            public DamageStreams Streams;
            public uint Tick = 10;
            public Fixture()
            {
                F.World.GetOrCreateSystem<DamageStreamsSystem>();
                F.World.GetOrCreateSystem<FxEventQueueCreateSystem>();
                F.World.GetOrCreateSystem<SfxEventQueueCreateSystem>();
                Clock = Em.CreateSingleton(new SimClock { Tick = Tick });
                Em.CreateSingleton(new PersistentIdAllocator { Next = 1000, End = 100000 });
                Em.CreateSingleton(PhysicsVoxelConfig.Default);
                End = F.World.GetOrCreateSystemManaged<GameplayEndCommandBufferSystem>();
                Split = F.World.GetOrCreateSystem<SplitSystem>();
                Cleanup = F.World.GetOrCreateSystem<ChunkColliderCleanupSystem>();
                Disposal = F.World.GetOrCreateSystem<DeferredDisposalSystem>();
                using var q = Em.CreateEntityQuery(typeof(DamageStreams)); Streams = q.GetSingleton<DamageStreams>();
            }
            public Entity AddIsland(int count = 100)
            {
                var source = F.Box(0, count == 100 ? new int3(9, 9, 0) : new int3(count - 1, 0, 0));
                Entity root = F.Spawn(source);
                Em.AddComponentData(root, new PhysicsMass { Transform = RigidTransform.identity, InverseMass = .01f, InverseInertia = new float3(1) });
                Em.AddComponentData(root, new PhysicsVelocity { Linear = new float3(4, 0, 2), Angular = new float3(0, 2, 0) });
                int start = Streams.Data->IslandMasks.Length;
                var slots = Em.GetBuffer<BrickSlot>(root);
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].Brick == Entity.Null) continue;
                    var solid = Em.GetBuffer<BrickSolidMask>(slots[i].Brick); CellMask mask = default;
                    for (int w = 0; w < 64; w++) mask.Words[w] = solid[w].Bits;
                    Streams.Data->IslandMasks.Add(new IslandMask { SlotIndex = i, Mask = mask });
                }
                var grid = Em.GetComponentData<VoxelGrid>(root);
                Streams.Data->Islands.Add(new IslandRecord { Grid = root, CellCount = count, CellMin = grid.CellMin,
                    CellMax = grid.CellMax, MaskStart = start, MaskCount = Streams.Data->IslandMasks.Length - start,
                    Ordinal = Streams.Data->Islands.Length });
                return root;
            }
            public void Run()
            {
                Em.SetComponentData(Clock, new SimClock { Tick = Tick });
                Split.Update(F.World.Unmanaged); Em.CompleteAllTrackedJobs(); End.Update();
                Streams.Data->Islands.Clear(); Streams.Data->IslandMasks.Clear(); Streams.Data->PendingDetachNext.Clear();
            }
            public EntityQuery Chunks(bool disabled = false) => Em.CreateEntityQuery(new EntityQueryDesc
                { All = new[] { ComponentType.ReadOnly<ChunkBody>() }, Options = disabled ? EntityQueryOptions.IncludeDisabledEntities : EntityQueryOptions.Default });
            public void Frame()
            {
                Tick++; Em.SetComponentData(Clock, new SimClock { Tick = Tick });
                Cleanup.Update(F.World.Unmanaged); Disposal.Update(F.World.Unmanaged);
            }
            public int RetiredCount { get { using var q = Em.CreateEntityQuery(typeof(DeferredDisposalQueue)); return q.GetSingleton<DeferredDisposalQueue>().Blobs.Length; } }
            public void Dispose() { Em.CompleteAllTrackedJobs(); F.Dispose(); }
        }

        [Test] public void Chunk_InheritsMomentum()
        {
            using var f = new Fixture(); var parent = f.AddIsland(); f.Run();
            using var q = f.Chunks(); Assert.AreEqual(1, q.CalculateEntityCount()); var entity = q.GetSingletonEntity();
            var body = f.Em.GetComponentData<ChunkBody>(entity); var transform = f.Em.GetComponentData<LocalTransform>(entity);
            var velocity = f.Em.GetComponentData<PhysicsVelocity>(entity); var mass = f.Em.GetComponentData<PhysicsMass>(entity);
            var pm = f.Em.GetComponentData<PhysicsMass>(parent); var pv = f.Em.GetComponentData<PhysicsVelocity>(parent);
            var pt = f.Em.GetComponentData<LocalTransform>(parent);
            float3 direction = transform.Position - pt.TransformPoint(pm.CenterOfMass); direction.y = 0;
            float3 expected = DamageMath.VelocityAtPoint(pv, pm, pt, transform.Position) + math.normalizesafe(direction, new float3(1,0,0)) * DamageConfig.Default.ChunkSeparationSpeed;
            Assert.Less(math.distance(expected, velocity.Linear), .01f);
            float cellMass = f.F.Content.Service.BlockDefs.Value.Blocks[f.F.Content.Index("block_hull")].CellMass;
            Assert.AreEqual(100 * cellMass, 1f / mass.InverseMass, .01f);
            Assert.AreEqual(100, body.CellCount); Assert.AreEqual(float3.zero, mass.CenterOfMass);
            Assert.AreEqual(f.Tick, body.SpawnTick); Assert.IsFalse(f.Em.HasComponent<Disabled>(entity));
        }

        [Test] public void GreedyBoxes_VolumeEqualsCells()
        {
            var rng = new Unity.Mathematics.Random(7123);
            using var cells = new NativeList<ChunkCell>(Allocator.Temp);
            using var boxes = new NativeList<ChunkBoxBuilder.Box>(Allocator.Temp);
            for (int sample = 0; sample < 200; sample++)
            {
                cells.Clear();
                for (int z = 0; z < 8; z++) for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
                    if (rng.NextBool()) cells.Add(new ChunkCell { X = (byte)x, Y = (byte)y, Z = (byte)z });
                ChunkBoxBuilder.Build(cells.AsArray(), 512, boxes);
                int volume = 0; using var covered = new NativeHashSet<int3>(512, Allocator.Temp);
                foreach (var box in boxes)
                {
                    volume += box.Size.x * box.Size.y * box.Size.z;
                    for (int z = 0; z < box.Size.z; z++) for (int y = 0; y < box.Size.y; y++) for (int x = 0; x < box.Size.x; x++)
                        Assert.IsTrue(covered.Add(box.Min + new int3(x,y,z)), "Boxes must not overlap");
                }
                Assert.AreEqual(cells.Length, volume);
                foreach (var cell in cells) Assert.IsTrue(covered.Contains(new int3(cell.X,cell.Y,cell.Z)));
                int exact = boxes.Length; ChunkBoxBuilder.Build(cells.AsArray(), 32, boxes);
                if (exact > 32) { Assert.AreEqual(1, boxes.Length); Assert.AreEqual(new int3(8), boxes[0].Size); }
                else Assert.AreEqual(exact, boxes.Length);
            }
        }

        [Test] public void Pool_CapsAt64_AndEvicts()
        {
            using var f = new Fixture();
            for (int tick = 0; tick < 10; tick++)
            {
                for (int i = 0; i < 8; i++) f.AddIsland(10);
                f.Run(); using var q = f.Chunks(); Assert.AreEqual(math.min(64, (tick + 1) * 8), q.CalculateEntityCount());
                f.Frame();
            }
            using var all = f.Chunks(true); Assert.AreEqual(64, all.CalculateEntityCount());
            using var fx = f.Em.CreateEntityQuery(typeof(FxEventQueue));
            var queue = fx.GetSingleton<FxEventQueue>(); queue.Access.CompleteAccess();
            Assert.Greater(queue.Queue.Count, 0, "Eviction emits debris");
            Assert.AreEqual(80, f.Streams.Data->Current.ChunksCreated);
        }

        static void CheckNoLeaks(Action exercise)
        {
            var previous = NativeLeakDetection.Mode;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try
            {
                exercise(); // Warm up shared Unity/type resources before establishing the leak baseline.
                UnsafeUtility.CheckForLeaks();
                exercise();
                Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "Chunk lifetime/unload must release owned native allocations.");
            }
            finally { NativeLeakDetection.Mode = previous; }
        }

        [Test] public void Lifetime_DisablesAndReleasesBlob()
        {
            CheckNoLeaks(() =>
            {
                using var f = new Fixture(); f.AddIsland(); f.Run();
                using var q = f.Chunks(); var entity = q.GetSingletonEntity(); var body = f.Em.GetComponentData<ChunkBody>(entity);
                f.Tick = body.DissolveStartTick + 1; f.Run(); Assert.Less(f.Em.GetComponentData<LocalTransform>(entity).Scale, 1);
                f.Tick = body.EndTick; f.Run(); Assert.IsTrue(f.Em.HasComponent<Disabled>(entity));
                Assert.AreEqual(0, f.Em.GetComponentData<ChunkBody>(entity).Live);
                Assert.IsFalse(f.Em.GetComponentData<PhysicsCollider>(entity).Value.IsCreated);
                Assert.IsFalse(f.Em.GetComponentData<ChunkColliderBlob>(entity).Value.IsCreated);
                f.Frame(); Assert.AreEqual(1, f.RetiredCount); f.Frame(); Assert.AreEqual(1, f.RetiredCount);
                f.Frame(); Assert.AreEqual(0, f.RetiredCount);
            });
        }

        [Test] public void Unload_ReleasesBlobsWithoutGameplay()
        {
            CheckNoLeaks(() =>
            {
                using var f = new Fixture(); f.AddIsland(); f.Run();
                using var chunks = f.Chunks(true); f.Em.DestroyEntity(chunks);
                using var active = f.Em.CreateEntityQuery(typeof(GameplayActive)); f.Em.DestroyEntity(active);
                f.Frame(); Assert.AreEqual(1, f.RetiredCount);
                using var owners = f.Em.CreateEntityQuery(typeof(ChunkColliderBlob)); Assert.IsTrue(owners.IsEmptyIgnoreFilter);
                for (int i = 0; i < 4; i++) f.Frame();
                Assert.AreEqual(0, f.RetiredCount);
            });
        }
    }
}
