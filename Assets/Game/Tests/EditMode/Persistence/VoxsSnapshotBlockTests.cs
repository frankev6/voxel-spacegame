using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Game.Core.Events;
using Game.Core.Save;
using Game.Persistence.Container;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Voxel;
using Game.Voxel.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-008: snapshot isolation and ownership through real save-service success/failure paths.</summary>
    public sealed class VoxsSnapshotBlockTests
    {
        string root;
        SaveService service;
        readonly SlotRef slot = new SlotRef(new ProfileId("p001"), new SlotId("manual_001"));
        [SetUp] public void SetUp()
        {
            EventHub.Reset();
            root = Path.Combine(Path.GetTempPath(), "VoxsSnapshotTests_" + Guid.NewGuid().ToString("N"));
            var registry = new SaveChunkRegistry(new[] {
                new ChunkRegistration("TSTA", 1000, "test", "structures"),
                new ChunkRegistration("TSTB", 1010, "test", "later") });
            service = new SaveService(new SavePaths(root), null, null, new SaveServiceConfig(), registry);
            service.AttachContext(new FakeSaveContext());
        }
        [TearDown] public void TearDown()
        {
            service.Dispose(); EventHub.Reset(); TestContentFactory.DestroyAll();
            if (Directory.Exists(root)) Directory.Delete(root, true);
            else if (File.Exists(root)) File.Delete(root);
        }
        [Test] public void Capture_IsolatesFromLaterMutation()
        {
            var c = VoxsCodecTests.Catalog(); var names = VoxsBlockNames.FromCatalog(c);
            using var source = VoxsTestStructures.Random(c, 11);
            source.Source.Partial = true; source.Source.RemovedBricks.Dispose();
            source.Source.RemovedBricks = new NativeArray<int3>(new[] { new int3(10) }, Allocator.Persistent);
            byte[] expected = VoxsCodec.Encode(source.Source, names, VoxsEncodeOptions.ForSaveChunk);
            using var snapshot = VoxsSnapshotBlock.Capture(source.Source, names);
            source.Source.Palette[1] = default;
            source.Source.Instances[0] = default;
            source.Source.Cells[0] = default;
            source.Source.BrickCoords[0] = new int3(999);
            source.Source.RemovedBricks[0] = new int3(-999);
            source.Source.CellSize = 12;
            byte[] actual = Task.Run(() => snapshot.Encode()).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(expected, actual);
            Task.Run(() => snapshot.Dispose()).GetAwaiter().GetResult();
            snapshot.Dispose(); Assert.Throws<ObjectDisposedException>(() => snapshot.Encode());
        }
        [UnityTest] public IEnumerator SaveLoad_RestoresStructures()
        {
            var c = VoxsCodecTests.Catalog(); var names = VoxsBlockNames.FromCatalog(c);
            using var a = VoxsTestStructures.Random(c, 31); using var b = VoxsTestStructures.Random(c, 32);
            using var participant = new FakeStructureParticipant(c, a.Source, b.Source); service.Register(participant);
            byte[][] expected = { VoxsCodec.Encode(a.Source, names, VoxsEncodeOptions.ForSaveChunk), VoxsCodec.Encode(b.Source, names, VoxsEncodeOptions.ForSaveChunk) };
            var save = service.SaveAsync(slot, SaveReason.Manual).GetAwaiter();
            double until = UnityEditor.EditorApplication.timeSinceStartup + 20;
            while (!save.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
            Assert.IsTrue(save.IsCompleted, "save timeout"); Assert.IsTrue(save.GetResult().Success);
            foreach (var block in participant.Captured) Assert.Throws<ObjectDisposedException>(() => block.Encode());
            var read = service.ReadAsync(slot).GetAwaiter();
            until = UnityEditor.EditorApplication.timeSinceStartup + 20;
            while (!read.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
            Assert.IsTrue(read.IsCompleted, "load timeout"); var loaded = read.GetResult(); Assert.IsTrue(loaded.Success);
            service.Restore(loaded.Save); Assert.AreEqual(2, participant.Restored.Count);
            for (int i = 0; i < 2; i++) CollectionAssert.AreEqual(expected[i], VoxsCodec.Encode(VoxsTestStructures.View(participant.Restored[i]), names, VoxsEncodeOptions.ForSaveChunk));
        }
        [Test] public void AllPaths_Dispose()
        {
            var c = VoxsCodecTests.Catalog(); using var source = VoxsTestStructures.Random(c, 7);
            using var participant = new FakeStructureParticipant(c, source.Source, source.Source); service.Register(participant);
            var later = new FakeParticipant("TSTB"); service.Register(later);
            Assert.IsTrue(service.WriteCaptured(service.CaptureNow(slot, SaveReason.Manual, null)).Success);
            var mode = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            UnsafeUtility.CheckForLeaks();
            try
            {
                // Warmed success, a later participant failing capture, a filesystem failure,
                // and codec failure on the save worker must all release persistent copies.
                for (int path = 0; path < 4; path++)
                {
                    participant.Captured.Clear(); later.ThrowOnCapture = path == 1;
                    if (path == 2) { Directory.Delete(root, true); File.WriteAllText(root, "blocks directory creation"); }
                    if (path == 3) { File.Delete(root); source.Source.Cells[0] = new Game.Voxel.Model.Cell { Hp = 1 }; }
                    if (path == 1)
                    {
                        LogAssert.Expect(LogType.Error, new Regex("capture of chunk TSTB"));
                        LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: capture failure for the test"));
                    }
                    var captured = service.CaptureNow(slot, SaveReason.Manual, null);
                    var result = Task.Run(() => service.WriteCaptured(captured)).GetAwaiter().GetResult();
                    Assert.AreEqual(path == 0, result.Success, "path " + path);
                    Assert.AreEqual(2, participant.Captured.Count);
                    foreach (var block in participant.Captured) { Assert.Throws<ObjectDisposedException>(() => block.Encode()); block.Dispose(); }
                }
                Assert.AreEqual(0, UnsafeUtility.CheckForLeaks());
            }
            finally { NativeLeakDetection.Mode = mode; }
        }
    }
}
