using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Editor.Content;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;

namespace Game.Tests.EditMode.Editor.Content
{
    /// <summary>RACE-008 / RACE-013: the six approved racers are real, flyable blueprints: a core, engines that thrust and a
    /// gun each, Darts light and Rams heavy (the mass-against-knockback trade, SPEC-92). Darts fly sprint engines and Rams
    /// torches (ADR-0120).</summary>
    public sealed class RacerShipTests
    {
        static readonly string[] Darts = { "ship_racer_needle", "ship_racer_kite", "ship_racer_gimbal" };
        static readonly string[] Rams = { "ship_racer_battering_ram", "ship_racer_hammerhead" };

        static (int cores, int thrusters, int weapons, float massKg) Survey(ContentService service, BlueprintLibrary library, string id)
        {
            var definition = service.Catalog.Get<ShipBlueprintDefinition>(new ContentId(id));
            Assert.IsNotNull(definition, id);
            Assert.IsTrue(library.TryGet(definition.Id.Hash, out var ship), id);
            var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            using var blocks = new NativeList<StructureBlock>(Allocator.Temp);
            ship.GetBlocks(blocks);
            int cores = 0, thrusters = 0, weapons = 0;
            foreach (var b in blocks)
            {
                ref var def = ref service.BlockDefs.Value.Blocks[b.Placement.Block];
                if (def.IsCore) cores++;
                if (def.HasModule(BlockModuleId.Thruster)) thrusters++;
                if (def.HasModule(BlockModuleId.WeaponMount)) weapons++;
            }
            return (cores, thrusters, weapons, (float)ship.GetMass(content).Mass);
        }

        static int Count(ContentService service, BlueprintLibrary library, string id, params string[] blocks)
        {
            var wanted = new System.Collections.Generic.HashSet<ushort>();
            foreach (var block in blocks) wanted.Add(service.Catalog.GetBlockIndex(service.Catalog.Get<BlockDefinition>(new ContentId(block))));
            var definition = service.Catalog.Get<ShipBlueprintDefinition>(new ContentId(id));
            Assert.IsTrue(library.TryGet(definition.Id.Hash, out var ship), id);
            using var list = new NativeList<StructureBlock>(Allocator.Temp);
            ship.GetBlocks(list);
            int n = 0;
            foreach (var b in list) if (wanted.Contains(b.Placement.Block)) n++;
            return n;
        }

        [Test]
        public void Racers_FlyTheirLinesEngines()
        {
            using var service = new ContentService(ContentDatabaseBuilder.Rebuild());
            using var library = new BlueprintLibrary(service.Catalog, new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations });
            foreach (var id in Darts)
            {
                Assert.GreaterOrEqual(Count(service, library, id, "block_engine_sprint_small", "block_engine_sprint_medium"), 1, id + " sprint engines");
                Assert.AreEqual(0, Count(service, library, id, "block_engine_torch_large", "block_engine_torch_capital"), id + " carries no torch");
            }
            foreach (var id in Rams) Assert.GreaterOrEqual(Count(service, library, id, "block_engine_torch_large"), 2, id + " torches");
            foreach (var id in new[] { "ship_racer_needle", "ship_racer_kite", "ship_racer_battering_ram", "ship_racer_hammerhead" })
                Assert.GreaterOrEqual(Count(service, library, id, "block_thruster_vernier"), 4, id + " verniers");
        }

        [Test]
        public void Racers_HaveCoreEnginesAndGuns()
        {
            using var service = new ContentService(ContentDatabaseBuilder.Rebuild());
            using var library = new BlueprintLibrary(service.Catalog, new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations });
            foreach (var id in Darts)
            {
                var s = Survey(service, library, id);
                Assert.AreEqual(1, s.cores, id + " core");
                Assert.GreaterOrEqual(s.thrusters, 3, id + " thrusters");
                Assert.GreaterOrEqual(s.weapons, 1, id + " guns");
            }
            foreach (var id in Rams)
            {
                var s = Survey(service, library, id);
                Assert.AreEqual(1, s.cores, id + " core");
                Assert.GreaterOrEqual(s.thrusters, 3, id + " thrusters");
                Assert.GreaterOrEqual(s.weapons, 1, id + " guns");
            }
        }

        // The playground fits a shield through the ship editor, which refuses a ship it cannot validate (SPEC-51 §5.6).
        [Test]
        public void Racers_PassShipEditorValidation()
        {
            using var service = new ContentService(ContentDatabaseBuilder.Rebuild());
            using var library = new BlueprintLibrary(service.Catalog, new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations });
            var config = UnityEngine.ScriptableObject.CreateInstance<ShipEditorConfig>();
            var failures = new System.Text.StringBuilder();
            try
            {
                using var context = new EditorContext(config, service.Catalog, service.BlockDefs, service.Orientations, 16);
                foreach (var id in System.Linq.Enumerable.Concat(Darts, Rams))
                {
                    var definition = service.Catalog.Get<ShipBlueprintDefinition>(new ContentId(id));
                    Assert.IsTrue(library.TryGet(definition.Id.Hash, out var ship), id);
                    using var session = new EditSession(new ShipEditorEntryArgs { Source = ship, Options = new EditSessionOptions { Creative = true } }, context);
                    var issues = new System.Collections.Generic.List<EditIssue>();
                    session.GetIssues(issues);
                    foreach (var issue in issues) failures.AppendLine($"{id}: {issue.Kind} x{issue.Count}");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
            Assert.IsEmpty(failures.ToString());
        }

        [Test]
        public void Darts_AreLighterThanRams()
        {
            using var service = new ContentService(ContentDatabaseBuilder.Rebuild());
            using var library = new BlueprintLibrary(service.Catalog, new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations });
            float heaviestDart = 0, lightestRam = float.MaxValue;
            foreach (var id in Darts) heaviestDart = System.Math.Max(heaviestDart, Survey(service, library, id).massKg);
            foreach (var id in Rams) lightestRam = System.Math.Min(lightestRam, Survey(service, library, id).massKg);
            Assert.Less(heaviestDart, lightestRam, "every Dart is lighter than every Ram");
        }
    }
}
