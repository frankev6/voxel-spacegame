using System;
using Game.Core.Components;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Spatial
{
    public sealed class SpatialHashBuildTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly World World = new World("Spatial build");
            public EntityManager Em => World.EntityManager;
            readonly SystemHandle system;
            readonly Entity clock;
            public Fixture()
            {
                Em.CreateEntity(typeof(GameplayActive));
                clock = Em.CreateEntity(typeof(SimClock));
                system = World.GetOrCreateSystem<SpatialHashBuildSystem>();
            }
            public SpatialHash Hash
            { get { using var q = Em.CreateEntityQuery(typeof(SpatialHash)); return q.GetSingleton<SpatialHash>(); } }
            public void Tick(uint tick)
            {
                Em.SetComponentData(clock, new SimClock { Tick = tick });
                ref var state = ref World.Unmanaged.ResolveSystemStateRef(system);
                ref var builder = ref World.Unmanaged.GetUnsafeSystemRef<SpatialHashBuildSystem>(system);
                builder.OnUpdate(ref state);
            }
            public Entity Grid(float3 position, GridKind kind = GridKind.Ship)
            {
                var e = Em.CreateEntity(typeof(VoxelGrid), typeof(LocalTransform));
                Em.SetComponentData(e, new VoxelGrid { CellSize = .5f, CellMin = new int3(-2,-1,-3), CellMax = new int3(3,2,4), Kind = kind });
                Em.SetComponentData(e, LocalTransform.FromPositionRotation(position, quaternion.RotateY(.7f)));
                return e;
            }
            public Entity Body(float3 position, float radius)
            {
                var e = Em.CreateEntity(typeof(SpatialBody), typeof(LocalTransform));
                Em.SetComponentData(e, new SpatialBody { Radius = radius });
                Em.SetComponentData(e, LocalTransform.FromPosition(position)); return e;
            }
            public void Dispose() => World.Dispose();
        }
        [BurstCompile(CompileSynchronously = true)]
        struct ReadJob : IJob
        {
            [ReadOnly] public SpatialHashQueryData Hash;
            public NativeArray<uint> Output;
            public int Index;
            public void Execute()
            {
                Output[Index] = Hash.State.Value.BuiltTick;
                Output[Index + 1] = (uint)Hash.Proxies.Length;
            }
        }
        [Test] public void ProxyValues()
        {
            using var f = new Fixture(); var e = f.Grid(new float3(20,3,-10), GridKind.Station);
            f.Em.AddComponentData(e, new PhysicsVelocity { Linear = new float3(2,0,3) });
            f.Em.AddComponentData(e, Allegiance.Player); f.Em.AddComponent<PlayerShip>(e);
            f.Em.AddComponent<AiBrain>(e); f.Em.AddComponent<Dormant>(e);
            f.Em.AddComponentData(e, new ShieldState { Active = 1, MaxHp = 20, Center = new float3(2,0,0), BoundingRadius = 8 });
            var hulk = f.Grid(new float3(50,0,0)); f.Em.AddComponent<Hulk>(hulk);
            var asteroid = f.Grid(new float3(-50,0,0), GridKind.Asteroid);
            var body = f.Body(new float3(0,0,1000), 900);
            f.Tick(17); var hash = f.Hash; hash.CompleteAccess();
            var proxy = hash.Get(hash.FindIndex(e));
            var localCenter = new float3(.5f,.5f,.5f);
            var pose = f.Em.GetComponentData<LocalTransform>(e);
            Assert.Less(math.distance(proxy.Center,pose.TransformPoint(localCenter)),1e-5f);
            Assert.That(proxy.HullRadius,Is.EqualTo(math.length(new float3(3,2,4))*.5f).Within(1e-5));
            Assert.That(proxy.Radius,Is.EqualTo(8+math.distance(new float3(2,0,0),localCenter)).Within(1e-5));
            Assert.AreEqual(ProxyFlags.PlayerShip|ProxyFlags.HasAi|ProxyFlags.Dormant|ProxyFlags.Shielded,proxy.Flags);
            Assert.AreEqual(new float3(2,0,3),proxy.Velocity); Assert.AreEqual(Allegiance.Player,proxy.Allegiance);
            Assert.AreEqual(ProxyKind.Station,proxy.Kind); Assert.AreEqual(ProxyKind.Hulk,hash.Get(hash.FindIndex(hulk)).Kind);
            Assert.AreEqual(ProxyKind.Asteroid,hash.Get(hash.FindIndex(asteroid)).Kind);
            var celestial=hash.Get(hash.FindIndex(body)); Assert.AreEqual(ProxyKind.Celestial,celestial.Kind);
            Assert.AreEqual(900,celestial.HullRadius); Assert.AreEqual(Allegiance.None,celestial.Allegiance);
            Assert.AreEqual(1,hash.Large.Length); Assert.AreEqual(17,hash.State.Value.BuiltTick);
            f.Em.SetComponentEnabled<Dormant>(e,false);
            f.Em.SetComponentData(e,new ShieldState { MaxHp=20,Center=new float3(2,0,0),BoundingRadius=8 });
            f.Tick(18);hash.CompleteAccess();proxy=hash.Get(hash.FindIndex(e));
            Assert.AreEqual(ProxyFlags.PlayerShip|ProxyFlags.HasAi,proxy.Flags);
            Assert.Greater(proxy.Radius,proxy.HullRadius,"Inactive rechargeable shield keeps conservative reach");
        }
        [Test] public void SortedByIndex()
        {
            using var f=new Fixture();
            for(int i=0;i<200;i++) { if(i%2==0) f.Grid(new float3(i,0,0)); else f.Body(new float3(i,0,0),1); }
            f.Tick(1);var hash=f.Hash;hash.CompleteAccess();Assert.AreEqual(200,hash.Proxies.Length);
            for(int i=1;i<hash.Proxies.Length;i++) Assert.Less(hash.Proxies[i-1].Entity.Index,hash.Proxies[i].Entity.Index);
            foreach(int index in hash.EntryIndices) Assert.That(index,Is.InRange(0,199));
        }
        [Test] public void ReaderOrdering_NoSafetyErrors()
        {
            var prior=NativeLeakDetection.Mode;bool safety=BurstCompiler.Options.EnableBurstSafetyChecks;
            NativeLeakDetection.Mode=NativeLeakDetectionMode.EnabledWithStackTrace;BurstCompiler.Options.EnableBurstSafetyChecks=true;
            try
            {
                using var f=new Fixture();f.Body(float3.zero,1);
                using var output=new NativeArray<uint>(4,Allocator.TempJob);
                f.Tick(10);var hash=f.Hash;
                var first=new ReadJob { Hash=hash.AsReadOnly(),Output=output,Index=0 }.Schedule(hash.BeginRead(default));
                hash.EndRead(first);
                f.Tick(11);
                var second=new ReadJob { Hash=hash.AsReadOnly(),Output=output,Index=2 }.Schedule(hash.BeginRead(default));
                hash.EndRead(second);hash.CompleteAccess();
                CollectionAssert.AreEqual(new uint[]{10,1,11,1},output.ToArray());
            }
            finally { NativeLeakDetection.Mode=prior;BurstCompiler.Options.EnableBurstSafetyChecks=safety; }
        }
        [Test] public void WorkerCountIndependent()
        {
            int prior=JobsUtility.JobWorkerCount;
            try
            {
                using var f=new Fixture();for(int i=0;i<600;i++)f.Grid(new float3(i*17%1400,0,i*31%1700));
                f.Body(new float3(20,0,30),900);
                SpatialProxy[] proxies=null;int[] entries=null,large=null;
                foreach(int workers in new[]{1,JobsUtility.JobWorkerMaximumCount})
                {
                    JobsUtility.JobWorkerCount=workers;f.Tick(33);var hash=f.Hash;hash.CompleteAccess();
                    if(proxies==null){proxies=hash.Proxies.AsArray().ToArray();entries=hash.EntryIndices.AsArray().ToArray();large=hash.Large.AsArray().ToArray();}
                    else {CollectionAssert.AreEqual(proxies,hash.Proxies.AsArray().ToArray());CollectionAssert.AreEqual(entries,hash.EntryIndices.AsArray().ToArray());CollectionAssert.AreEqual(large,hash.Large.AsArray().ToArray());}
                }
            }
            finally {JobsUtility.JobWorkerCount=prior;}
        }
        [Test] public void SkipsSpawning()
        {
            using var f=new Fixture();var ready=f.Grid(float3.zero);var pending=f.Grid(new float3(10,0,0));
            f.Em.AddComponent<GridSpawning>(ready);f.Em.SetComponentEnabled<GridSpawning>(ready,false);
            f.Em.AddComponent<GridSpawning>(pending);f.Tick(1);var hash=f.Hash;hash.CompleteAccess();
            Assert.AreEqual(1,hash.Proxies.Length);Assert.AreEqual(ready,hash.Proxies[0].Entity);
            f.Em.SetComponentEnabled<GridSpawning>(pending,false);f.Tick(2);hash.CompleteAccess();Assert.AreEqual(2,hash.Proxies.Length);
        }
    }
}
