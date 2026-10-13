using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Editor.Content;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-010: canonical weapon/ability links, editor reference validation and content hashing (SPEC-04).</summary>
    public sealed class BlockLinksTests
    {
        [TearDown] public void TearDown() => DestroyAll();

        [Test]
        public void WeaponIndex_IsFilled()
        {
            var alpha = Create<WeaponDefinition>("wpn_alpha");
            var zeta = Create<WeaponDefinition>("wpn_zeta", so =>
            {
                Set(so, "damage", 12f); Set(so, "explosionDamage", 8f);
                Set(so, "pelletsPerRound", 3); Set(so, "burstCount", 2);
                Set(so, "fireRate", 4f); Set(so, "powerDraw", 7.5f);
            });
            var muzzle = new float3(.5f, 0f, 1.5f);
            var block = Block("weapon_test", BlockKind.Functional,
                modules: new BlockModule[] { new WeaponMountModule { Weapon = zeta, MuzzleOffsetCells = muzzle } });
            var defs = Definitions(block, zeta, alpha);
            var blob = BlockDefsBlobBuilderTests.Build(defs, out var catalog, out var modules);
            try
            {
                Assert.IsTrue(modules.Value.TryGet<WeaponMountParams>(BlockModuleId.WeaponMount, catalog.GetBlockIndex(block), out var row));
                Assert.AreEqual(2, row.WeaponIndex, "weapon indices follow catalog ID order, not input order");
                Assert.AreEqual(catalog.IndexOf<WeaponDefinition>(zeta), row.WeaponIndex);
                Assert.AreEqual(muzzle, row.MuzzleOffsetCells);
                Assert.IsTrue(block.TryGetModule<WeaponMountModule>(out var mount));
                var stats = new BlockStatContribution { SustainedDps = 10f, PowerDraw = 2f };
                mount.ContributeStats(ref stats, 0);
                Assert.AreEqual(394f, stats.SustainedDps);
                Assert.AreEqual(9.5f, stats.PowerDraw);

                Edit(block, so => Set(so, "modules", new BlockModule[]
                    { new WeaponMountModule { Weapon = alpha, MuzzleOffsetCells = muzzle } }));
                var changed = BlockDefsBlobBuilderTests.Build(defs, out _, out var changedModules);
                try
                {
                    Assert.IsTrue(changedModules.Value.TryGet<WeaponMountParams>(BlockModuleId.WeaponMount, catalog.GetBlockIndex(block), out var changedRow));
                    Assert.AreEqual(catalog.IndexOf<WeaponDefinition>(alpha), changedRow.WeaponIndex);
                    Assert.AreNotEqual(blob.Value.BlockDefsHash, changed.Value.BlockDefsHash, "the weapon index is hashed through the module row");
                }
                finally { changed.Dispose(); changedModules.Dispose(); }
            }
            finally { blob.Dispose(); modules.Dispose(); }
        }

        [Test]
        public void E047_WeaponLinkRules()
        {
            var block = Block("weapon_test", BlockKind.Functional, modules: new BlockModule[] { new WeaponMountModule() });
            var missing = Validate(block);
            Assert.AreEqual(1, missing.ErrorCount, missing.ToText());
            Assert.AreEqual("DATA-E047", missing.Entries[0].Code);
            Assert.AreEqual("modules[0].weapon", missing.Entries[0].Field);
            Assert.IsTrue(block.TryGetModule<WeaponMountModule>(out var emptyMount));
            var stats = new BlockStatContribution { SustainedDps = 10f, PowerDraw = 2f };
            emptyMount.ContributeStats(ref stats, 0);
            Assert.AreEqual(10f, stats.SustainedDps);
            Assert.AreEqual(2f, stats.PowerDraw);

            var weapon = Create<WeaponDefinition>("wpn_outside");
            var ability = Create<AbilityDefinition>("ability_outside");
            Edit(block, so => Set(so, "modules", new BlockModule[] { new WeaponMountModule { Weapon = weapon } }));
            SetAbilities(block, ability);
            var linked = Validate(block);
            Assert.AreEqual(0, linked.ErrorCount, linked.ToText());

            // Omit both linked definitions: the editor must inspect the nested module and the new array.
            var database = ScriptableObject.CreateInstance<ContentDatabase>();
            try
            {
                database.EditorSetDefinitions(Definitions(block));
                var report = ContentValidatorRunner.Validate(database);
                var references = report.Entries.Where(e => e.Code == "DATA-E010").ToArray();
                CollectionAssert.AreEquivalent(new[] { "modules[0].weapon", "grantedAbilities[0]" }, references.Select(e => e.Field).ToArray(), report.ToText());
                Assert.IsFalse(report.Entries.Any(e => e.Code == "DATA-E047"), report.ToText());
            }
            finally { Object.DestroyImmediate(database); }
        }

        [Test]
        public void AbilityIndices_PreserveOrder()
        {
            var alpha = Create<AbilityDefinition>("ability_alpha");
            var zeta = Create<AbilityDefinition>("ability_zeta");
            var block = Block("block_abilities", BlockKind.Functional);
            SetAbilities(block, zeta, alpha, zeta);
            CollectionAssert.AreEqual(new[] { zeta, alpha, zeta }, block.GrantedAbilities);
            var defs = Definitions(block, zeta, alpha);
            var blob = BlockDefsBlobBuilderTests.Build(defs, out var catalog, out var modules);
            try
            {
                Assert.AreEqual(0, blob.Value.Blocks[0].AbilityIndices.Length);
                Assert.AreEqual(0, blob.Value.Blocks[catalog.GetBlockIndex(catalog.MissingBlock)].AbilityIndices.Length);
                ref var baked = ref blob.Value.Blocks[catalog.GetBlockIndex(block)];
                Assert.AreEqual(3, baked.AbilityIndices.Length);
                Assert.AreEqual(catalog.IndexOf<AbilityDefinition>(zeta), baked.AbilityIndices[0]);
                Assert.AreEqual(catalog.IndexOf<AbilityDefinition>(alpha), baked.AbilityIndices[1]);
                Assert.AreEqual(catalog.IndexOf<AbilityDefinition>(zeta), baked.AbilityIndices[2]);
                Assert.AreEqual(2, baked.AbilityIndices[0], "do not sort or deduplicate authored grants");
                Assert.AreEqual(1, baked.AbilityIndices[1]);

                SetAbilities(block, alpha, zeta, zeta);
                var changed = BlockDefsBlobBuilderTests.Build(defs, out _, out var changedModules);
                try
                {
                    ref var reordered = ref changed.Value.Blocks[catalog.GetBlockIndex(block)];
                    Assert.AreEqual(catalog.IndexOf<AbilityDefinition>(alpha), reordered.AbilityIndices[0]);
                    Assert.AreEqual(catalog.IndexOf<AbilityDefinition>(zeta), reordered.AbilityIndices[1]);
                    Assert.AreNotEqual(blob.Value.BlockDefsHash, changed.Value.BlockDefsHash, "authored ability order is hashed");
                }
                finally { changed.Dispose(); changedModules.Dispose(); }
            }
            finally { blob.Dispose(); modules.Dispose(); }
        }

        static List<ContentDefinition> Definitions(BlockDefinition block, params ContentDefinition[] linked)
        {
            var defs = new List<ContentDefinition> { Block("block_missing", BlockKind.Structural), Item("item_missing"), block };
            defs.AddRange(linked);
            return defs;
        }

        static void SetAbilities(BlockDefinition block, params AbilityDefinition[] abilities)
            => Edit(block, so =>
            {
                var property = so.FindProperty("grantedAbilities");
                property.arraySize = abilities.Length;
                for (int i = 0; i < abilities.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = abilities[i];
            });
    }
}
