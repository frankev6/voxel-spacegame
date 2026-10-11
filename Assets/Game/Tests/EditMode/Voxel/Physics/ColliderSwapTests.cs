using System.Diagnostics;
using System.Threading;
using Game.Core.Components;
using Game.Tests.Common;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    public sealed class ColliderSwapTests : GameTestBase
    {
        EcsTestWorld test;
        EntityManager em;
        SystemHandle system;
        Entity clock,root;
        ColliderDirtyQueue queue;
        ColliderBuildCache cache;
        ColliderBatchQueue batch;
        static BlobAssetReference<Collider> Box(float x=0)=>BoxCollider.Create(new BoxGeometry{Center=new float3(x,0,0),Size=new float3(1),Orientation=quaternion.identity},PhysicsLayers.Grid,PhysicsLayers.GridMaterial);
        [SetUp] public void SetUp()
        {
            test=CreateWorld(typeof(ColliderSwapSystem));em=test.EntityManager;test.AddGameplayActive();clock=test.AddSimClock();
            queue=ColliderDirtyQueue.Create();cache=ColliderBuildCache.Create();batch=ColliderBatchQueue.Create();em.CreateSingleton(queue);em.CreateSingleton(cache);em.CreateSingleton(batch);
            root=em.CreateEntity(typeof(PhysicsCollider),typeof(GridColliderState));var provisional=Box();em.SetComponentData(root,new PhysicsCollider{Value=provisional});
            em.SetComponentData(root,new GridColliderState{Phase=GridColliderPhase.Provisional,Motion=GridMotion.Dynamic,BoxCount=1});
            ColliderPipeline.AddRecord(ref cache.C,root,1,.5f,GridMotion.Dynamic,installed:provisional);
            system=test.World.GetExistingSystem<ColliderSwapSystem>();
        }
        [TearDown] public void TearDown(){test.CompleteAll();cache.CompleteAccess();batch.Dispose();cache.Dispose();queue.Dispose();}
        int Pending(uint install)
        {
            cache.CompleteAccess();int index=ColliderPipeline.GetOrCreateUpdate(ref cache.C,1,install,0,ColliderPriority.Damage);
            ColliderPipeline.Seal(ref cache.C,index);return index;
        }
        void Built(int index,BlobAssetReference<Collider> blob)
        {
            var update=cache.C.Updates[index];update.State=UpdateState.Built;update.Compound=blob;update.ChildCount=blob.IsCreated?1:0;update.BoxCount=update.ChildCount;cache.C.Updates[index]=update;
            var stats=cache.C.Stats.Value;stats.PendingCompoundBytes+=ColliderPipeline.Bytes(blob);cache.C.Stats.Value=stats;
        }
        void Tick(uint tick)
        {
            em.SetComponentData(clock,new SimClock{Tick=tick});system.Update(test.World.Unmanaged);cache.CompleteAccess();test.CompleteAll();ColliderPipelineInvariants.Check(cache,batch);
        }
        [Test] public void Urgent_InstallsAtInstallTick()
        {
            var original=cache.C.Grids[0].Installed;var replacement=Box(2);int index=Pending(8);Built(index,replacement);
            for(uint t=1;t<8;t++){Tick(t);Assert.AreEqual(original,em.GetComponentData<PhysicsCollider>(root).Value);}
            Tick(8);Assert.AreEqual(replacement,em.GetComponentData<PhysicsCollider>(root).Value);Assert.AreEqual(8,em.GetComponentData<GridColliderState>(root).InstalledTick);
            Assert.AreEqual(original,cache.C.Retired[0]);Assert.AreEqual(0,cache.C.Grids[0].Updates.Length);
        }
        [Test] public void NewerUpdate_Wins()
        {
            var original=cache.C.Grids[0].Installed;var older=Box(1);var newer=Box(2);Built(Pending(8),older);Built(Pending(12),newer);
            Tick(12);Assert.AreEqual(newer,em.GetComponentData<PhysicsCollider>(root).Value);Assert.AreEqual(newer,cache.C.Grids[0].Installed);
            Assert.AreEqual(1,cache.C.Stats.Value.Installs);Assert.AreEqual(2,cache.C.Retired.Length);
            Assert.IsTrue(cache.C.Retired.Contains(original));Assert.IsTrue(cache.C.Retired.Contains(older));Assert.AreEqual(2,cache.C.FreeUpdates.Length);
        }
        [Test] public void Late_CountedOnce()
        {
            int update=Pending(8);Tick(7);Assert.AreEqual(0,cache.C.Stats.Value.LateInstalls);
            Tick(8);Tick(9);Tick(10);Assert.AreEqual(1,cache.C.Stats.Value.LateInstalls);Assert.AreEqual(1,cache.C.Updates[update].LateCounted);
            var replacement=Box(2);Built(update,replacement);Tick(11);Assert.AreEqual(replacement,cache.C.Grids[0].Installed);Assert.AreEqual(11,em.GetComponentData<GridColliderState>(root).InstalledTick);
            Tick(12);Assert.AreEqual(1,cache.C.Stats.Value.LateInstalls);
        }
        struct SlowJob:IJob { public void Execute()=>Thread.Sleep(500); }
        [Test] public void Swap_DoesNotBlockOnRunningBatch()
        {
            Tick(0); // Warm up Burst dispatch before timing the deliberately unfinished handle.
            var handle=new SlowJob().Schedule();batch.State.Value=new ColliderBatchState{Handle=handle,InFlight=1};JobHandle.ScheduleBatchedJobs();
            try
            {
                var watch=Stopwatch.StartNew();system.Update(test.World.Unmanaged);watch.Stop();
                Assert.IsFalse(handle.IsCompleted,"Fixed-step update must return while the batch still runs");Assert.Less(watch.Elapsed.TotalMilliseconds,250);
                Assert.AreEqual(1,batch.State.Value.InFlight);
                TestContext.WriteLine($"Swap returned in {watch.Elapsed.TotalMilliseconds:F4} ms while the 500 ms batch was still running.");
            }
            finally{handle.Complete();cache.CompleteAccess();batch.State.Value=default;}
        }
        [Test] public void State_ReadyAfterInstall()
        {
            // Supply a completed detached assembly: Swap must finalize asynchronously before its install job.
            int update=Pending(0);var u=cache.C.Updates[update];u.State=UpdateState.Building;cache.C.Updates[update]=u;var replacement=Box(2);
            batch.B.Assemblies.Add(new AssemblyItem{Update=update,Serial=1});batch.B.AssemblyResults.Add(new AssemblyResult{Compound=replacement,ChildCount=1,BoxCount=1});
            batch.State.Value=new ColliderBatchState{InFlight=1};Tick(3);
            var state=em.GetComponentData<GridColliderState>(root);Assert.AreEqual(GridColliderPhase.Ready,state.Phase);Assert.AreEqual(3,state.InstalledTick);
            Assert.AreEqual(1,state.ChildCount);Assert.AreEqual(1,state.BoxCount);Assert.AreEqual(0,batch.State.Value.InFlight);Assert.AreEqual(0,batch.B.Assemblies.Length);
            var superseded=Box(4);Built(Pending(4),superseded);Built(Pending(8),default);Tick(8);
            state=em.GetComponentData<GridColliderState>(root);Assert.AreEqual(replacement,em.GetComponentData<PhysicsCollider>(root).Value);
            Assert.AreEqual(0,state.ChildCount);Assert.AreEqual(1,state.BoxCount);Assert.AreEqual(3,state.InstalledTick);Assert.AreEqual(GridColliderPhase.Ready,state.Phase);
            Assert.IsTrue(cache.C.Retired.Contains(superseded));Assert.AreEqual(0,cache.C.Stats.Value.PendingUpdates);Assert.AreEqual(0,cache.C.Stats.Value.PendingCompoundBytes);
        }
        [Test] public void RootDestroyed_PrefixDiscarded()
        {
            var original=cache.C.Grids[0].Installed;var a=Box(1);var b=Box(2);Built(Pending(8),a);Built(Pending(12),b);em.DestroyEntity(root);Tick(12);
            Assert.AreEqual(0,cache.C.Grids[0].Updates.Length);Assert.AreEqual(0,cache.C.Stats.Value.Installs);Assert.AreEqual(original,cache.C.Grids[0].Installed);
            Assert.AreEqual(2,cache.C.Retired.Length);Assert.IsTrue(cache.C.Retired.Contains(a));Assert.IsTrue(cache.C.Retired.Contains(b));
            Assert.AreEqual(0,cache.C.Stats.Value.PendingUpdates);
        }
    }
}
