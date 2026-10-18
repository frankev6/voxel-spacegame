using Game.Data.Combat;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Weapons
{
    /// <summary>DATA-013 (ADR-0121 decision 2): damage takes a large hull block apart cell by cell; the instance goes with its last cell.</summary>
    [Category("Scenario")]
    public sealed class LargeHullTests
    {
        [Test] public void Damage_RemovesCellsThenInstance()
        {
            using var f = new CombatFixture();
            ushort block = f.Flight.Index("block_hull_large_2");
            var source = new VoxelStructure(.5f);
            Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = block, Origin = int3.zero }, f.Flight.Content));
            var root = f.Flight.SpawnSource(source, new float3(0, 0, 20), motion: GridMotion.Static);
            for (int i = 0; i < 60 && !f.Em.HasBuffer<BlockInstance>(root); i++) f.Tick();
            f.Tick(10);
            var streams = f.World.GetSingleton<DamageStreams>();
            int removed = 0; uint sequence = 0;
            // Layer by layer, so the cells left stay connected (no split).
            for (int z = 0; z < 2; z++) for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
            {
                streams.Damage.PostFromMainThread(new DamageEvent { Grid = root, LocalPoint = (new float3(x, y, z) + .5f) * .5f, LocalDir = new float3(0, 0, 1),
                    Amount = 1000, Penetration = 1, Type = DamageType.Energy, Producer = ProducerKind.Test, Sequence = sequence++,
                    Flags = DamageEventFlags.NoImpulse | DamageEventFlags.NoKick });
                f.Tick(3); removed++;
                if (removed == 8) break;
                Assert.AreEqual(8 - removed, f.Cells(root), $"one cell per hit ({removed})");
                var instance = Instance(f, root, block);
                Assert.AreEqual(8 - removed, instance.CellsAlive, "the block loses cells, not itself");
                Assert.AreEqual(BlockInstanceFlags.None, instance.Flags & BlockInstanceFlags.Destroyed);
            }
            f.Tick(10);
            bool gone = !f.Em.Exists(root) || !f.Em.HasBuffer<BlockInstance>(root) || f.Cells(root) == 0;
            Assert.IsTrue(gone, "the last cell takes the block with it");
            if (f.Em.Exists(root) && f.Em.HasBuffer<BlockInstance>(root))
                foreach (var i in f.Em.GetBuffer<BlockInstance>(root)) Assert.IsTrue(i.Block == 0 || i.CellsAlive == 0, "no live instance remains");
        }

        static BlockInstance Instance(CombatFixture f, Entity root, ushort block)
        {
            foreach (var i in f.Em.GetBuffer<BlockInstance>(root)) if (i.Block == block) return i;
            Assert.Fail("the large block's instance is missing"); return default;
        }
    }
}
