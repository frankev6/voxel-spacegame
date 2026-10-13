using System;
using System.Collections.Generic;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.EditMode.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    [DisableAutoCreation] internal partial struct HitTestProbeSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnUpdate(ref SystemState state) { }
    }
    /// <summary>WPN-003: production spawned cells, current poses and analytic shields, through the query-data API.</summary>
    public sealed class CombatHitTestTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly TestGridFactory Grids = new TestGridFactory();
            public EntityManager Em => Grids.EM;
            readonly List<Entity> roots = new List<Entity>();
            readonly SystemHandle probe;
            public Fixture() { probe = Grids.World.GetOrCreateSystem<HitTestProbeSystem>(); }
            public Entity Spawn(VoxelStructure source, float3 position = default)
            {
                Entity e = Grids.Spawn(source); roots.Add(e);
                Em.SetComponentData(e, LocalTransform.FromPosition(position)); return e;
            }
            public Entity Box(float3 position = default) => Spawn(Grids.Box(0, new int3(3,3,3)), position);
            public Entity Body(float3 position, float radius)
            {
                Entity e = Em.CreateEntity(typeof(LocalTransform), typeof(SpatialBody));
                Em.SetComponentData(e, LocalTransform.FromPosition(position));
                Em.SetComponentData(e, new SpatialBody { Radius = radius }); roots.Add(e); return e;
            }
            public CombatHit Cast(float3 a, float3 b, Entity ignore = default)
            {
                var hash = SpatialHash.Create(128);
                try
                {
                    roots.Sort((x,y) => x.Index.CompareTo(y.Index));
                    var cells = new SortedDictionary<long,List<int>>();
                    foreach (Entity e in roots)
                    {
                        var pose = Em.GetComponentData<LocalTransform>(e);
                        float3 center = pose.Position; float radius; ProxyKind kind;
                        if (Em.HasComponent<SpatialBody>(e)) { radius = Em.GetComponentData<SpatialBody>(e).Radius; kind = ProxyKind.Celestial; }
                        else
                        {
                            var grid = Em.GetComponentData<VoxelGrid>(e);
                            float3 lo = grid.CellMin * grid.CellSize, hi = (grid.CellMax + 1) * grid.CellSize;
                            center = pose.TransformPoint((lo + hi) * .5f); radius = math.distance(lo,hi) * .5f; kind = ProxyKind.Ship;
                        }
                        float hullRadius = radius;
                        if (Em.HasComponent<ShieldState>(e))
                        {
                            var shield = Em.GetComponentData<ShieldState>(e);
                            radius = math.max(radius, math.distance(center, pose.TransformPoint(shield.Center)) + math.cmax(shield.Radii));
                        }
                        int index = hash.Proxies.Length;
                        hash.Proxies.Add(new SpatialProxy { Entity=e, Center=center, Radius=radius, HullRadius=hullRadius, Kind=kind });
                        if (radius > 512) { hash.Large.Add(index); continue; }
                        int2 min=(int2)math.floor((center.xz-radius)/128), max=(int2)math.floor((center.xz+radius)/128);
                        for(int x=min.x;x<=max.x;x++) for(int z=min.y;z<=max.y;z++)
                        {
                            long key=SpatialHash.CellKey(new int2(x,z));
                            if(!cells.TryGetValue(key,out var values)) { values=new List<int>();cells.Add(key,values); }
                            values.Add(index);
                        }
                    }
                    foreach(var cell in cells)
                    {
                        int start=hash.EntryIndices.Length;
                        foreach(int i in cell.Value) hash.EntryIndices.Add(i);
                        hash.Cells.Add(cell.Key,new int2(start,cell.Value.Count));
                    }
                    ref var state=ref Grids.World.Unmanaged.ResolveSystemStateRef(probe);
                    var hitTest=CombatHitTest.Create(ref state); hitTest.Update(ref state);
                    return hitTest.Segment(a,b,0,ignore,hash.AsReadOnly(),127);
                }
                finally { hash.Dispose(); }
            }
            public void Dispose() => Grids.Dispose();
        }
        [Test] public void HitsExactCell_RandomYaw()
        {
            using var f=new Fixture();
            using var source=BenchmarkStructures.Ship20k(f.Grids.Content.Index("block_hull"));
            Entity grid=f.Spawn(source); var random=new Unity.Mathematics.Random(48151);
            for(int i=0;i<100;i++)
            {
                var pose=LocalTransform.FromPositionRotation(random.NextFloat3(-100,100),quaternion.RotateY(random.NextFloat(-math.PI,math.PI)));
                f.Em.SetComponentData(grid,pose);
                float3 entry=new float3(0,2.25f,5.25f);
                var hit=f.Cast(pose.TransformPoint(entry-new float3(2,0,0)),pose.TransformPoint(entry+new float3(42,0,0)));
                Assert.AreEqual(HitKind.Hull,hit.Kind);Assert.AreEqual(grid,hit.Grid);
                Assert.Less(math.distance(pose.TransformPoint(entry),hit.WorldPoint),.01f);
            }
        }
        [Test] public void ThroughHole_NextCell()
        {
            using var f=new Fixture();var source=f.Grids.Box(0,new int3(5,0,0));
            source.SetCell(new int3(0),default);source.SetCell(new int3(1,0,0),default);
            Entity grid=f.Spawn(source);var hit=f.Cast(new float3(-1,.25f,.25f),new float3(4,.25f,.25f));
            Assert.AreEqual(grid,hit.Grid);Assert.AreEqual(HitKind.Hull,hit.Kind);Assert.That(hit.WorldPoint.x,Is.EqualTo(1).Within(.001));
            var inside=f.Cast(new float3(1.25f,.25f,.25f),new float3(4,.25f,.25f));
            Assert.AreEqual(0,inside.Fraction,"Muzzle inside occupied hull hits at zero");
        }
        [Test] public void IgnoresShooter()
        {
            using var f=new Fixture();Entity shooter=f.Box(),target=f.Box(new float3(4,0,0));
            var hit=f.Cast(new float3(-1,.25f,.25f),new float3(8,.25f,.25f),shooter);
            Assert.AreEqual(target,hit.Grid);Assert.That(hit.WorldPoint.x,Is.EqualTo(4).Within(.001));
        }
        [Test] public void ShieldBeforeHull()
        {
            using var f=new Fixture();Entity grid=f.Box();
            var shield=new ShieldState { Active=1,MaxHp=100,Hp=100,Center=new float3(1),Radii=new float3(3,2,4),BoundingRadius=4 };
            f.Em.AddComponentData(grid,shield);
            var hit=f.Cast(new float3(-5,1,1),new float3(5,1,1));
            Assert.AreEqual(HitKind.Shield,hit.Kind);Assert.That(hit.WorldPoint.x,Is.EqualTo(-2).Within(.001));
            Assert.Less(math.distance(hit.ShieldLocalPoint,new float3(-2,1,1)),.001);
            Assert.Less(math.distance(hit.WorldNormal,new float3(-1,0,0)),.001);
            shield.Active=0;f.Em.SetComponentData(grid,shield);
            Assert.AreEqual(HitKind.Hull,f.Cast(new float3(-5,1,1),new float3(5,1,1)).Kind);
        }
        [Test] public void NearestGridWins()
        {
            using var f=new Fixture();f.Box(new float3(5,0,0));Entity near=f.Box();
            var hit=f.Cast(new float3(-2,.25f,.25f),new float3(10,.25f,.25f));Assert.AreEqual(near,hit.Grid);
        }
        [Test] public void TieBreakByIndex()
        {
            using var f=new Fixture();Entity a=f.Box(),b=f.Box();
            Assert.AreEqual(a.Index<b.Index?a:b,f.Cast(new float3(-2,.25f,.25f),new float3(10,.25f,.25f)).Grid);
        }
        [Test] public void Celestial()
        {
            using var f=new Fixture();Entity body=f.Body(new float3(10,0,0),2);
            var hit=f.Cast(float3.zero,new float3(20,0,0));
            Assert.AreEqual(body,hit.Grid);Assert.AreEqual(HitKind.Celestial,hit.Kind);Assert.That(hit.Fraction,Is.EqualTo(.4f).Within(.001));
            Assert.Less(math.distance(hit.WorldNormal,new float3(-1,0,0)),.001);
            Assert.AreEqual(HitKind.None,f.Cast(new float3(0,3,0),new float3(20,3,0)).Kind);
        }
    }
}
