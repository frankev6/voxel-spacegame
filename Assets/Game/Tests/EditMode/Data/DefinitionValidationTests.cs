using System;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Validation;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-004: ID rules and definition self-validation (SPEC-04 §4.3, §5.6 step 1, §5.8).</summary>
    public sealed class DefinitionValidationTests
    {
        [TearDown] public void TearDown() => DestroyAll();

        /// <summary>A test-only definition type with no [ContentIdPrefix] anywhere in its hierarchy.</summary>
        sealed class UnprefixedDefinition : ContentDefinition { }

        static string[] Errors(ValidationReport r) => r.Entries.Where(e => e.Severity == ValidationSeverity.Error).Select(e => e.Code).ToArray();
        static string[] Warnings(ValidationReport r) => r.Entries.Where(e => e.Severity == ValidationSeverity.Warning).Select(e => e.Code).ToArray();

        static void AssertOnly(string code, ContentDefinition d, string because)
        {
            var r = Validate(d);
            CollectionAssert.AreEqual(new[] { code }, Errors(r).Distinct().ToArray(), $"{because}: {r.ToText()}");
        }

        static void AssertClean(ContentDefinition d, string because)
        {
            var r = Validate(d);
            Assert.AreEqual(0, r.ErrorCount + r.WarningCount, $"{because}: {r.ToText()}");
        }

        static readonly Vector3Int[] Capacitor = { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0) };
        static readonly Vector3Int[] LShape = { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, 0, 1) };
        static readonly Vector3Int[] LShapeMirrored = { new Vector3Int(0, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 0, 1) };

        [Test]
        public void IdFormat_AcceptsAndRejects()
        {
            foreach (var ok in new[] { "block_hull", "weapon_railgun_medium", "paint_05_yellow", "faction_4", "a_b" })
                Assert.IsTrue(ContentIdRules.IsValidFormat(ok), ok);
            foreach (var bad in new[] { "PaintBucket_05_Yellow", "block-hull", "hull", "_block", "block__a", "block_", "4_block", "a_", "", null, "a_" + new string('b', 63) })
                Assert.IsFalse(ContentIdRules.IsValidFormat(bad), bad ?? "null");
            Assert.IsTrue(ContentIdRules.IsValidFormat("a_" + new string('b', 62)), "64 characters");
        }

        [Test]
        public void Prefix_BlockAndItemRules()
        {
            Assert.IsFalse(ContentIdRules.HasAllowedPrefix(typeof(BlockDefinition), "item_x", out var blockPrefixes), "block with an item ID");
            CollectionAssert.AreEqual(new[] { "block_", "weapon_" }, blockPrefixes);
            Assert.IsFalse(ContentIdRules.HasAllowedPrefix(typeof(ItemDefinition), "block_x", out var itemPrefixes), "item with a block ID");
            CollectionAssert.AreEqual(new[] { "item_", "resource_", "module_", "officer_", "paint_" }, itemPrefixes);
            Assert.IsTrue(ContentIdRules.HasAllowedPrefix(typeof(BlockDefinition), "weapon_railgun_medium", out _));
            Assert.IsTrue(ContentIdRules.HasAllowedPrefix(typeof(ItemDefinition), "paint_05_yellow", out _));
            Assert.IsTrue(ContentIdRules.HasAllowedPrefix(typeof(ShipBlueprintDefinition), "ship_scout", out _));
            Assert.IsFalse(ContentIdRules.HasAllowedPrefix(typeof(UnprefixedDefinition), "thing_x", out var none), "no prefix declared");
            Assert.AreEqual(0, none.Length);
            Assert.IsFalse(ContentIdRules.HasAllowedPrefix(typeof(BlockDefinition), "block_", out _), "the prefix alone is not an ID");
        }

        [Test]
        public void Item_RulesE020ToE024()
        {
            AssertClean(Item("item_scrap"), "valid item");
            AssertOnly("DATA-E020", Item("item_a", so => Set(so, "displayName", " ")), "empty display name");
            AssertOnly("DATA-E021", Item("item_b", so => Set(so, "maxStack", 0)), "max stack 0");
            AssertOnly("DATA-E022", Item("item_c", so => Set(so, "basePrice", -1)), "negative price");
            AssertOnly("DATA-E024", Item("item_d", so => Set(so, "category", (int)ItemCategory.Block)), "plain item with category Block");
            AssertOnly("DATA-E024", Block("block_e", BlockKind.Structural, edit: so => Set(so, "category", (int)ItemCategory.Misc)), "block without category Block");

            var noIcon = Validate(Item("item_f", so => Set(so, "icon", null)));
            Assert.AreEqual(0, noIcon.ErrorCount);
            CollectionAssert.AreEqual(new[] { "DATA-W023" }, Warnings(noIcon));
            AssertClean(Item("item_missing", so => Set(so, "icon", null)), "placeholders need no icon");
        }

        [Test]
        public void Block_FootprintRules()
        {
            AssertClean(Block("block_hull", BlockKind.Structural), "valid structural block");
            AssertClean(Block("block_capacitor_small", BlockKind.Functional, Capacitor), "valid 2x2x1 functional block");
            AssertOnly("DATA-E030", Block("block_a", BlockKind.Prop, new[] { new Vector3Int(1, 0, 0) }), "footprint without the root");
            AssertOnly("DATA-E030", Block("block_b", BlockKind.Prop, Array.Empty<Vector3Int>()), "empty footprint");
            AssertOnly("DATA-E031", Block("block_c", BlockKind.Prop, new[] { Vector3Int.zero, Vector3Int.zero }), "duplicate cell");
            AssertOnly("DATA-E032", Block("block_d", BlockKind.Prop, new[] { Vector3Int.zero, new Vector3Int(32, 0, 0), new Vector3Int(-32, 0, 0) }), "cell outside [-32, 31]");
            AssertOnly("DATA-E032", Block("block_e", BlockKind.Prop, edit: so => Set(so, "clearance", new[] { new Vector3Int(0, 0, -33) })), "clearance outside the range");
            AssertOnly("DATA-E033", Block("block_f", BlockKind.Prop, edit: so => Set(so, "clearance", new[] { Vector3Int.zero })), "clearance inside the footprint");
            AssertOnly("DATA-E033", Block("block_g", BlockKind.Prop, edit: so => Set(so, "clearance", new[] { new Vector3Int(0, 0, 1), new Vector3Int(0, 0, 1) })), "duplicate clearance");
            AssertClean(Block("block_h", BlockKind.Prop, edit: so => Set(so, "clearance", new[] { new Vector3Int(0, 0, 1) })), "valid clearance");
            AssertOnly("DATA-E034", Block("block_i", BlockKind.Structural, edit: so => Set(so, "allowedOrientations.Bits", 0u)), "no orientation");
            AssertOnly("DATA-E034", Block("block_j", BlockKind.Structural, edit: so => Set(so, "allowedOrientations.Bits", 1u << 24)), "bit above 23");
            AssertOnly("DATA-E036", Block("block_k", BlockKind.Structural, new[] { Vector3Int.zero, new Vector3Int(1, 0, 0) }), "two-cell structural block");
        }

        [Test]
        public void Block_KindFunctionRules()
        {
            AssertClean(Block("block_detail", BlockKind.Prop), "valid prop");
            AssertOnly("DATA-E035", Block("block_a", BlockKind.Structural, modules: new BlockModule[] { new CargoModule { Params = new CargoParams { Capacity = 1 } } }), "structural with a module");
            AssertOnly("DATA-E035", Block("block_b", BlockKind.Functional, modules: Array.Empty<BlockModule>()), "functional without modules");
            AssertOnly("DATA-E037", Block("block_c", BlockKind.Prop, edit: so => Set(so, "shape", (int)BlockShape.Wedge)), "prop that is not a cube");
            AssertClean(Block("block_d", BlockKind.Structural, edit: so => Set(so, "shape", (int)BlockShape.Wedge)), "structural blocks may use any shape");
            AssertOnly("DATA-E038", Block("block_e", BlockKind.Prop, edit: so => Set(so, "propMesh", null)), "prop without mesh");
            AssertOnly("DATA-E038", Block("block_f", BlockKind.Functional, edit: so => Set(so, "propMesh", null)), "functional without mesh");
            AssertOnly("DATA-E039", Block("block_g", BlockKind.Structural, edit: so => Set(so, "cellHp", 0f)), "cell HP 0");
            AssertOnly("DATA-E039", Block("block_h", BlockKind.Structural, edit: so => Set(so, "cellMass", -1f)), "negative mass");
            AssertOnly("DATA-E040", Block("block_i", BlockKind.Functional, edit: so => Set(so, "functionHp", 0f)), "functional with function HP 0");
            AssertOnly("DATA-E040", Block("block_j", BlockKind.Structural, edit: so => Set(so, "functionHp", 5f)), "structural with function HP");
            AssertOnly("DATA-E045", Block("block_k", BlockKind.Structural, edit: so => Set(so, "materialLayer", 64)), "material layer 64");
            AssertClean(Block("block_l", BlockKind.Structural, edit: so => Set(so, "materialLayer", 63)), "material layer 63");
            AssertOnly("DATA-E048", Block("block_m", BlockKind.Functional, modules: new BlockModule[]
                { new PowerSourceModule { Params = new PowerSourceParams { Generation = 1 } }, new PowerSourceModule { Params = new PowerSourceParams { Generation = 2 } } }), "two modules with one id");
            AssertOnly("DATA-E049", Block("block_n", BlockKind.Structural, edit: so => Set(so, "blockCategory", (int)BlockCategory.Power)), "structural with category Power");
            AssertOnly("DATA-E049", Block("block_o", BlockKind.Functional, edit: so => Set(so, "blockCategory", (int)BlockCategory.Detail)), "functional with category Detail");

            var b = Block("block_p", BlockKind.Functional, modules: new BlockModule[] { new ThrusterModule(), new CargoModule { Params = new CargoParams { Capacity = 5 } } });
            Assert.IsTrue(b.TryGetModule<CargoModule>(out var cargo)); Assert.AreEqual(5f, cargo.Params.Capacity);
            Assert.IsFalse(b.TryGetModule<CoreModule>(out var core)); Assert.IsNull(core);
        }

        [Test]
        public void Block_MirrorRules()
        {
            var d = BlockPlacementMath.ComputeMirrorOffset(Capacitor.Select(c => new int3(c.x, c.y, c.z)).ToArray(), Capacitor.Select(c => new int3(c.x, c.y, c.z)).ToArray(), out bool valid);
            Assert.IsTrue(valid); Assert.AreEqual(new int3(1, 0, 0), d);
            AssertClean(Block("block_capacitor", BlockKind.Prop, Capacitor), "the 2x2x1 box is X-symmetric");

            AssertOnly("DATA-E044", Block("block_l_alone", BlockKind.Prop, LShape), "chiral footprint without a partner");
            AssertOnly("DATA-E044", Block("block_faces", BlockKind.Prop, edit: so => Set(so, "attachFaces", (int)BlockFaceMask.PosX)), "attach faces not X-symmetric");
            AssertOnly("DATA-E044", Block("block_clear", BlockKind.Prop, edit: so => Set(so, "clearance", new[] { new Vector3Int(1, 0, 0) })), "clearance not X-symmetric");

            var left = Block("block_l_left", BlockKind.Prop, LShape);
            var right = Block("block_l_right", BlockKind.Prop, LShapeMirrored);
            Edit(left, so => Set(so, "mirrorPartner", right)); Edit(right, so => Set(so, "mirrorPartner", left));
            AssertClean(left, "correct partner (left)"); AssertClean(right, "correct partner (right)");

            var wrongA = Block("block_w_a", BlockKind.Prop, LShape);
            var wrongB = Block("block_w_b", BlockKind.Prop, LShape);
            Edit(wrongA, so => Set(so, "mirrorPartner", wrongB)); Edit(wrongB, so => Set(so, "mirrorPartner", wrongA));
            AssertOnly("DATA-E043", wrongA, "partner that is not the mirror");
            AssertOnly("DATA-E043", wrongB, "partner that is not the mirror (other side)");

            var lonely = Block("block_one_way", BlockKind.Prop, LShape);
            var other = Block("block_other", BlockKind.Prop);
            Edit(lonely, so => Set(so, "mirrorPartner", other));
            AssertOnly("DATA-E042", lonely, "partner that does not name this block back");
            AssertClean(other, "the other block is achiral and valid");

            var structural = Block("block_s", BlockKind.Structural);
            var prop = Block("block_q", BlockKind.Prop);
            Edit(structural, so => Set(so, "mirrorPartner", prop)); Edit(prop, so => Set(so, "mirrorPartner", structural));
            AssertOnly("DATA-E042", structural, "partner of a different kind");
        }

        [Test]
        public void Block_FunctionParamRanges()
        {
            var valid = new BlockModule[]
            {
                new CoreModule { Params = new CoreParams { MaxCells = 100, MaxExtentCells = 10 } },
                new PowerSourceModule { Params = new PowerSourceParams { Generation = 5 } },
                new PowerStorageModule { Params = new PowerStorageParams { Storage = 5 } },
                new ThrusterModule { Params = new ThrusterParams { ThrustNewtons = 1000, PowerDraw = 0 } },
                new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 10, RegenPerSecond = 0, RechargeDelaySeconds = 0, PowerDraw = 0 } },
                new CargoModule { Params = new CargoParams { Capacity = 10 } },
                new ExplosiveModule { Params = new ExplosiveParams { BlastRadiusCells = 2, BlastDamage = 50 } },
                new WeaponMountModule(),
            };
            AssertClean(Block("block_every_module", BlockKind.Functional, modules: valid), "every module in range");

            (BlockModule module, string field)[] bad =
            {
                (new CoreModule { Params = new CoreParams { MaxCells = 0, MaxExtentCells = 1 } }, "Params.MaxCells"),
                (new CoreModule { Params = new CoreParams { MaxCells = 1, MaxExtentCells = 0 } }, "Params.MaxExtentCells"),
                (new PowerSourceModule(), "Params.Generation"),
                (new PowerStorageModule { Params = new PowerStorageParams { Storage = float.NaN } }, "Params.Storage"),
                (new ThrusterModule { Params = new ThrusterParams { ThrustNewtons = 0, PowerDraw = 1 } }, "Params.ThrustNewtons"),
                (new ThrusterModule { Params = new ThrusterParams { ThrustNewtons = 1, PowerDraw = -1 } }, "Params.PowerDraw"),
                (new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 0 } }, "Params.Capacity"),
                (new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 1, RegenPerSecond = -1 } }, "Params.RegenPerSecond"),
                (new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 1, RechargeDelaySeconds = -1 } }, "Params.RechargeDelaySeconds"),
                (new ShieldEmitterModule { Params = new ShieldEmitterParams { Capacity = 1, PowerDraw = -1 } }, "Params.PowerDraw"),
                (new CargoModule(), "Params.Capacity"),
                (new ExplosiveModule { Params = new ExplosiveParams { BlastRadiusCells = 0, BlastDamage = 1 } }, "Params.BlastRadiusCells"),
                (new ExplosiveModule { Params = new ExplosiveParams { BlastRadiusCells = 1, BlastDamage = 0 } }, "Params.BlastDamage"),
            };
            for (int i = 0; i < bad.Length; i++)
            {
                var r = Validate(Block($"block_bad_{i}", BlockKind.Functional, modules: new[] { bad[i].module }));
                CollectionAssert.AreEqual(new[] { "DATA-E041" }, Errors(r), $"{bad[i].module.GetType().Name} {bad[i].field}: {r.ToText()}");
                Assert.AreEqual("modules[0]." + bad[i].field, r.Entries[0].Field);
                Assert.AreEqual($"test:block_bad_{i}", r.Entries[0].AssetPath);
            }
        }
    }
}
