using System.Diagnostics;
using Game.Data.Blocks;
using Game.Tests.Common.Perf;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Game.Tests.PlayMode.Voxel
{
    public sealed class BrickMesherPerfTests
    {
        [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
        struct MesherJob : IJob
        {
            [ReadOnly] public NativeArray<uint> Snapshot;
            [ReadOnly] public BlobAssetReference<BrickShapeTables> Shapes;
            public NativeList<PackedBrickVertex> Vertices;
            public NativeList<uint> Indices;
            public void Execute() => BrickMesher.MeshLod0(Snapshot, ref Shapes.Value, Vertices, Indices, out _, out _);
        }
        static BlobAssetReference<BrickShapeTables> Shapes()
        {
            var orientations=OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(),Allocator.Temp);
            try{return BrickShapeTablesBuilder.Build(ref orientations.Value);}finally{orientations.Dispose();}
        }
        static double[] Measure(MesherJob job,int count)
        {
            for(int i=0;i<20;i++)job.Run();
            var samples=new double[count];
            for(int i=0;i<count;i++)
            {
                long start=Stopwatch.GetTimestamp();job.Run();
                samples[i]=(Stopwatch.GetTimestamp()-start)*1000000.0/Stopwatch.Frequency;
            }
            return samples;
        }
        [Test, Performance, Category("Performance")]
        public void Lod0_TypicalDamagedHullBrick_Under60us()
        {
            var snapshot=new NativeArray<uint>(5832,Allocator.TempJob);
            using var shapes=Shapes();using var vertices=new NativeList<PackedBrickVertex>(8192,Allocator.TempJob);
            using var indices=new NativeList<uint>(16384,Allocator.TempJob);
            try
            {
                uint cube=MeshCell.Encode(1,0,0,0,0,0,false);
                for(int z=-1;z<=16;z++)for(int y=-1;y<4;y++)for(int x=-1;x<=16;x++)
                {
                    float3 delta=new float3(x+.5f,y+.5f,z+.5f)-new float3(8,4,8);
                    if(math.lengthsq(delta)>=9) snapshot[MeshCell.SnapIndex(x,y,z)]=cube;
                }
                var job=new MesherJob{Snapshot=snapshot,Shapes=shapes,Vertices=vertices,Indices=indices};
                var samples=Measure(job,1000);var run=new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("Lod0DamagedHull",samples,"us");
                run.Write("Lod0DamagedHull",60);run.AssertMedian("Lod0DamagedHull",60);
                TestContext.WriteLine($"Damaged slab: {vertices.Length} vertices, {indices.Length/3} triangles; 1000 single-threaded Burst runs (safety disabled on job).");
            }
            finally{snapshot.Dispose();}
        }
        [Test, Performance, Category("Performance")]
        public void Lod0_Checkerboard_Report()
        {
            var snapshot=new NativeArray<uint>(5832,Allocator.TempJob);
            using var shapes=Shapes();using var vertices=new NativeList<PackedBrickVertex>(100000,Allocator.TempJob);
            using var indices=new NativeList<uint>(200000,Allocator.TempJob);
            try
            {
                for(int z=0;z<16;z++)for(int y=0;y<16;y++)for(int x=0;x<16;x++)
                    if(((x+y+z)&1)==0)snapshot[MeshCell.SnapIndex(x,y,z)]=MeshCell.Encode(1,0,0,0,(x+17*y+67*z)%256,0,false);
                var job=new MesherJob{Snapshot=snapshot,Shapes=shapes,Vertices=vertices,Indices=indices};
                var samples=Measure(job,1000);var run=new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("Lod0Checkerboard",samples,"us");
                run.Write("Lod0Checkerboard");Assert.AreEqual(49152,vertices.Length);Assert.AreEqual(73728,indices.Length);
                for(int z=0;z<16;z++)for(int y=0;y<16;y++)for(int x=0;x<16;x++)
                    snapshot[MeshCell.SnapIndex(x,y,z)]=MeshCell.Encode(1,y%2==0?3:4,0,0,(x+17*(y/2)+67*z)%256,0,false);
                job.Run();uint largest=0;foreach(uint index in indices){Assert.Less(index,(uint)vertices.Length);largest=math.max(largest,index);}
                Assert.GreaterOrEqual(vertices.Length,73728);Assert.Greater(largest,65535u);Assert.AreEqual((uint)vertices.Length-1,largest);
                TestContext.WriteLine($"Valid painted long-slope tiling: {vertices.Length} vertices, {indices.Length/3} triangles, maximum index {largest}; requires UInt32 GPU indices.");
            }
            finally{snapshot.Dispose();}
        }
    }
}
