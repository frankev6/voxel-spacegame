using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Simulation.Power;
using Game.Tests.Common;
using Game.Voxel.Data;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>The two required PWR-008 validator cases use real VOXS decode, multi-cell modules and warning-only reports.</summary>
    public sealed class PowerBudgetValidatorTests : GameTestBase
    {
        [TearDown] public void DestroyContent() => DestroyAll();

        [Test]
        public void HighDraw_Warns()
        {
            var weapon = Create<WeaponDefinition>("wpn_test", so =>
            { Set(so, "damage", 60f); Set(so, "projectileSpeed", 400f); Set(so, "powerDraw", 100f); });
            var source = Block("block_source", BlockKind.Functional, modules: new BlockModule[]
            {
                new PowerSourceModule { Params = new PowerSourceParams { Generation = 40 } },
                new PowerStorageModule { Params = new PowerStorageParams { Storage = 200 } },
            });
            var load = Block("block_load", BlockKind.Functional, new[] { Vector3Int.zero, Vector3Int.right },
                new BlockModule[] { new WeaponMountModule { Weapon = weapon } });
            var definitions = new List<ContentDefinition>
                { Block("block_missing", BlockKind.Structural), Item("item_missing"), source, load, weapon };
            using var service = ContentService.CreateForTests(definitions);
            var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            using var structure = new VoxelStructure();
            Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(new BlockPlacement
                { Block = service.Catalog.GetBlockIndex(source) }, content));
            Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(new BlockPlacement
                { Block = service.Catalog.GetBlockIndex(load), Origin = new int3(4, 0, 0) }, content));
            var blueprint = Create<ShipBlueprintDefinition>("ship_high_draw");
            structure.TryGetCellBounds(out var min, out var max);
            blueprint.EditorSetPayload(VoxelStructureCodec.Encode(structure, VoxsBlockNames.FromCatalog(service.Catalog), VoxsEncodeOptions.ForAsset),
                structure.SolidCellCount, new Vector3Int(min.x, min.y, min.z), new Vector3Int(max.x, max.y, max.z));
            definitions.Add(blueprint);
            var buildReport = new ValidationReport("budget fixture");
            var catalog = ContentCatalog.Build(definitions, buildReport);
            Assert.IsFalse(buildReport.HasErrors, buildReport.ToText());
            using var library = new BlueprintLibrary(catalog, content);
            Assert.IsTrue(library.TryGet(blueprint.Id.Hash, out var decoded));
            var budget = PowerBudgetValidation.Measure(decoded, catalog);
            Assert.AreEqual(40, budget.Generation); Assert.AreEqual(200, budget.Capacity);
            Assert.AreEqual(100, budget.WeaponDraw, "The two-cell weapon counts once.");
            Assert.AreEqual(200d / 60, budget.SustainSeconds, 1e-10);
            Assert.AreEqual(5, budget.RechargeSeconds);
            var report = Run(catalog);
            Assert.AreEqual(220, new PowerBudgetValidator().Order);
            Assert.AreEqual("PWR-W001", report.Entries.Single().Code);
            Assert.AreEqual(ValidationSeverity.Warning, report.Entries[0].Severity);
            Assert.AreEqual("content:ship_high_draw", report.Entries[0].AssetPath);
            Assert.AreEqual("payload", report.Entries[0].Field);
            StringAssert.Contains("ship_high_draw: sustain 3.333 s < 30 s (rule R1)", report.Entries[0].Message);
            var bootReport = new ValidationReport("boot");
            PowerBudgetValidation.Validate(catalog, library, bootReport);
            Assert.AreEqual(report.ToText(), bootReport.ToText(), "Editor and boot use the same warning rules.");

            // A mixed block contributes every module, still once per instance.
            Edit(load, so => Set(so, "modules", new BlockModule[]
            {
                new WeaponMountModule { Weapon = weapon },
                new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 3000, PowerDraw = 8 } },
                new ThrusterModule { Params = new ThrusterParams { ThrustNewtons = 20000, PowerDraw = 6 } },
            }));
            budget = PowerBudgetValidation.Measure(decoded, catalog);
            Assert.AreEqual(8, budget.ShieldDraw); Assert.AreEqual(6, budget.ThrusterDraw);
            Assert.AreEqual(200d / 74, budget.SustainSeconds, 1e-10);
            Assert.AreEqual(6.25, budget.RechargeSeconds);

            foreach (float shieldDraw in new[] { 40f, 60f })
            {
                Edit(load, so => Set(so, "modules", new BlockModule[]
                {
                    new WeaponMountModule { Weapon = weapon },
                    new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 3000, PowerDraw = shieldDraw } },
                }));
                Assert.IsTrue(double.IsPositiveInfinity(PowerBudgetValidation.Measure(decoded, catalog).RechargeSeconds));
                report = Run(catalog);
                CollectionAssert.AreEqual(new[] { "PWR-W001", "PWR-W002" }, report.Entries.Select(e => e.Code));
                Assert.IsTrue(report.Entries.All(e => e.Severity == ValidationSeverity.Warning));
                StringAssert.Contains("recharge infinite s > 10 s (rule R2)", report.Entries[1].Message);
            }

            Edit(load, so => Set(so, "modules", new BlockModule[] { new WeaponMountModule { Weapon = weapon } }));
            foreach (float capacity in new[] { 400f, 401f, 1799f, 1800f })
            {
                Edit(source, so => Set(so, "modules", new BlockModule[]
                {
                    new PowerSourceModule { Params = new PowerSourceParams { Generation = 40 } },
                    new PowerStorageModule { Params = new PowerStorageParams { Storage = capacity } },
                }));
                report = Run(catalog);
                Assert.AreEqual(capacity < 1800, report.Entries.Any(e => e.Code == "PWR-W001"), "Exactly 30 s meets R1.");
                Assert.AreEqual(capacity > 400, report.Entries.Any(e => e.Code == "PWR-W002"), "Exactly 10 s meets R2.");
            }
        }

        [Test]
        public void Fighter_NoWarning()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentDatabaseBuilder.DatabasePath);
            Assert.IsNotNull(database);
            var fighter = database.Definitions.OfType<ShipBlueprintDefinition>().Single(d => d.IdString == "ship_fighter");
            var definitions = database.Definitions.Where(d => !(d is ShipBlueprintDefinition) || d == fighter).ToArray();
            var buildReport = new ValidationReport("fighter");
            var catalog = ContentCatalog.Build(definitions, buildReport);
            Assert.IsFalse(buildReport.HasErrors, buildReport.ToText());
            var report = Run(catalog);
            Assert.IsEmpty(report.Entries, report.ToText());
            using var library = new BlueprintLibrary(catalog, default);
            Assert.IsTrue(library.TryGet(fighter.Id.Hash, out var structure));
            var budget = PowerBudgetValidation.Measure(structure, catalog);
            Assert.AreEqual(40, budget.Generation); Assert.AreEqual(200, budget.Capacity);
            Assert.AreEqual(36, budget.WeaponDraw); Assert.AreEqual(0, budget.ShieldDraw); Assert.AreEqual(0, budget.ThrusterDraw);
            Assert.IsTrue(double.IsPositiveInfinity(budget.SustainSeconds)); Assert.AreEqual(5, budget.RechargeSeconds);
        }

        static ValidationReport Run(ContentCatalog catalog)
        {
            var report = new ValidationReport("budget");
            new PowerBudgetValidator().Validate(catalog, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            return report;
        }
    }
}
