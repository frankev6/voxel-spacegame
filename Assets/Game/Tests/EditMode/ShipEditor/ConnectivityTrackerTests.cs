using System;
using System.Collections.Generic;
using Game.Core;
using Game.ShipEditor.Validation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class ConnectivityTrackerTests
    {
        static void Add(ConnectivityTracker tracker, params int3[] cells) { using var data = new NativeArray<int3>(cells, Allocator.Temp); tracker.OnAdd(data); }
        static void Remove(ConnectivityTracker tracker, params int3[] cells) { using var data = new NativeArray<int3>(cells, Allocator.Temp); tracker.OnRemove(data); }
        [Test] public void Add_MergesIslands()
        {
            using var t = new ConnectivityTracker(); Add(t, new int3(-1,0,0), new int3(1,0,0)); Assert.AreEqual(2,t.IslandCount);
            Add(t,int3.zero); Assert.AreEqual(1,t.IslandCount); Assert.AreEqual(t.RootAt(new int3(-1,0,0)),t.RootAt(new int3(1,0,0))); Assert.AreEqual(3,t.SizeOf(t.RootAt(int3.zero)));
        }
        [Test] public void RemoveLeaf_NoSplit()
        {
            using var t = new ConnectivityTracker(); Add(t,int3.zero,new int3(1,0,0),new int3(2,0,0)); Remove(t,int3.zero);
            Assert.IsFalse(t.ConnectivityPending); Assert.AreEqual(1,t.IslandCount); Assert.AreEqual(-1,t.RootAt(int3.zero)); Assert.AreEqual(2,t.SizeOf(t.RootAt(new int3(1,0,0))));
        }
        [Test] public void RemoveBridge_SmallSplit_Relabels()
        {
            using var t = new ConnectivityTracker(); Add(t,new int3(-2,0,0),new int3(-1,0,0),int3.zero,new int3(1,0,0),new int3(2,0,0)); Remove(t,int3.zero);
            Assert.IsFalse(t.ConnectivityPending); Assert.AreEqual(2,t.IslandCount); Assert.AreNotEqual(t.RootAt(new int3(-1,0,0)),t.RootAt(new int3(1,0,0)));
            using var output = new NativeList<int3>(Allocator.TempJob);
            t.DisconnectedCells(new int3(-1,0,0),output,10); Assert.AreEqual(2,output.Length); foreach(var cell in output) Assert.Greater(cell.x,0);
            t.DisconnectedCells(new int3(-1,0,0),output,1); Assert.AreEqual(1,output.Length);
            // Surviving cells of one damaged footprint can span multiple islands.
            Remove(t,new int3(-1,0,0),new int3(1,0,0)); Assert.AreEqual(2,t.IslandCount);
        }
        [Test] public void RemoveBridge_LargeSplit_AsyncRelabel()
        {
            using var t = new ConnectivityTracker(); using var cells = new NativeList<int3>(Allocator.Temp);
            // Two 9,000-cell lobes connected only by the bridge at x=0.
            for(int x=1;x<=10;x++) for(int y=0;y<30;y++) for(int z=0;z<30;z++) { cells.Add(new int3(x,y,z)); cells.Add(new int3(-x,y,z)); }
            cells.Add(int3.zero); t.OnAdd(cells.AsArray()); Assert.AreEqual(1,t.IslandCount);
            Remove(t,int3.zero); Assert.IsTrue(t.ConnectivityPending); Assert.AreEqual(2,t.IslandCount); Assert.IsFalse(t.ConnectivityPending);
            Assert.AreEqual(9000,t.SizeOf(t.RootAt(new int3(-1,0,0)))); Assert.AreEqual(9000,t.SizeOf(t.RootAt(new int3(1,0,0))));
        }
        [Test] public void Property_RandomOps_MatchFlood()
        {
            using var t = new ConnectivityTracker(64); var solid = new HashSet<int3>();
            using var seed = new NativeList<int3>(Allocator.Temp);
            for(int x=0;x<8;x++) for(int y=0;y<8;y++) for(int z=0;z<8;z++) { var cell=new int3(x,y,z); solid.Add(cell); seed.Add(cell); }
            t.OnAdd(seed.AsArray()); var rng = RngStreams.For(8273,RngPurpose.ShipEditor,0);
            for(int op=0;op<2000;op++)
            {
                int span = op % 2 == 0 ? 8 : 32; var cell=new int3(rng.NextInt(span),rng.NextInt(span),rng.NextInt(span));
                if(solid.Remove(cell)) Remove(t,cell); else {solid.Add(cell); Add(t,cell);}
                var remaining = new HashSet<int3>(solid); var roots = new HashSet<int>(); var queue=new Queue<int3>(); int islands=0;
                while(remaining.Count>0)
                {
                    using var it=remaining.GetEnumerator(); it.MoveNext(); var start=it.Current; remaining.Remove(start); queue.Enqueue(start); islands++;
                    int root=t.RootAt(start); Assert.GreaterOrEqual(root,0); Assert.IsTrue(roots.Add(root),"Distinct flood islands share root at op "+op); int count=0;
                    while(queue.Count>0)
                    {
                        var at=queue.Dequeue(); count++; Assert.AreEqual(root,t.RootAt(at),"Membership at op "+op);
                        for(int d=0;d<6;d++) { int3 n=at+Game.ShipEditor.Jobs.ConnectivityCells.Direction(d); if(remaining.Remove(n)) queue.Enqueue(n); }
                    }
                    Assert.AreEqual(count,t.SizeOf(root));
                }
                Assert.AreEqual(islands,t.IslandCount,"Count at op "+op);
            }
        }
        [Test] public void Dispose_NoLeaks()
        {
            using(var warm = new ConnectivityTracker()) { Add(warm,int3.zero); warm.FullRelabel(); }
            void BaselineLog(string message, string trace, UnityEngine.LogType type)
            { if (type == UnityEngine.LogType.Log && message.StartsWith("Leak Detected", StringComparison.Ordinal)) LogAssert.Expect(type, message); }
            UnityEngine.Application.logMessageReceived += BaselineLog;
            try { UnsafeUtility.CheckForLeaks(); } finally { UnityEngine.Application.logMessageReceived -= BaselineLog; }
            var t=new ConnectivityTracker(); Add(t,int3.zero,new int3(1,0,0)); t.FullRelabel(); Assert.IsTrue(t.ConnectivityPending);
            t.Dispose(); t.Dispose(); Assert.Throws<ObjectDisposedException>(()=>t.CompleteRelabel());
            Assert.AreEqual(0, UnsafeUtility.CheckForLeaks()); LogAssert.NoUnexpectedReceived();
        }
    }
}
