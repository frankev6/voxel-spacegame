using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    public sealed class ColliderDebugTests
    {
        [Test] public void Provisional_OneBox()
        {
            using var world = new World("ProvisionalDebug"); var em = world.EntityManager;
            using var output = new NativeList<ColliderDebugBox>(Allocator.Temp); output.Add(new ColliderDebugBox { Min = new float3(99), Max = new float3(100) });
            Entity root = em.CreateEntity(typeof(PhysicsCollider), typeof(GridColliderState));
            var blob = GridBodyFactory.CreateProvisionalCollider(new int3(-2, 0, 3), new int3(4, 5, 6), .5f, false);
            try
            {
                em.SetComponentData(root, new PhysicsCollider { Value = blob }); em.SetComponentData(root, new GridColliderState { Phase = GridColliderPhase.Provisional });
                Assert.AreEqual(1, ColliderDebug.CollectBoxes(em, root, output)); Assert.AreEqual(2, output.Length);
                Assert.AreEqual(new float3(99), output[0].Min, "Collector appends without clearing caller entries");
                Assert.AreEqual(new float3(-1, 0, 1.5f), output[1].Min); Assert.AreEqual(new float3(2.5f, 3, 3.5f), output[1].Max); Assert.AreEqual(1, output[1].Provisional);
                em.DestroyEntity(root); Assert.AreEqual(0, ColliderDebug.CollectBoxes(em, root, output));
            }
            finally { blob.Dispose(); }
        }

        [Test] public void Ready_BoxesCoverCells()
        {
            using var world = new World("ReadyDebug"); var em = world.EntityManager;
            var masks = new[] { new BrickMaskBits(), BrickMaskBits.Full() }; masks[0].Set(0, true); masks[0].Set(7 + 16 * 7 + 256 * 7, true); masks[0].Set(4095, true);
            var coords = new[] { new int3(-1, 0, -2), new int3(2, 1, 0) };
            var children = new NativeArray<BrickChild>(2, Allocator.Temp); var scratch = new NativeList<BrickBox>(Allocator.Temp);
            var instances = new NativeArray<CompoundCollider.ColliderBlobInstance>(1, Allocator.Temp);
            BlobAssetReference<Collider> grid = default, outer = default;
            var pose = new RigidTransform(quaternion.EulerXYZ(.2f, .35f, -.1f), new float3(-3, 2, 1));
            using var output = new NativeList<ColliderDebugBox>(Allocator.Temp);
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var blob = BrickColliderBuilder.Build(masks[i], .5f, 64, .05f, PhysicsLayers.Grid, PhysicsLayers.GridMaterial, ref scratch, out int boxes);
                    children[i] = new BrickChild { Blob = blob, BoxCount = boxes, Coord = coords[i] };
                }
                grid = GridColliderAssembler.Assemble(children, .5f);
                instances[0] = new CompoundCollider.ColliderBlobInstance { Collider = grid, CompoundFromChild = pose }; outer = CompoundCollider.Create(instances);
                Entity root = em.CreateEntity(typeof(PhysicsCollider), typeof(GridColliderState));
                em.SetComponentData(root, new PhysicsCollider { Value = outer }); em.SetComponentData(root, new GridColliderState { Phase = GridColliderPhase.Ready });
                Assert.AreEqual(4, ColliderDebug.CollectBoxes(em, root, output));
                foreach (var box in output) Assert.AreEqual(0, box.Provisional);
                for (int b = 0; b < 2; b++) for (int local = 0; local < 4096; local++)
                {
                    if (!masks[b].Get(local)) continue; float3 min = (float3)BrickMath.CellOf(coords[b], local) * .5f;
                    bool covered = false;
                    foreach (var box in output)
                    {
                        bool contains = true;
                        for (int c = 0; c < 8; c++)
                        {
                            var corner = math.transform(pose, min + new float3(c & 1, (c >> 1) & 1, (c >> 2) & 1) * .5f);
                            contains &= math.all(corner >= box.Min - 1e-5f) && math.all(corner <= box.Max + 1e-5f);
                        }
                        covered |= contains;
                    }
                    Assert.IsTrue(covered, $"Brick {b}, cell {local}");
                }
            }
            finally
            {
                if (outer.IsCreated) outer.Dispose(); if (grid.IsCreated) grid.Dispose();
                for (int i = 0; i < children.Length; i++) { var blob = children[i].Blob; if (blob.IsCreated) blob.Dispose(); }
                children.Dispose(); instances.Dispose(); scratch.Dispose();
            }
        }

        [BurstCompile] struct CountersJob : IJob
        {
            public NativeReference<ColliderPipelineStats> Stats;
            public void Execute()
            {
                var s = Stats.Value; s.Installs = 7; s.LateInstalls = 2; s.DroppedRequests = 3; s.Batches = 4; s.ForcedBatchCompletes = 5;
                s.LastBatchBricks = 8; s.LastBatchAssemblies = 2; s.LastBatchUrgentBricks = 3; Stats.Value = s;
            }
        }
        [Test] public void Stats_BytesMatchBlobs()
        {
            using var world = new World("ColliderStats"); var em = world.EntityManager;
            var cache = ColliderBuildCache.Create(); var batch = ColliderBatchQueue.Create();
            BlobAssetReference<Collider> Box() => GridBodyFactory.CreateProvisionalCollider(0, 0, .5f, false);
            try
            {
                em.CreateSingleton(cache); int index = ColliderPipeline.AddRecord(ref cache.C, em.CreateEntity(), 1, .5f, GridMotion.Dynamic, installed: Box());
                var record = cache.C.Grids[index]; record.Children.Add(new BrickChild { Blob = Box(), BoxCount = 1 }); cache.C.Grids[index] = record;
                int update = ColliderPipeline.GetOrCreateUpdate(ref cache.C, 1, 8, 1, ColliderPriority.Damage); ColliderPipeline.Seal(ref cache.C, update);
                var u = cache.C.Updates[update]; u.State = UpdateState.Built; u.Compound = Box(); u.ChildCount = u.BoxCount = 1; cache.C.Updates[update] = u;
                var stats = cache.C.Stats.Value; stats.CachedBricks = stats.CachedBoxes = 1; stats.CachedBrickBytes = ColliderPipeline.Bytes(record.Children[0].Blob);
                stats.PendingCompoundBytes = ColliderPipeline.Bytes(u.Compound); cache.C.Stats.Value = stats; ColliderPipeline.Retire(ref cache.C, Box());
                var handle = new CountersJob { Stats = cache.C.Stats }.Schedule(cache.Acquire(default)); cache.Release(handle);
                var gathered = GridColliderStats.Gather(em); Assert.AreEqual(7, gathered.Installs); ColliderPipelineInvariants.Check(cache, batch);
                long installed = 0, cached = 0, pending = 0, retired = 0;
                foreach (var grid in cache.C.Grids)
                {
                    installed += grid.Installed.Value.MemorySize;
                    for (int i = 0; i < grid.Children.Length; i++) cached += grid.Children[i].Blob.Value.MemorySize;
                }
                foreach (var item in cache.C.Updates) if (item.Compound.IsCreated) pending += item.Compound.Value.MemorySize;
                foreach (var blob in cache.C.Retired) retired += blob.Value.MemorySize;
                Assert.AreEqual(installed, gathered.InstalledBytes); Assert.AreEqual(cached, gathered.CachedBrickBytes);
                Assert.AreEqual(pending, gathered.PendingCompoundBytes); Assert.AreEqual(retired, gathered.RetiredPendingBytes);
                GridColliderStats.ResetCounters(em); var reset = GridColliderStats.Gather(em);
                gathered.Installs = gathered.LateInstalls = gathered.DroppedRequests = gathered.Batches = gathered.ForcedBatchCompletes = 0;
                Assert.AreEqual(gathered, reset, "Reset preserves ownership gauges and the last batch snapshot");
            }
            finally { batch.Dispose(); cache.Dispose(); }
        }
    }
}
