using Game.Core.Save;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.PlayMode.Galaxy
{
    public sealed class T2StreamingTests
    {
        [Test] public void LoadUnload_Hysteresis()
        {
            using var f = new StarSystemLoadTests.Fixture(true); f.Load(); f.Stream(0);
            Entity root = f.Grid(); Assert.AreNotEqual(Entity.Null, root); Assert.IsTrue(f.Loader.IsArrivalReady);
            f.Stream(12); Assert.AreEqual(root, f.Grid(), "Retain inside the wider unload boundary.");
            f.Stream(14); Assert.AreEqual(Entity.Null, f.Grid());
            f.Stream(12); Assert.AreEqual(Entity.Null, f.Grid(), "Do not reload outside the load boundary.");
            f.Stream(0); Assert.AreNotEqual(Entity.Null, f.Grid());
        }
        [Test] public void Damage_PersistsSmallStation()
        {
            using var f = new StarSystemLoadTests.Fixture(true); f.Load(); f.Stream(0);
            RemoveCell(f); f.Stream(100); var delta = f.State.GetOrCreateDelta(f.Station.Id.System);
            Assert.IsTrue(delta.TryGetStructure(f.Station.Id.LocalKey, out var saved)); Assert.AreEqual(3, saved.Structure.SolidCellCount);
            f.Stream(0); Assert.AreEqual(3, CountCells(f)); f.Stream(100);
            byte[] bytes;
            using (var writer = new SaveWriter())
            {
                new GalaxySaveParticipant(f.State, f.Catalog).Capture(writer);
                try { bytes = writer.ToArray(block => block.Encode()); } finally { writer.DisposeDeferredBlocks(); }
            }
            using var restored = new GalaxyState();
            new GalaxySaveParticipant(restored, f.Catalog).Restore(new SaveReader(bytes), 1);
            Assert.IsTrue(restored.GetOrCreateDelta(f.Station.Id.System).TryGetStructure(f.Station.Id.LocalKey, out var copy));
            Assert.AreEqual(3, copy.Structure.SolidCellCount);
            var names = Game.Voxel.Data.VoxsBlockNames.FromCatalog(f.Catalog);
            Assert.AreEqual(saved.Structure.ComputeContentHash(names), copy.Structure.ComputeContentHash(names));
        }
        [Test] public void Damage_LargeStationResets()
        {
            // Four cells exceed this fixture's two-cell persistence cap; no large real station is needed.
            using var f = new StarSystemLoadTests.Fixture(true, 2); f.Load(); f.Stream(0);
            RemoveCell(f); f.Stream(100);
            Assert.IsFalse(f.State.GetOrCreateDelta(f.Station.Id.System).TryGetStructure(f.Station.Id.LocalKey, out _));
            f.Stream(0); Assert.AreEqual(4, CountCells(f));
        }
        [Test] public void CoreLost_HulkPersists()
        {
            using var f = new StarSystemLoadTests.Fixture(true); f.Load(); f.Stream(0);
            Entity root = f.Grid(); f.Em.AddComponent<Hulk>(root);
            f.Watch.Update(f.World.Unmanaged); f.Em.CompleteAllTrackedJobs(); f.Tick();
            CollectionAssert.Contains(f.State.GetOrCreateDelta(f.Station.Id.System).Destroyed, f.Station.Id.LocalKey);
            Assert.AreEqual(root, f.Grid(), "Core loss leaves the physical grid in place.");
            f.Stream(100); f.Stream(0); Assert.IsTrue(f.Em.HasComponent<Hulk>(f.Grid()));
            Assert.AreEqual(GridKind.Hulk, f.Em.GetComponentData<VoxelGrid>(f.Grid()).Kind);
        }
        [Test] public void NoLeaks_TwentyCycles()
        {
            using var f = new StarSystemLoadTests.Fixture(true); f.Load();
            for (int i = 0; i < 20; i++)
            {
                f.Stream(0); var source = f.Em.GetComponentData<GridSource>(f.Grid());
                Assert.IsNotNull(f.Registry.Get(source.StructureHandle));
                f.Stream(100); Assert.AreEqual(Entity.Null, f.Grid()); Assert.IsNull(f.Registry.Get(source.StructureHandle));
            }
            var delta = f.State.GetOrCreateDelta(f.Station.Id.System);
            Assert.AreEqual(1, delta.StructureDiffs.Count); Assert.IsFalse(delta.StructureDiffs[0].Structure.IsPinned);
            f.Unload(); f.DrainRetired();
        }
        static int CountCells(StarSystemLoadTests.Fixture f)
        {
            Entity root = f.Grid();
            int count = 0;
            foreach (var slot in f.Em.GetBuffer<BrickSlot>(root))
                if (slot.Brick != Entity.Null) count += f.Em.GetComponentData<Brick>(slot.Brick).SolidCount;
            return count;
        }
        static void RemoveCell(StarSystemLoadTests.Fixture f)
        {
            Entity root = f.Grid(); var grid = f.Em.GetComponentData<VoxelGrid>(root); grid.Revision++;
            int3 at = new(-2, 0, 0);
            Entity entity = f.Em.GetBuffer<BrickSlot>(root)[BrickMath.SlotIndex(BrickMath.BrickOf(at), grid.BrickMin, grid.BrickDims)].Brick;
            var brick = f.Em.GetComponentData<Brick>(entity); var dirty = f.Em.GetComponentData<BrickDirty>(entity);
            Assert.IsTrue(BrickWrite.Set(ref brick, f.Em.GetBuffer<BrickCells>(entity), f.Em.GetBuffer<BrickSparseCells>(entity),
                f.Em.GetBuffer<BrickSolidMask>(entity), ref dirty, BrickMath.LocalIndex(BrickMath.LocalOf(at)), default, grid.Revision));
            f.Em.SetComponentData(entity, brick); f.Em.SetComponentData(entity, dirty); f.Em.SetComponentData(root, grid);
        }
    }
}
