using System;
using System.Reflection;
using Game.Core.Components;
using Game.Tests.Common;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    public sealed class BrickColliderBuildTests : GameTestBase
    {
        EcsTestWorld test;
        EntityManager em;
        SystemHandle system;
        ColliderDirtyQueue queue;
        ColliderBuildCache cache;
        ColliderBatchQueue batch;
        Entity clockEntity, configEntity, frameEntity;
        [SetUp] public void SetUp()
        {
            test=CreateWorld(typeof(BrickColliderBuildSystem));em=test.EntityManager;test.AddGameplayActive();clockEntity=test.AddSimClock();
            queue=ColliderDirtyQueue.Create();cache=ColliderBuildCache.Create();batch=ColliderBatchQueue.Create();
            em.CreateSingleton(queue);em.CreateSingleton(cache);em.CreateSingleton(batch);
            configEntity=em.CreateSingleton(PhysicsVoxelConfig.Default);frameEntity=em.CreateSingleton(new DeferredDisposalQueue());
            system=test.World.GetExistingSystem<BrickColliderBuildSystem>();
        }
        [TearDown] public void TearDown(){test.CompleteAll();batch.Dispose();cache.Dispose();queue.Dispose();}
        void Config(int bricks,int assemblies)
        {var c=PhysicsVoxelConfig.Default;c.MaxBricksPerBatch=bricks;c.MaxAssembliesPerBatch=assemblies;em.SetComponentData(configEntity,c);}
        int Update(uint serial,int count,ColliderPriority priority=ColliderPriority.Spawn,uint install=0,int firstCoord=0,int solid=0)
        {
            cache.CompleteAccess();
            if(!cache.C.GridIndex.ContainsKey(serial))ColliderPipeline.AddRecord(ref cache.C,em.CreateEntity(),serial,.5f,GridMotion.Dynamic);
            int update=ColliderPipeline.GetOrCreateUpdate(ref cache.C,serial,install,0,priority);
            var mask=new BrickMaskBits();mask.Set(solid,true);
            for(int i=0;i<count;i++)ColliderPipeline.SetSnapshot(ref cache.C,update,new int3(firstCoord+i,0,0),mask,false);
            return update;
        }
        void Step(uint tick=0)
        {
            em.SetComponentData(clockEntity,new SimClock{Tick=tick});var frame=em.GetComponentData<DeferredDisposalQueue>(frameEntity);frame.Frame++;em.SetComponentData(frameEntity,frame);
            system.Update(test.World.Unmanaged);
        }
        void Finish()
        {
            var state=batch.State.Value;Assert.AreEqual(1,state.InFlight);state.Handle.Complete();cache.CompleteAccess();
            ColliderPipelineInvariants.Check(cache,batch);ColliderPipeline.Finalize(ref cache,ref batch);batch.State.Value=default;
            ColliderPipelineInvariants.Check(cache,batch);
        }
        [Test] public void Seal_OnlyClosedWindows()
        {
            int urgent=Update(1,1,ColliderPriority.Damage,12);int asap=Update(2,1);
            Step(5);Assert.AreEqual(UpdateState.Collecting,cache.C.Updates[urgent].State);Assert.AreEqual(UpdateState.Building,cache.C.Updates[asap].State);
            batch.State.Value.Handle.Complete();Step(5);Assert.AreEqual(UpdateState.Built,cache.C.Updates[asap].State);
            Step(6);Assert.AreEqual(UpdateState.Building,cache.C.Updates[urgent].State);Finish();
            Assert.AreEqual(UpdateState.Built,cache.C.Updates[urgent].State);
        }
        [Test] public void Budget_NonUrgentCapped()
        {
            Config(16,2);int large=Update(1,40);Step();Assert.AreEqual(16,batch.B.Bricks.Length);Assert.AreEqual(0,batch.B.Assemblies.Length);Finish();
            Step();Assert.AreEqual(16,batch.B.Bricks.Length);Finish();Step();Assert.AreEqual(8,batch.B.Bricks.Length);Assert.AreEqual(1,batch.B.Assemblies.Length);Finish();
            Assert.AreEqual(UpdateState.Built,cache.C.Updates[large].State);
            for(uint serial=2;serial<=5;serial++)Update(serial,1);Step();Assert.AreEqual(2,batch.B.Assemblies.Length);Assert.AreEqual(2,batch.B.Bricks.Length);Finish();
            Step();Assert.AreEqual(2,batch.B.Assemblies.Length);Finish();
        }
        [Test] public void Urgent_NeverDeferred()
        {
            Config(16,1);Update(1,300,ColliderPriority.Damage,8);Update(2,200,ColliderPriority.Split,8);Update(3,100);
            Step(2);Assert.AreEqual(516,batch.B.Bricks.Length);Assert.AreEqual(2,batch.B.Assemblies.Length);Assert.AreEqual(500,cache.C.Stats.Value.LastBatchUrgentBricks);
            Finish();Assert.AreEqual(500,cache.C.Stats.Value.CachedBricks-16);
        }
        [Test] public void PerGridOrder_Respected()
        {
            Config(16,2);int older=Update(1,20);ColliderPipeline.Seal(ref cache.C,older);
            int newer=Update(1,1,ColliderPriority.Damage,8,0,3);
            Step(2);Assert.AreEqual(16,batch.B.Bricks.Length);Assert.AreEqual(0,batch.B.Assemblies.Length);Assert.AreEqual(0,cache.C.Updates[newer].BricksScheduled);Finish();
            Step(3);Assert.AreEqual(5,batch.B.Bricks.Length);Assert.AreEqual(2,batch.B.Assemblies.Length);
            Assert.AreEqual(older,batch.B.Assemblies[0].Update);Assert.AreEqual(newer,batch.B.Assemblies[1].Update);
            Finish();Assert.AreEqual(UpdateState.Built,cache.C.Updates[older].State);Assert.AreEqual(UpdateState.Built,cache.C.Updates[newer].State);
            // Old compound retains cell 0; the newer overlay moves that brick's occupied cell to x=3.
            var oldBlob=cache.C.Updates[older].Compound;var newBlob=cache.C.Updates[newer].Compound;
            var ray=new RaycastInput{Start=new float3(.25f,2,.25f),End=new float3(.25f,-2,.25f),Filter=PhysicsLayers.ProjectileQuery};
            Assert.IsTrue(oldBlob.Value.CastRay(ray));Assert.IsFalse(newBlob.Value.CastRay(ray));
            ray.Start=new float3(1.75f,2,.25f);ray.End=new float3(1.75f,-2,.25f);Assert.IsTrue(newBlob.Value.CastRay(ray));
        }
        [Test] public unsafe void PartialBuild_EqualsSingleShot()
        {
            int update=Update(1,1000);int batches=0;
            while(cache.C.Updates[update].State!=UpdateState.Built){Step();batches++;Finish();Assert.LessOrEqual(batches,4);}
            Assert.AreEqual(4,batches);Assert.AreEqual(1000,cache.C.Updates[update].ChildCount);Assert.AreEqual(1,cache.C.Grids[0].Updates.Length);
            var children=cache.C.Grids[0].Children;
            var array=new NativeArray<BrickChild>(children.Length,Allocator.Temp);
            for(int i=0;i<children.Length;i++)array[i]=children[i];
            var expected=GridColliderAssembler.Assemble(array,.5f);
            try
            {
                var actual=cache.C.Updates[update].Compound;Assert.AreEqual(expected.Value.MemorySize,actual.Value.MemorySize);
                Assert.AreEqual(0,UnsafeUtility.MemCmp(expected.GetUnsafePtr(),actual.GetUnsafePtr(),expected.Value.MemorySize));
                TestContext.WriteLine($"1,000 bricks built in {batches} batches, one final assembly; {actual.Value.MemorySize} bytes equal to single-shot assembly.");
            }
            finally{expected.Dispose();array.Dispose();}
        }
        [Test] public void Jobs_HaveNoEcsAccess()
        {
            foreach(Type type in new[]{typeof(BrickColliderJob),typeof(GridAssembleJob),typeof(ColliderFinalizeJob)})
            foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {
                string name=field.FieldType.Name;
                Assert.IsFalse(name.Contains("Lookup")||name.Contains("TypeHandle")||field.FieldType==typeof(EntityManager)||field.FieldType==typeof(SystemState),type.Name+"."+field.Name);
            }
        }
    }
}
