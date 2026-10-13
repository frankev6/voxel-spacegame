using System.Linq;
using Game.Core;
using Game.Galaxy.Data;
using Game.Voxel.Model;
using NUnit.Framework;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class StarSystemDeltaTests
    {
        [Test] public void Sorted_UnderRandomOrder()
        {
            var random = new Unity.Mathematics.Random(173);
            for (int permutation = 0; permutation < 100; permutation++)
            {
                using var state = new GalaxyState();
                var delta = state.GetOrCreateDelta(new StarSystemId(1));
                int[] order = Enumerable.Range(1, 16).ToArray();
                for (int i = order.Length - 1; i > 0; i--)
                { int j = random.NextInt(i + 1); (order[i], order[j]) = (order[j], order[i]); }
                foreach (int value in order)
                {
                    ulong key = (ulong)value; var id = new StarSystemId((uint)value);
                    Assert.IsTrue(delta.MarkDestroyed(key)); Assert.IsFalse(delta.MarkDestroyed(key));
                    Assert.IsTrue(delta.MarkInteracted(key)); Assert.IsFalse(delta.MarkInteracted(key));
                    delta.SetReplacement(key, new ContentId("body_old")); delta.SetReplacement(key, new ContentId("body_new"));
                    delta.SetStructure(key, 1, new VoxelStructure()); delta.SetStructure(key, 2, new VoxelStructure());
                    Assert.IsTrue(state.AddKnown(id)); Assert.IsFalse(state.AddKnown(id));
                    Assert.IsTrue(state.AddVisited(id)); Assert.IsFalse(state.AddVisited(id));
                    state.GetOrCreateDelta(id).IncrementVisit();
                }
                var expected = Enumerable.Range(1, 16).Select(i => (ulong)i).ToArray();
                CollectionAssert.AreEqual(expected, delta.Destroyed); CollectionAssert.AreEqual(expected, delta.Interacted);
                CollectionAssert.AreEqual(expected, delta.Replacements.Select(r => r.BodyKey));
                CollectionAssert.AreEqual(expected, delta.StructureDiffs.Select(r => r.BodyKey));
                CollectionAssert.AreEqual(expected, state.Known.Select(i => (ulong)i));
                CollectionAssert.AreEqual(expected, state.Visited.Select(i => (ulong)i));
                CollectionAssert.AreEqual(expected, state.DeltasSorted().Select(d => (ulong)d.StarSystem.Value));
                Assert.IsTrue(delta.Replacements.All(r => r.Template.Value == "body_new"));
                Assert.IsTrue(delta.StructureDiffs.All(r => r.Revision == 2));
                uint revision = state.Revision; state.RouteDestination = new StarSystemId(3); state.RouteDestination = new StarSystemId(3);
                Assert.AreEqual(revision + 1, state.Revision); state.Clear(); Assert.AreEqual(revision + 2, state.Revision);
                Assert.IsEmpty(state.Known); Assert.IsEmpty(state.Visited); Assert.IsEmpty(state.DeltasSorted());
            }
        }
        [Test] public void StorySpawn_RemovalKeepsSlots()
        {
            using var delta = new StarSystemDelta(new StarSystemId(1));
            Assert.AreEqual(0, delta.AddStorySpawn(new StorySpawnRecord { Template = new ContentId("body_a") }));
            Assert.AreEqual(1, delta.AddStorySpawn(new StorySpawnRecord { Template = new ContentId("body_b") }));
            Assert.IsTrue(delta.RemoveStorySpawn(0)); Assert.IsFalse(delta.RemoveStorySpawn(0));
            Assert.AreEqual(2, delta.AddStorySpawn(new StorySpawnRecord { Template = new ContentId("body_c") }));
            Assert.IsTrue(delta.StorySpawns[0].Removed); Assert.AreEqual("body_b", delta.StorySpawns[1].Template.Value);
            Assert.AreEqual("body_c", delta.StorySpawns[2].Template.Value); Assert.IsFalse(delta.IsEmpty);
        }
    }
}
