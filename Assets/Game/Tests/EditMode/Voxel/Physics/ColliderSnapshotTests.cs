using Game.Core.Components;
using Game.Core.Systems;
using Game.Tests.Common;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    public sealed partial class ColliderSnapshotTests : GameTestBase
    {
        EcsTestWorld test;
        EntityManager em;
        SystemHandle collect;
        ColliderDirtyQueue queue;
        ColliderBuildCache cache;
        ColliderBatchQueue batch;
        BrickMeshQueue mesh;
        Entity root;
        [SetUp] public void SetUp()
        {
            test = CreateWorld(typeof(ColliderSnapshotSystem), typeof(BrickDirtyScheduleSystem), typeof(MaskWriterSystem)); em = test.EntityManager;
            test.AddGameplayActive(); test.AddSimClock(); collect = test.World.GetExistingSystem<ColliderSnapshotSystem>();
            queue = ColliderDirtyQueue.Create(); cache = ColliderBuildCache.Create(); batch = ColliderBatchQueue.Create(); mesh = BrickMeshQueue.Create();
            em.CreateSingleton(queue); em.CreateSingleton(cache); em.CreateSingleton(batch); em.CreateSingleton(mesh); em.CreateSingleton(PhysicsVoxelConfig.Default);
            root = em.CreateEntity(typeof(VoxelGrid), typeof(BrickSlot));
            em.SetComponentData(root, new VoxelGrid { Serial = 7, CellSize = .5f, BrickDims = new int3(8,1,1) });
            ColliderPipeline.AddRecord(ref cache.C, root, 7, .5f, GridMotion.Dynamic);
        }
        [TearDown] public void TearDown()
        { test.CompleteAll(); queue.CompleteAccess(); cache.CompleteAccess(); mesh.CompleteAccess(); batch.Dispose(); cache.Dispose(); queue.Dispose(); mesh.Dispose(); }
        Entity Brick(int x, int solid = 0)
        {
            Entity e = em.CreateEntity(typeof(Brick), typeof(BrickSolidMask), typeof(BrickDirty));
            em.SetComponentData(e, new Brick { Grid = root, Coord = new int3(x,0,0), SolidCount = 1 });
            em.SetComponentEnabled<BrickDirty>(e, false);var mask = new BrickMaskBits();mask.Set(solid,true);mask.CopyTo(em.GetBuffer<BrickSolidMask>(e));
            return e;
        }
        void Enqueue(Entity brick,int x,ColliderPriority priority=ColliderPriority.Damage,uint serial=7)
        { queue.CompleteAccess(); queue.Enqueue(serial,root,brick,new int3(x,0,0),priority); }
        void Tick(uint tick)
        {
            using var clock = em.CreateEntityQuery(typeof(SimClock)); em.SetComponentData(clock.GetSingletonEntity(),new SimClock { Tick=tick });
            collect.Update(test.World.Unmanaged);queue.CompleteAccess();cache.CompleteAccess();test.CompleteAll();
            Assert.AreEqual(0,queue.Requests.Length);ColliderPipelineInvariants.Check(cache,batch);
        }
        [Test] public void InstallTick_Table()
        {
            uint[] expected={8,8,12,12,12,12,16,16,16,16,20,20,20,20,24,24,24,24,28,28,28};
            for(uint tick=0;tick<=20;tick++)
            {
                Assert.AreEqual(expected[tick],ColliderInstallSchedule.InstallTick(ColliderPriority.Damage,tick,7,4));
                Assert.AreEqual(expected[tick],ColliderInstallSchedule.InstallTick(ColliderPriority.Split,tick,7,4));
                Assert.AreEqual(0,ColliderInstallSchedule.InstallTick(ColliderPriority.Edit,tick,7,4));
                Assert.AreEqual(0,ColliderInstallSchedule.InstallTick(ColliderPriority.Spawn,tick,7,4));
                if(tick>0)Assert.GreaterOrEqual(expected[tick],expected[tick-1]);
                Assert.IsFalse(ColliderInstallSchedule.IsWindowClosed(expected[tick],expected[tick]-7,7));
                Assert.IsTrue(ColliderInstallSchedule.IsWindowClosed(expected[tick],expected[tick]-6,7));
            }
            Assert.IsTrue(ColliderInstallSchedule.IsWindowClosed(0,0,7));
        }
        [Test] public void SameBucket_OneUpdate()
        {
            var a=Brick(0);var b=Brick(1);Enqueue(a,0,ColliderPriority.Split);Tick(2);Enqueue(b,1);Tick(5);
            Assert.AreEqual(1,cache.C.Updates.Length);var update=cache.C.Updates[0];Assert.AreEqual(12,update.InstallTick);
            Assert.AreEqual(2,update.RequestTick);Assert.AreEqual(ColliderPriority.Damage,update.Priority);Assert.AreEqual(2,update.Bricks.Length);
            Assert.AreEqual(1,cache.C.Grids[0].Updates.Length);Enqueue(a,0);Tick(6);Assert.AreEqual(2,cache.C.Updates.Length);
            Assert.AreEqual(16,cache.C.Updates[1].InstallTick);Assert.AreEqual(1,cache.C.Updates[1].Seq);
        }
        [Test] public void DuplicateCoord_NewestMaskWins()
        {
            var e=Brick(0);Enqueue(e,0);Tick(2);int originalSlot=cache.C.Updates[0].Bricks[0].MaskSlot;
            var mask=new BrickMaskBits();mask.Set(4095,true);mask.CopyTo(em.GetBuffer<BrickSolidMask>(e));Enqueue(e,0);Tick(3);
            var update=cache.C.Updates[0];Assert.AreEqual(1,update.Bricks.Length);Assert.AreEqual(originalSlot,update.Bricks[0].MaskSlot);
            var snapshot=cache.C.Masks[originalSlot];Assert.IsFalse(snapshot.Get(0));Assert.IsTrue(snapshot.Get(4095));Assert.AreEqual(1,cache.C.Masks.Length);
            default(BrickMaskBits).CopyTo(em.GetBuffer<BrickSolidMask>(e));Enqueue(e,0);Tick(4);
            Assert.AreEqual(-1,cache.C.Updates[0].Bricks[0].MaskSlot);Assert.AreEqual(1,cache.C.FreeMasks.Length);
        }
        [Test] public void DestroyedBrick_Removed()
        {
            var dead=Brick(0);Enqueue(dead,0);em.DestroyEntity(dead);
            var empty=Brick(1);default(BrickMaskBits).CopyTo(em.GetBuffer<BrickSolidMask>(empty));Enqueue(empty,1);
            var wrong=Brick(2);em.SetComponentData(wrong,new Brick{Grid=Entity.Null});Enqueue(wrong,2);
            var shortMask=Brick(3);em.GetBuffer<BrickSolidMask>(shortMask).ResizeUninitialized(1);Enqueue(shortMask,3);Tick(1);
            var update=cache.C.Updates[0];Assert.AreEqual(4,update.Bricks.Length);for(int i=0;i<4;i++)Assert.AreEqual(-1,update.Bricks[i].MaskSlot);
            Assert.AreEqual(0,cache.C.Masks.Length);
        }
        [Test] public void UnknownSerial_Dropped()
        {
            var e=Brick(0);Enqueue(e,0,serial:99);Enqueue(e,0,serial:100);Tick(1);
            Assert.AreEqual(2,cache.C.Stats.Value.DroppedRequests);Assert.AreEqual(0,cache.C.Updates.Length);Assert.AreEqual(0,cache.C.Masks.Length);
        }
        public struct MaskWriteRequest:IComponentData { public Entity Brick; }
        [DisableAutoCreation, BurstCompile]
        [UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
        [UpdateBefore(typeof(BrickDirtyScheduleSystem))]
        public partial struct MaskWriterSystem:ISystem
        {
            BufferLookup<BrickSolidMask> masks;
            public void OnCreate(ref SystemState state){masks=state.GetBufferLookup<BrickSolidMask>();state.RequireForUpdate<MaskWriteRequest>();}
            [BurstCompile] public void OnUpdate(ref SystemState state)
            {
                masks.Update(ref state);
                state.Dependency=new WriteMaskJob{Masks=masks,Brick=SystemAPI.GetSingleton<MaskWriteRequest>().Brick}.Schedule(state.Dependency);
            }
        }
        [BurstCompile(CompileSynchronously=true)] struct WriteMaskJob:IJob
        {
            public BufferLookup<BrickSolidMask> Masks;
            public Entity Brick;
            public void Execute(){var mask=new BrickMaskBits();mask.Set(17,true);mask.CopyTo(Masks[Brick]);}
        }
        [Test] public void MaskTakenAtRequestTick()
        {
            var e=Brick(0);em.SetComponentData(e,new BrickDirty{Flags=BrickDirtyFlags.Collider});em.SetComponentEnabled<BrickDirty>(e,true);
            var slots=em.GetBuffer<BrickSlot>(root);slots.ResizeUninitialized(8);for(int i=0;i<8;i++)slots[i]=default;slots[0]=new BrickSlot{Brick=e};
            // The preceding damage writer is unfinished when collection is scheduled: its ECS dependency must be honoured.
            em.CreateSingleton(new MaskWriteRequest{Brick=e});
            using(var clock=em.CreateEntityQuery(typeof(SimClock)))em.SetComponentData(clock.GetSingletonEntity(),new SimClock{Tick=3});
            test.StepTicks(1);queue.CompleteAccess();cache.CompleteAccess();ColliderPipelineInvariants.Check(cache,batch);
            Assert.AreEqual(1,cache.C.Updates.Length);var update=cache.C.Updates[0];Assert.AreEqual(3,update.RequestTick);Assert.AreEqual(12,update.InstallTick);
            var snapshot=cache.C.Masks[update.Bricks[0].MaskSlot];Assert.IsTrue(snapshot.Get(17));Assert.IsFalse(snapshot.Get(0));
            default(BrickMaskBits).CopyTo(em.GetBuffer<BrickSolidMask>(e));
            Assert.IsTrue(cache.C.Masks[update.Bricks[0].MaskSlot].Get(17),"Snapshot is detached from later live changes");
        }
    }
}
