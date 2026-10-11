using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    public sealed class BrickMesherTests
    {
        BlobAssetReference<BrickShapeTables> shapes;
        NativeArray<uint> snapshot;
        NativeList<PackedBrickVertex> vertices;
        NativeList<uint> indices;
        [SetUp] public void SetUp()
        {
            var orientations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Temp);
            try { shapes = BrickShapeTablesBuilder.Build(ref orientations.Value); } finally { orientations.Dispose(); }
            snapshot = new NativeArray<uint>(5832, Allocator.TempJob);
            vertices = new NativeList<PackedBrickVertex>(100000, Allocator.TempJob); indices = new NativeList<uint>(200000, Allocator.TempJob);
        }
        [TearDown] public void TearDown() { indices.Dispose(); vertices.Dispose(); snapshot.Dispose(); shapes.Dispose(); }
        [BurstCompile(CompileSynchronously = true)] struct MeshJob : IJob
        {
            [ReadOnly] public NativeArray<uint> Snapshot;
            [ReadOnly] public BlobAssetReference<BrickShapeTables> Shapes;
            public NativeList<PackedBrickVertex> Vertices;
            public NativeList<uint> Indices;
            public void Execute() => BrickMesher.MeshLod0(Snapshot, ref Shapes.Value, Vertices, Indices, out _, out _);
        }
        void Mesh() => new MeshJob { Snapshot=snapshot, Shapes=shapes, Vertices=vertices, Indices=indices }.Run();
        static uint Cube(int colour=0) => MeshCell.Encode(1,0,0,0,colour,0,false);
        void Randomize(ref Random rng)
        {
            for(int i=0;i<snapshot.Length;i++)
            {
                int cls=rng.NextInt(5)==0 ? (rng.NextBool()?1:2) : 0;
                snapshot[i]=cls==0 ? 0 : MeshCell.Encode(cls,cls==1?rng.NextInt(5):0,rng.NextInt(24),rng.NextInt(64),rng.NextInt(256),rng.NextInt(4),rng.NextBool());
            }
        }
        static int3 Vertex(ShapeFace f,int i) => i==0?f.V0:i==1?f.V1:i==2?f.V2:f.V3;
        uint At(int3 p) => snapshot[MeshCell.SnapIndex(p.x,p.y,p.z)];
        bool Occupied(int3 p) => MeshCell.Class(At(p))!=0;
        int ReferenceAO(int3 cell, int direction,int3 vertex)
        {
            int axis=direction/2,u=(axis+1)%3,v=(axis+2)%3,best=3;
            int3 normal=default; normal[axis]=(direction&1)==0?1:-1;
            for(int a=-1;a<=1;a+=2) for(int b=-1;b<=1;b+=2)
            {
                if(vertex[u]==0 && a>0 || vertex[u]==2 && a<0 || vertex[v]==0 && b>0 || vertex[v]==2 && b<0) continue;
                int3 s1=cell+normal,s2=s1,corner=s1; s1[u]+=a;s2[v]+=b;corner[u]+=a;corner[v]+=b;
                int one=Occupied(s1)?1:0,two=Occupied(s2)?1:0,diagonal=Occupied(corner)?1:0;
                best=math.min(best,one+two==2?0:3-one-two-diagonal);
            }
            return best;
        }
        IEnumerable<(int3 Cell,uint Value,ShapeFace Face)> Faces()
        {
            for(int z=0;z<16;z++) for(int y=0;y<16;y++) for(int x=0;x<16;x++)
            {
                int3 cell=new int3(x,y,z);uint value=At(cell);if(MeshCell.Class(value)!=1)continue;
                int table=MeshCell.Shape(value)*24+MeshCell.Orient(value);
                for(int f=0;f<shapes.Value.FaceCount[table];f++)
                {
                    var face=shapes.Value.Faces[shapes.Value.FirstFace[table]+f];
                    if(face.Plane!=255)
                    {
                        uint neighbour=At(cell+BrickMath.FaceDirection((BlockFace)face.Plane));int cls=MeshCell.Class(neighbour);
                        int full=cls==0?0:cls==2?63:shapes.Value.FullFaces[MeshCell.Shape(neighbour)*24+MeshCell.Orient(neighbour)];
                        if((full&(1<<(face.Plane^1)))!=0)continue;
                    }
                    yield return (cell,value,face);
                }
            }
        }
        static ulong Bits(PackedBrickVertex v,bool withAO) => v.Lo | ((ulong)(withAO?v.Hi:v.Hi&~(3u<<24))<<32);
        static (ulong,ulong,ulong) Key(ulong a,ulong b,ulong c)
        { if(a>b)(a,b)=(b,a);if(b>c)(b,c)=(c,b);if(a>b)(a,b)=(b,a);return(a,b,c); }
        static void Add(Dictionary<(ulong,ulong,ulong),int> set,ulong a,ulong b,ulong c)
        { var key=Key(a,b,c);set.TryGetValue(key,out int count);set[key]=count+1; }
        void MatchesReference(bool withAO)
        {
            var expected=new Dictionary<(ulong,ulong,ulong),int>();
            var packed=new PackedBrickVertex[4];var ao=new int[4];
            foreach(var item in Faces())
            {
                var f=item.Face;uint c=item.Value;
                for(int i=0;i<f.VertexCount;i++)
                {
                    int3 local=Vertex(f,i),p=item.Cell*2+local;ao[i]=f.Plane==255?3:ReferenceAO(item.Cell,f.Plane,local);
                    packed[i]=PackedBrickVertex.Pack(p.x,p.y,p.z,f.Normal,MeshCell.Layer(c),MeshCell.Colour(c),ao[i],MeshCell.Damage(c),MeshCell.Scorched(c));
                }
                ulong a=Bits(packed[0],withAO),b=Bits(packed[1],withAO),d=Bits(packed[2],withAO);
                if(f.VertexCount==3){Add(expected,a,b,d);continue;}
                ulong e=Bits(packed[3],withAO);
                if(ao[0]+ao[2]<ao[1]+ao[3]){Add(expected,a,b,e);Add(expected,b,d,e);}
                else{Add(expected,a,b,d);Add(expected,a,d,e);}
            }
            for(int i=0;i<indices.Length;i+=3)
            {
                var key=Key(Bits(vertices[(int)indices[i]],withAO),Bits(vertices[(int)indices[i+1]],withAO),Bits(vertices[(int)indices[i+2]],withAO));
                if(!expected.TryGetValue(key,out int count))Assert.Fail("Unexpected emitted triangle at index "+i);
                if(count==1)expected.Remove(key);else expected[key]=count-1;
            }
            Assert.AreEqual(0,expected.Count,"Every expected triangle must be emitted exactly once");
        }
        [Test] public void Faces_MatchBruteForce()
        {
            var rng=new Random(11);
            for(int trial=0;trial<500;trial++){Randomize(ref rng);Mesh();MatchesReference(false);}
        }
        [Test] public void BorderCulling_UsesNeighbours()
        {
            // Fully filled halo suppresses every cube face; opening one neighbour emits one quad.
            for(int i=0;i<snapshot.Length;i++)snapshot[i]=Cube();Mesh();Assert.AreEqual(0,indices.Length);Assert.AreEqual(0,vertices.Length);
            snapshot[MeshCell.SnapIndex(16,3,5)]=0;Mesh();Assert.AreEqual(6,indices.Length);MatchesReference(true);
            BrickMesher.MeshLod0(snapshot,ref shapes.Value,vertices,indices,out var min,out var max);
            Assert.AreEqual(new float3(16,3,5),min);Assert.AreEqual(new float3(16,4,6),max);
        }
        [Test] public void Lod0_TwoTrianglesPerExposedFace()
        {
            var rng=new Random(123);Randomize(ref rng);Mesh();int quads=0,triangles=0;
            foreach(var item in Faces())if(item.Face.VertexCount==4)quads++;else triangles++;
            Assert.AreEqual(2*quads+triangles,indices.Length/3);
        }
        [Test] public void Winding_MatchesNormals()
        {
            var rng=new Random(87);Randomize(ref rng);Mesh();
            float3 Position(PackedBrickVertex p)=>new float3(p.Lo&255,(p.Lo>>8)&255,(p.Lo>>16)&255)*.5f;
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=vertices[(int)indices[i]];var b=vertices[(int)indices[i+1]];var c=vertices[(int)indices[i+2]];
                Assert.Greater(math.dot(math.cross(Position(b)-Position(a),Position(c)-Position(a)),VoxelNormals.Normal((int)(a.Hi&255))),0);
            }
        }
        [Test] public void AO_MatchesBruteForce()
        {
            var rng=new Random(98);for(int trial=0;trial<10;trial++){Randomize(ref rng);Mesh();MatchesReference(true);}
        }
        [Test] public void Welding_FlatWall289()
        {
            // One exposed 16x16 top face; other directions are occluded by solid halo cells.
            for(int z=-1;z<=16;z++)for(int y=-1;y<=0;y++)for(int x=-1;x<=16;x++)snapshot[MeshCell.SnapIndex(x,y,z)]=Cube();
            Mesh();Assert.AreEqual(256*6,indices.Length);Assert.AreEqual(289,vertices.Length);MatchesReference(true);
        }
    }
}
