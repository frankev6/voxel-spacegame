using System;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Voxel.Damage
{
    /// <summary>DMG-012: production split recording, next-tick detach and independent hulk ownership.</summary>
    public unsafe sealed class HulkSplitTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory F = new TestGridFactory();
            public EntityManager Em => F.EM;
            public readonly Entity Parent, Clock, AllocatorEntity;
            public readonly Entity[] Functionals = new Entity[2];
            public readonly float3[] Positions = new float3[2];
            readonly SystemHandle streamsSystem, resolve, split, bodyInit;
            readonly GameplayEndCommandBufferSystem end;
            public DamageStreams Streams;
            public Fixture()
            {
                streamsSystem = F.World.GetOrCreateSystem<DamageStreamsSystem>();
                resolve = F.World.GetOrCreateSystem<DamageResolveSystem>();
                F.World.GetOrCreateSystem<FxEventQueueCreateSystem>(); F.World.GetOrCreateSystem<SfxEventQueueCreateSystem>();
                Clock = Em.CreateSingleton(new SimClock { Tick = 10 });
                AllocatorEntity = Em.CreateSingleton(new PersistentIdAllocator { Next = 1000, End = 2000 });
                Em.CreateSingleton(PhysicsVoxelConfig.Default);
                end = F.World.GetOrCreateSystemManaged<GameplayEndCommandBufferSystem>();
                F.World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
                F.World.GetOrCreateSystem<GridColliderCleanupSystem>();
                split = F.World.GetOrCreateSystem<SplitSystem>(); bodyInit = F.World.GetOrCreateSystem<GridBodyInitSystem>();
                using var q = Em.CreateEntityQuery(typeof(DamageStreams)); Streams = q.GetSingleton<DamageStreams>();
                var source = F.Box(0, new int3(19,9,9));
                // The retained core occupies old instance zero; the two moved thrusters must remap to zero/one.
                F.Place(source, "block_core_cross", new int3(-10,0,0));
                InstallThrusters(source); Parent = F.Spawn(source);
                var pose = LocalTransform.FromPositionRotation(new float3(8,0,-4), quaternion.RotateY(.7f));
                Em.SetComponentData(Parent, pose);
                Em.AddComponentData(Parent, new PhysicsMass { Transform = new RigidTransform(quaternion.identity, new float3(-2,0,0)), InverseMass = .001f, InverseInertia = new float3(0,.01f,0) });
                Em.AddComponentData(Parent, new PhysicsVelocity { Linear = new float3(4,0,2), Angular = new float3(0,2,0) });
                Em.AddComponentData(Parent, new PlanarConstraint());
                var sourceFlags = Em.GetComponentData<GridSource>(Parent); sourceFlags.Flags |= GridSpawnFlags.PlanarInertia; Em.SetComponentData(Parent, sourceFlags);
                var instances = Em.GetBuffer<BlockInstance>(Parent);
                for (int i = 0; i < 2; i++)
                {
                    Functionals[i] = instances[i+1].Functional; Assert.AreNotEqual(Entity.Null, Functionals[i]);
                    Positions[i] = pose.TransformPoint(Em.GetComponentData<LocalTransform>(Functionals[i]).Position);
                }
                StageIsland();
            }
            public void InstallThrusters(VoxelStructure source)
            {
                ushort block = F.Content.Index("block_thruster_medium");
                ref var definition = ref F.Content.Service.BlockDefs.Value.Blocks[block];
                foreach (var origin in new[] { new int3(2,3,2), new int3(12,6,6) })
                {
                    for (int i = 0; i < definition.Cells.Length; i++) source.SetCell(origin + definition.Cells[i], default);
                    F.Place(source, "block_thruster_medium", origin);
                }
            }
            void StageIsland()
            {
                var slots = Em.GetBuffer<BrickSlot>(Parent);
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    Entity brick = slots[slot].Brick; if (brick == Entity.Null) continue;
                    var coord = Em.GetComponentData<Brick>(brick).Coord; var solid = Em.GetBuffer<BrickSolidMask>(brick); CellMask mask = default; bool any = false;
                    for (int c = 0; c < 4096; c++)
                    {
                        int3 cell = BrickMath.CellOf(coord,c);
                        if (math.all(cell >= 0) && math.all(cell <= new int3(19,9,9)) && (solid[c>>6].Bits & (1UL << (c&63))) != 0)
                        { mask.Words[c>>6] |= 1UL << (c&63); any = true; }
                    }
                    if (any) Streams.Data->IslandMasks.Add(new IslandMask { SlotIndex = slot, Mask = mask });
                }
                Streams.Data->Islands.Add(new IslandRecord { Grid = Parent, MaskCount = Streams.Data->IslandMasks.Length,
                    CellMin = 0, CellMax = new int3(19,9,9), CellCount = 2000, HasFunctional = 1 });
            }
            public Entity Split()
            {
                split.Update(F.World.Unmanaged); Complete(); end.Update();
                using var q = Em.CreateEntityQuery(typeof(Hulk),typeof(VoxelGrid));
                return q.IsEmpty ? Entity.Null : q.GetSingletonEntity();
            }
            public void DetachNextTick()
            {
                Em.SetComponentData(Clock, new SimClock { Tick = 11 }); streamsSystem.Update(F.World.Unmanaged); Complete();
                resolve.Update(F.World.Unmanaged); Complete(); end.Update();
            }
            public void Adopt()
            { var clock = Em.GetComponentData<SimClock>(Clock); clock.Tick++; Em.SetComponentData(Clock, clock); bodyInit.Update(F.World.Unmanaged); Complete(); end.Update(); }
            public void Complete() { Em.CompleteAllTrackedJobs(); Streams.Data->AllJobs(default).Complete(); }
            public int Count(Entity root)
            {
                int count = 0; foreach (var slot in Em.GetBuffer<BrickSlot>(root))
                    if (slot.Brick != Entity.Null && Em.Exists(slot.Brick)) count += Em.GetComponentData<Brick>(slot.Brick).SolidCount;
                return count;
            }
            public bool InGroup(Entity root, Entity child)
            { foreach (var e in Em.GetBuffer<LinkedEntityGroup>(root)) if (e.Value == child) return true; return false; }
            public void Dispose() { Complete(); F.Dispose(); }
        }
        [Test] public void Hulk_ExistsSameTick_CellsConserved()
        {
            using var f = new Fixture(); int original = f.Count(f.Parent); Entity hulk = f.Split();
            Assert.AreNotEqual(Entity.Null,hulk); Assert.AreEqual(2000,f.Count(hulk)); Assert.AreEqual(original,f.Count(f.Parent));
            var expectedSource = f.F.Box(0,new int3(19,9,9)); f.InstallThrusters(expectedSource); Entity expected = f.F.Spawn(expectedSource);
            var grid = f.Em.GetComponentData<VoxelGrid>(expected); grid.Kind = GridKind.Hulk; f.Em.SetComponentData(expected,grid);
            Assert.AreEqual(CellStateHasher.ComputeGridHash(f.Em,expected),CellStateHasher.ComputeGridHash(f.Em,hulk));
            f.DetachNextTick(); Assert.AreEqual(original-2000,f.Count(f.Parent)); Assert.AreEqual(original,f.Count(f.Parent)+f.Count(hulk));
            Assert.AreEqual(2000,f.Streams.Data->Removed.Length);
            foreach (var cell in f.Streams.Data->Removed) Assert.AreEqual(CellRemovedReason.DetachedBody,cell.Reason);
        }
        [Test] public void Functional_ReparentedAndInLeg()
        {
            using var f = new Fixture(); Entity hulk = f.Split();
            for (int i=0;i<2;i++)
            {
                Entity e=f.Functionals[i]; var functional=f.Em.GetComponentData<FunctionalBlock>(e);
                Assert.AreEqual(hulk,functional.Grid); Assert.AreEqual(i,functional.InstanceIndex);
                Assert.AreEqual(hulk,f.Em.GetComponentData<Parent>(e).Value);
                float3 position=f.Em.GetComponentData<LocalTransform>(hulk).TransformPoint(f.Em.GetComponentData<LocalTransform>(e).Position);
                Assert.Less(math.distance(f.Positions[i],position),1e-5f); Assert.IsTrue(f.InGroup(hulk,e)); Assert.IsFalse(f.InGroup(f.Parent,e));
            }
            f.DetachNextTick(); foreach(var e in f.Functionals) Assert.IsTrue(f.Em.Exists(e));
        }
        [Test] public void ParentDestroy_LeavesHulk()
        {
            using var f=new Fixture(); Entity hulk=f.Split(); f.DetachNextTick(); f.Em.DestroyEntity(f.Parent);
            Assert.IsTrue(f.Em.Exists(hulk)); Assert.AreEqual(2000,f.Count(hulk));
            foreach(var e in f.Functionals) { Assert.IsTrue(f.Em.Exists(e)); Assert.AreEqual(hulk,f.Em.GetComponentData<FunctionalBlock>(e).Grid); }
        }
        [Test] public void PersistentId_FromBlock_DefersWhenEmpty()
        {
            using var f=new Fixture(); f.Em.SetComponentData(f.AllocatorEntity,new PersistentIdAllocator{Next=1000,End=1000});
            Assert.AreEqual(Entity.Null,f.Split()); Assert.AreEqual(0,f.Streams.Data->PendingDetachNext.Length);
            Assert.GreaterOrEqual(f.Em.GetBuffer<IntegritySeed>(f.Parent).Length,2); Assert.AreEqual(1,f.Streams.Data->Current.SplitsDeferred);
            f.Em.SetComponentData(f.AllocatorEntity,new PersistentIdAllocator{Next=1000,End=1001}); Entity hulk=f.Split();
            Assert.AreEqual(1000ul,f.Em.GetComponentData<PersistentId>(hulk).Value);
            Assert.AreNotEqual(f.Em.GetComponentData<PersistentId>(f.Parent).Value,f.Em.GetComponentData<PersistentId>(hulk).Value);
            Assert.AreEqual(1001ul,f.Em.GetComponentData<PersistentIdAllocator>(f.AllocatorEntity).Next);
            var info=f.Em.GetComponentData<HulkInfo>(hulk); Assert.AreEqual(HulkCause.Split,info.Cause); Assert.AreEqual(10u,info.Tick);
            Assert.AreEqual(f.Em.GetComponentData<PersistentId>(f.Parent).Value,info.OriginPersistentId);
        }
        [Test] public void Velocity_IsRigidPlusSeparation()
        {
            using var f=new Fixture(); Entity hulk=f.Split(); var pose=f.Em.GetComponentData<LocalTransform>(hulk);
            var mass=f.Em.GetComponentData<PhysicsMass>(hulk); var parentMass=f.Em.GetComponentData<PhysicsMass>(f.Parent);
            var pv=f.Em.GetComponentData<PhysicsVelocity>(f.Parent); float3 com=pose.TransformPoint(mass.CenterOfMass);
            float3 direction=com-pose.TransformPoint(parentMass.CenterOfMass);direction.y=0;
            float3 expected=DamageMath.VelocityAtPoint(pv,parentMass,pose,com)+math.normalize(direction)*.75f;
            Assert.Less(math.distance(expected,f.Em.GetComponentData<PhysicsVelocity>(hulk).Linear),.01f);
            Assert.AreEqual(0,mass.InverseInertia.x);Assert.AreEqual(0,mass.InverseInertia.z);Assert.Greater(mass.InverseInertia.y,0);
        }
        [Test] public void Body_ViaGridBodyFactory()
        {
            using var f=new Fixture();Entity hulk=f.Split();
            Assert.AreEqual(0,f.Em.GetSharedComponent<PhysicsWorldIndex>(hulk).Value);
            Assert.IsTrue(f.Em.IsComponentEnabled<GridBodyInit>(hulk));Assert.IsTrue(f.Em.IsComponentEnabled<GridMassRecompute>(hulk));
            var blob=f.Em.GetComponentData<PhysicsCollider>(hulk).Value;var box=(BoxCollider*)blob.GetUnsafePtr();
            Assert.AreEqual(new float3(10,5,5)*.5f,box->Center);Assert.AreEqual(new float3(20,10,10)*.5f,box->Size);
            Assert.AreEqual(PhysicsLayers.Grid,box->GetCollisionFilter());Assert.AreEqual(CollisionResponsePolicy.None,box->Material.CollisionResponse);
            Assert.AreEqual(f.Parent,f.Em.GetComponentData<SplitOrigin>(hulk).Parent);Assert.AreEqual(70u,f.Em.GetComponentData<SplitGrace>(hulk).UntilTick);
            f.Adopt();f.Adopt();Assert.AreNotEqual(GridColliderPhase.None,f.Em.GetComponentData<GridColliderState>(hulk).Phase);
            Assert.AreEqual(blob,f.Em.GetComponentData<PhysicsCollider>(hulk).Value);
        }
        [Test] public void BrickScale_IsCellSize()
        {
            using var f=new Fixture();Entity hulk=f.Split();float size=f.Em.GetComponentData<VoxelGrid>(hulk).CellSize;
            foreach(var slot in f.Em.GetBuffer<BrickSlot>(hulk)) if(slot.Brick!=Entity.Null)
            {Assert.AreEqual(size,f.Em.GetComponentData<LocalTransform>(slot.Brick).Scale);Assert.AreEqual(hulk,f.Em.GetComponentData<Parent>(slot.Brick).Value);}
        }
    }
}
