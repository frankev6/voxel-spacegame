using System;
using System.Collections;
using System.Linq;
using Game.App.Boot;
using Game.Data.Blocks;
using Game.Data.Services;
using Game.ShipEditor.Data;
using Game.ShipEditor.Rendering;
using Game.ShipEditor.Services;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.ShipEditor
{
    public sealed class DraftRendererTests
    {
        sealed class Fixture : IDisposable
        {
            readonly ContentService content;
            readonly ShipEditorConfig config;
            readonly VoxelStructure original = new();
            readonly EditorContext context;
            internal readonly EditSession Session;
            internal readonly DraftRenderer Renderer;
            internal readonly ushort Hull;
            internal ShipEditorConfig Config => config;
            internal Fixture(params int3[] cells)
            {
                content = new ContentService(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase);
                config = ScriptableObject.CreateInstance<ShipEditorConfig>();
                context = new EditorContext(config, content.Catalog, content.BlockDefs, content.Orientations, 16);
                for (ushort i = 1; i < content.BlockDefs.Value.Blocks.Length; i++)
                {
                    ref var block = ref content.BlockDefs.Value.Blocks[i];
                    if (block.Kind == BlockKind.Structural && block.Cells.Length == 1) { Hull = i; break; }
                }
                Assert.AreNotEqual(0, Hull);
                using var edits = new NativeList<VoxelEdit>(Allocator.Temp);
                foreach (var cell in cells) edits.Add(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Cell = cell, Block = Hull });
                using var touched = new NativeList<int3>(Allocator.Temp);
                Assert.IsTrue(original.ApplyEdits(edits.AsArray(), new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations }, touched));
                Session = new EditSession(new ShipEditorEntryArgs { Source = original, Options = new EditSessionOptions { Creative = true } }, context);
                Renderer = new DraftRenderer(Session, context, null, null, content.PropMeshes);
            }
            internal void Remove(int3 cell)
            {
                Assert.IsTrue(new DraftReader(Session.Draft, context).TryGetBlockAt(cell, out var record));
                using var batch = new EditBatch(); var report = new ApplyReport();
                batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = record });
                Assert.IsTrue(Session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Remove, "Renderer fixture", report));
            }
            internal void ExerciseProps()
            {
                ushort prop = 0;
                for (ushort i = 1; i < content.BlockDefs.Value.Blocks.Length; i++)
                    if (content.BlockDefs.Value.Blocks[i].Kind != BlockKind.Structural && content.BlockDefs.Value.Blocks[i].PropMeshIndex >= 0) { prop = i; break; }
                Assert.AreNotEqual(0, prop, "Fixture requires a real content prop mesh.");
                int mesh = content.BlockDefs.Value.Blocks[prop].PropMeshIndex;
                using var set = new InstancedPropSet(content.PropMeshes[mesh]);
                var a = new BlockRecord { Block = prop, DamageOffset = -1 };
                var b = a; b.Origin = new int3(3, 0, 0);
                var c = a; c.Origin = new int3(6, 0, 0);
                set.Add(a, context, Session.Draft.CellSize); set.Add(b, context, Session.Draft.CellSize); set.Add(c, context, Session.Draft.CellSize);
                int before = set.MutationCount;
                Assert.IsTrue(set.Remove(b)); Assert.AreEqual(before + 1, set.MutationCount);
                Assert.AreEqual(2, set.Count); Assert.AreEqual(c, set.Records[1]);
                Assert.IsTrue(set.Remove(c)); Assert.IsTrue(set.Remove(a)); Assert.AreEqual(0, set.Count);
            }
            public void Dispose()
            { Renderer.Dispose(); Session.Dispose(); context.Dispose(); original.Dispose(); content.Dispose(); Object.Destroy(config); }
        }
        [Test] public void SingleEdit_RemeshesOneBrick()
        {
            using var f = new Fixture(new int3(3,3,3)); f.Renderer.UpdateMeshes(); int before = f.Renderer.RemeshCount;
            f.Remove(new int3(3,3,3)); f.Renderer.UpdateMeshes(); Assert.AreEqual(before + 1, f.Renderer.RemeshCount);
        }
        [Test] public void BoundaryEdit_RemeshesTwo()
        {
            using var f = new Fixture(new int3(15,3,3), new int3(16,3,3)); f.Renderer.UpdateMeshes(); int before = f.Renderer.RemeshCount;
            f.Remove(new int3(15,3,3)); f.Renderer.UpdateMeshes(); Assert.AreEqual(before + 2, f.Renderer.RemeshCount);
        }
        [Test] public void EditDuringAsync_Requeued_FinalMatches()
        {
            using var f = new Fixture(new int3(3,3,3), new int3(4,3,3)); f.Config.SyncRemeshBricksPerFrame = 0;
            f.Renderer.UpdateMeshes(); Assert.Greater(f.Renderer.PendingJobs, 0);
            f.Remove(new int3(3,3,3)); f.Renderer.UpdateMeshes(); Assert.Greater(f.Renderer.PendingJobs, 0);
            f.Renderer.UpdateMeshes(); Assert.AreEqual(0, f.Renderer.PendingJobs);
            var mesh = f.Renderer.Slots[int3.zero].Mesh;
            using var fresh = new Fixture(new int3(4,3,3)); fresh.Renderer.UpdateMeshes();
            var expected = fresh.Renderer.Slots[int3.zero].Mesh;
            Assert.AreEqual(expected.vertexCount, mesh.vertexCount); Assert.AreEqual(expected.bounds, mesh.bounds);
            CollectionAssert.AreEqual(expected.triangles, mesh.triangles);
            using var actualData = Mesh.AcquireReadOnlyMeshData(mesh); using var expectedData = Mesh.AcquireReadOnlyMeshData(expected);
            CollectionAssert.AreEqual(expectedData[0].GetVertexData<byte>().ToArray(), actualData[0].GetVertexData<byte>().ToArray());
        }
        [Test] public void SliceChange_LimitedBricks()
        {
            using var f = new Fixture(new int3(3,3,3), new int3(3,19,3), new int3(3,35,3));
            f.Renderer.UpdateMeshes(); int before = f.Renderer.RemeshCount;
            f.Renderer.SetSlice(true, 20); f.Renderer.UpdateMeshes(); Assert.AreEqual(before + 1, f.Renderer.RemeshCount);
            f.Renderer.SetSlice(true, 36); f.Renderer.UpdateMeshes(); Assert.AreEqual(before + 3, f.Renderer.RemeshCount);
        }
        [Test] public void PropAddRemove_O1()
        { using var f = new Fixture(new int3(3,3,3)); f.ExerciseProps(); }
        [UnityTest] public IEnumerator Dispose_NoLeaks()
        {
            var presentation = World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<PresentationSystemGroup>();
            bool wasEnabled = presentation != null && presentation.Enabled;
            var leakMode = NativeLeakDetection.Mode;
            if (presentation != null) presentation.Enabled = false;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try
            {
                using (var warmup = new Fixture(new int3(3,3,3))) warmup.Renderer.UpdateMeshes();
                yield return null;
                UnsafeUtility.CheckForLeaks();
                Mesh[] meshes;
                using (var f = new Fixture(new int3(3,3,3)))
                {
                    f.Config.SyncRemeshBricksPerFrame = 0;
                    f.Renderer.UpdateMeshes(); Assert.Greater(f.Renderer.PendingJobs, 0);
                    meshes = f.Renderer.Slots.Values.Select(slot => slot.Mesh).ToArray();
                    f.ExerciseProps();
                }
                yield return null;
                Assert.IsTrue(meshes.All(mesh => mesh == null));
                Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "Renderer snapshot, shape, slot and prop allocations must be released.");
            }
            finally
            {
                NativeLeakDetection.Mode = leakMode;
                if (presentation != null) presentation.Enabled = wasEnabled;
            }
        }
    }
}
