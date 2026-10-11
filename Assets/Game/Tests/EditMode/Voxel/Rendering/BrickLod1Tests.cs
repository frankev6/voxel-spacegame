using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    public sealed class BrickLod1Tests
    {
        ContentService content;
        BlobAssetReference<BrickShapeTables> shapes;
        NativeArray<uint> snapshot;
        NativeList<PackedBrickVertex> vertices;
        NativeList<uint> indices;
        [SetUp] public void SetUp()
        {
            content=new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
            shapes=BrickShapeTablesBuilder.Build(ref content.Orientations.Value);
            snapshot=new NativeArray<uint>(5832,Allocator.TempJob);
            vertices=new NativeList<PackedBrickVertex>(100000,Allocator.TempJob);indices=new NativeList<uint>(200000,Allocator.TempJob);
        }
        [TearDown] public void TearDown(){indices.Dispose();vertices.Dispose();snapshot.Dispose();shapes.Dispose();content.Dispose();}
        [BurstCompile(CompileSynchronously=true)] struct MeshJob:IJob
        {
            [ReadOnly] public NativeArray<uint> Snapshot;
            [ReadOnly] public BlobAssetReference<BrickShapeTables> Shapes;
            public NativeList<PackedBrickVertex> Vertices;
            public NativeList<uint> Indices;
            public bool Lod0;
            public void Execute()
            {
                if(Lod0)BrickMesher.MeshLod0(Snapshot,ref Shapes.Value,Vertices,Indices,out _,out _);
                else BrickMesher.MeshLod1(Snapshot,ref Shapes.Value,Vertices,Indices,out _,out _);
            }
        }
        void Mesh(bool lod0=false)=>new MeshJob{Snapshot=snapshot,Shapes=shapes,Vertices=vertices,Indices=indices,Lod0=lod0}.Run();
        static uint Cube(int key=0)=>MeshCell.Encode(1,0,0,key>>8,key&255,0,false);
        uint At(int3 p)=>snapshot[MeshCell.SnapIndex(p.x,p.y,p.z)];
        static int Index(int3 p)=>p.x+8*p.y+64*p.z;
        void Noise(ref Random rng,bool props)
        {
            float density=rng.NextFloat(.1f,.9f);
            for(int i=0;i<snapshot.Length;i++)
            {
                bool solid=rng.NextFloat()<density;
                int cls=props&&rng.NextInt(4)==0?2:1;
                snapshot[i]=solid?MeshCell.Encode(cls,0,0,rng.NextInt(2),rng.NextInt(4),rng.NextInt(4),rng.NextBool()):0;
            }
        }
        Dictionary<(int,int),int> ReferenceFaces()
        {
            var solid=new bool[512];var keys=new int[512];for(int i=0;i<512;i++)keys[i]=-1;
            for(int z=0;z<8;z++)for(int y=0;y<8;y++)for(int x=0;x<8;x++)
            {
                int count=0;var frequencies=new SortedDictionary<int,int>();
                for(int dz=0;dz<2;dz++)for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
                {
                    uint c=At(new int3(x*2+dx,y*2+dy,z*2+dz));int cls=MeshCell.Class(c);if(cls!=0)count++;
                    if(cls==1){int key=MeshCell.Layer(c)*256+MeshCell.Colour(c);frequencies.TryGetValue(key,out int n);frequencies[key]=n+1;}
                }
                int i=Index(new int3(x,y,z));solid[i]=count>=4;int best=0;
                foreach(var frequency in frequencies)if(frequency.Value>best){best=frequency.Value;keys[i]=frequency.Key;}
            }
            var result=new Dictionary<(int,int),int>();
            for(int z=0;z<8;z++)for(int y=0;y<8;y++)for(int x=0;x<8;x++)
            {
                int3 p=new int3(x,y,z);int i=Index(p);if(!solid[i]||keys[i]<0)continue;
                for(int d=0;d<6;d++)
                {
                    int axis=d/2;int3 next=p+BrickMath.FaceDirection((BlockFace)d);bool hidden;
                    if(math.all(next>=0)&&math.all(next<8))hidden=solid[Index(next)];
                    else
                    {
                        int count=0;int3 border=p*2;border[axis]=(d&1)==0?16:-1;
                        int u=(axis+1)%3,v=(axis+2)%3;
                        for(int a=0;a<2;a++)for(int b=0;b<2;b++){int3 sample=border;sample[u]+=a;sample[v]+=b;if(MeshCell.Class(At(sample))!=0)count++;}
                        hidden=count>=2;
                    }
                    if(!hidden)result.Add((i,d),keys[i]);
                }
            }
            return result;
        }
        static int3 HalfPosition(PackedBrickVertex p)=>new int3((int)(p.Lo&255),(int)((p.Lo>>8)&255),(int)((p.Lo>>16)&255));
        void AssertTiling()
        {
            var expected=ReferenceFaces();Assert.AreEqual(0,vertices.Length%4);Assert.AreEqual(vertices.Length/4*6,indices.Length);
            for(int r=0;r<vertices.Length;r+=4)
            {
                var first=vertices[r];int d=(int)(first.Hi&255),axis=d/2,u=(axis+1)%3,v=(axis+2)%3;
                int key=(int)((first.Hi>>8)&255)*256+(int)((first.Hi>>16)&255);
                int3 min=HalfPosition(first)/4,max=min;
                for(int k=1;k<4;k++){int3 p=HalfPosition(vertices[r+k])/4;min=math.min(min,p);max=math.max(max,p);}
                Assert.AreEqual(min[axis],max[axis]);
                for(int b=min[v];b<max[v];b++)for(int a=min[u];a<max[u];a++)
                {
                    int3 p=min;p[axis]-=(d&1)==0?1:0;p[u]=a;p[v]=b;
                    Assert.IsTrue(expected.TryGetValue((Index(p),d),out int expectedKey),"Unexpected or overlapping super-cell face");
                    Assert.AreEqual(expectedKey,key);expected.Remove((Index(p),d));
                }
            }
            Assert.AreEqual(0,expected.Count,"Greedy rectangles cover exactly the reference faces");
        }
        [Test] public void Greedy_TilesExactly()
        {
            for(int z=0;z<16;z++)for(int y=0;y<16;y++)for(int x=0;x<16;x++)snapshot[MeshCell.SnapIndex(x,y,z)]=Cube();
            Mesh();Assert.AreEqual(24,vertices.Length,"zero layer/colour is visible and six full sides merge");AssertTiling();
            // A 4:4 tie chooses the smaller raw key; an all-prop neighbour remains an occluder.
            for(int z=0;z<2;z++)for(int y=0;y<2;y++)for(int x=0;x<4;x++)
                snapshot[MeshCell.SnapIndex(x,y,z)]=x<2?Cube(y==0?0:1):MeshCell.Encode(2,0,0,0,0,0,false);
            Mesh();AssertTiling();
            var rng=new Random(91);for(int trial=0;trial<40;trial++){Noise(ref rng,true);Mesh();AssertTiling();}
        }
        [BurstCompile(CompileSynchronously=true)] struct SourceRatioJob:IJob
        {
            [ReadOnly] public VoxelStructureData Data;
            [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
            [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
            [ReadOnly] public BlobAssetReference<BrickShapeTables> Shapes;
            public NativeArray<uint> Snapshot;
            public NativeList<PackedBrickVertex> Vertices;
            public NativeList<uint> Indices;
            public NativeArray<long> Counts;
            public void Execute()
            {
                long lod0=0,lod1=0;
                for(int b=0;b<Data.Bricks.Length;b++)
                {
                    BrickSnapshotUtility.FromStructure(Data,Data.Bricks[b].Coord,ref Blocks.Value,ref Orientations.Value,Snapshot);
                    BrickMesher.MeshLod0(Snapshot,ref Shapes.Value,Vertices,Indices,out _,out _);lod0+=Indices.Length/3;
                    BrickMesher.MeshLod1(Snapshot,ref Shapes.Value,Vertices,Indices,out _,out _);lod1+=Indices.Length/3;
                }
                Counts[0]=lod0;Counts[1]=lod1;
            }
        }
        static void Ratio(string name,long a,long b)
        { Assert.Greater(a,0);Assert.LessOrEqual(4*b,a,name);TestContext.WriteLine($"{name}: LOD0 {a}, LOD1 {b} triangles ({100.0*b/a:F3}%)."); }
        [Test] public void TriangleRatio_AtMost25Percent()
        {
            ushort cube=content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            using var counts=new NativeArray<long>(2,Allocator.TempJob);
            foreach(bool station in new[]{true,false})
            {
                using var source=station?BenchmarkStructures.HollowStation1M(cube):BenchmarkStructures.Ship20k(cube);
                new SourceRatioJob{Data=source.Data,Blocks=content.BlockDefs,Orientations=content.Orientations,Shapes=shapes,
                    Snapshot=snapshot,Vertices=vertices,Indices=indices,Counts=counts}.Run();
                Ratio(station?"HollowStation1M":"Ship20k",counts[0],counts[1]);
            }
            long zero=0,one=0;var rng=new Random(31);
            for(int trial=0;trial<200;trial++){Noise(ref rng,false);Mesh(true);zero+=indices.Length/3;Mesh();one+=indices.Length/3;}
            Ratio("200 random-noise bricks",zero,one);
        }
        [Test] public void Positions_EvenAndInRange()
        {
            var rng=new Random(99);Noise(ref rng,true);Mesh();Assert.Greater(vertices.Length,0);
            foreach(var vertex in vertices)
            {
                int3 p=HalfPosition(vertex);Assert.IsTrue(math.all(p>=0)&&math.all(p<=32));Assert.AreEqual(int3.zero,p%4);
                Assert.AreEqual(3,(vertex.Hi>>24)&3);Assert.AreEqual(0,vertex.Hi>>26);
            }
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=vertices[(int)indices[i]];var b=vertices[(int)indices[i+1]];var c=vertices[(int)indices[i+2]];
                float3 normal=VoxelNormals.Normal((int)(a.Hi&255));
                Assert.Greater(math.dot(math.cross((float3)(HalfPosition(b)-HalfPosition(a)),(float3)(HalfPosition(c)-HalfPosition(a))),normal),0);
            }
        }
    }
}
