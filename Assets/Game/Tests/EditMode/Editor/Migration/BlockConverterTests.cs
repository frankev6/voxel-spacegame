using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration;
using Game.Editor.Migration.Converters;
using Game.Editor.Migration.Dto;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>SPEC-72 MIG-006: independent SPEC-04 §4.10 oracle, export geometry, and the exact expected warnings.</summary>
    public sealed class BlockConverterTests
    {
        const string Root = "Assets/Game/Content/_MigrationTest";
        LegacyMigrationContext context;
        Dictionary<string, BlockDefinition> blocks;
        bool ownsRoot;
        // id | kind | shape | HP | function HP | mass | category | geometry | clearance | mesh | muzzle
        const string Rows = @"block_hull|Structural|Cube|100|0|25|Structure|1,1,1|||
block_hull_angled|Structural|Wedge|50|0|15|Structure|1,1,1|||
block_core_small|Functional|Cube|300|300|60|Core|1,1,1||hull_1x1_Cube.008|
block_core_cross|Functional|Cube|500|3500|60|Core|cross||cross_3x3_hull_cross_3x3_hull|
block_capacitor_small|Functional|Cube|100|400|35|Power|2,2,1||capacitor_2x1_base_1x1_hull.002|
block_shield_small|Functional|Cube|100|100|40|Shield|1,1,1||base_1x1_hull_base_1x1_hull|
block_thruster_small|Functional|Cube|100|200|30|Propulsion|1,1,2|0,0,2;0,0,3;0,0,4|thruster1x2_Cube.004|
block_thruster_medium|Functional|Cube|100|1000|30|Propulsion|thruster||thruster3x3x2.001_Cube.007|
block_thruster_large|Functional|Cube|100|400|30|Propulsion|2,1,2|0,0,2;1,0,2;0,0,3;1,0,3;0,0,4;1,0,4|base_2x2_thruster_base_2x2_thruster|
block_portal_small|Prop|Cube|150|0|30|Detail|1,1,1|0,0,2|base_1x1_hull_base_1x1_hull|
block_prismatic_mirror_small|Prop|Cube|100|0|30|Detail|1,1,1|0,0,1|base_1x1_hull_base_1x1_hull|
block_prismatic_mirror_fixed_small|Prop|Cube|100|0|30|Detail|1,1,1|0,0,1|base_1x1_hull_base_1x1_hull|
weapon_plasma_turret_small|Functional|Cube|100|200|30|Weapon|1,1,2|0,0,2;0,0,3;0,0,4|gun_turret_1x2_base_1x2_gun.001|0,0,1.5
weapon_plasma_turret_small_fixed|Functional|Cube|150|300|30|Weapon|1,1,2|0,0,2;0,0,3;0,0,4|gun_turret_1x2_base_1x2_gun.001|0,0,1.5
weapon_plasma_turret_medium|Functional|Cube|100|400|30|Weapon|2,1,2|0,0,3;1,0,3;0,0,4;1,0,4|gun_turret_2x1x3_Cube.005|0.5,0,1.5
weapon_small_beam_turret|Functional|Cube|100|200|30|Weapon|1,1,2|0,0,2;0,0,3;0,0,4|beam_turret_1x2_Cube.002|0,0,1.5
weapon_fixed_beam_turret_small|Functional|Cube|200|400|30|Weapon|1,1,2|0,0,2;0,0,3|beam_turret_1x2_Cube.002|0,0,1.5
weapon_railgun_medium|Functional|Cube|200|400|30|Weapon|1,1,2|0,0,2;0,0,3;0,0,4|beam_turret_1x2_Cube.002|0,0,1.5
weapon_ark_lance_medium|Functional|Cube|200|1200|30|Weapon|1,2,3|0,0,3;0,0,4|gun_turret_2x1x3_Cube.005|0,0.5,2.5
weapon_flak_cannon_medium|Functional|Cube|200|1800|30|Weapon|3,1,3|0,0,4;1,0,4;2,0,4|flak_cannon_3x3x3_Cube|1,0,2.5
weapon_missile_launcher_small|Functional|Cube|200|800|30|Weapon|2,1,2|0,0,3;1,0,3;0,0,4;1,0,4|missile_launcher_2x1_Cube.006|0.5,0,1.5";
        [OneTimeSetUp] public void ConvertExport()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root)); ownsRoot = true;
            var report = new ValidationReport("MIG"); string legacyRoot = Path.GetFullPath("Import/Legacy");
            context = new LegacyMigrationContext(LegacyContentIndex.Load(legacyRoot, report), LegacyIdMap.Default,
                new LegacyAssetWriter(Root, new MigrationManifest(), false, report), report, false, legacyRoot);
            new BlockConverter().Convert(context); Assert.IsFalse(report.HasErrors, report.ToText());
            blocks = context.Writer.Outcomes.ToDictionary(o => Path.GetFileNameWithoutExtension(o.AssetPath), o => AssetDatabase.LoadAssetAtPath<BlockDefinition>(o.AssetPath));
        }
        [OneTimeTearDown] public void Clean() { if (ownsRoot) AssetDatabase.DeleteAsset(Root); }
        [Test] public void RealExport_MatchesSpec04Table()
        {
            Assert.AreEqual(21, blocks.Count);
            foreach (string line in Rows.Split('\n'))
            {
                string[] e = line.Trim().Split('|'); var b = blocks[e[0]];
                Assert.AreEqual(Enum.Parse<BlockKind>(e[1]), b.Kind, e[0]); Assert.AreEqual(Enum.Parse<BlockShape>(e[2]), b.Shape, e[0]);
                Assert.AreEqual(int.Parse(e[3]), b.CellHp, e[0]); Assert.AreEqual(int.Parse(e[4]), b.FunctionHp, e[0]); Assert.AreEqual(int.Parse(e[5]), b.CellMass, e[0]);
                Assert.AreEqual(Enum.Parse<BlockCategory>(e[6]), b.BlockCategory, e[0]);
                CollectionAssert.AreEquivalent(Geometry(e[7]), b.Cells, e[0]); CollectionAssert.AreEquivalent(Coordinates(e[8]), b.Clearance, e[0]);
                bool horizontal = e[6] == "Propulsion" || e[6] == "Detail" || e[6] == "Weapon";
                Assert.AreEqual(horizontal ? 0xFFFFu : 0xFFFFFFu, b.AllowedOrientations.Bits, e[0]);
                Assert.AreEqual(horizontal ? 47 : 63, (int)b.AttachFaces, e[0]);
                Assert.AreSame(b, b.MirrorPartner); Assert.AreEqual(0, b.ArmorClass); Assert.AreEqual(0, b.MaterialLayer); Assert.AreEqual(999, b.MaxStack); Assert.AreEqual(ItemCategory.Block, b.Category);
                if (e[9].Length == 0) Assert.IsNull(b.PropMesh); else Assert.AreEqual(e[9], b.PropMesh.name, e[0]);
                CheckModules(b, e[10]);
                string path = AssetDatabase.GetAssetPath(b); var validation = new ValidationReport("Block");
                b.Validate(validation.For(path), new ContentValidationContext(path, true, _ => true)); Assert.IsFalse(validation.HasErrors, validation.ToText());
            }
            foreach (var file in context.Index.Files)
            {
                var dto = BlockConverter.Read(file, context.Report); if (dto == null) continue;
                Assert.IsTrue(context.IdMap.TryMap(LegacyIdKind.Block, dto.RegistryKey, out string id)); var b = blocks[id];
                Assert.AreEqual(dto.ItemName, b.DisplayName); Assert.AreEqual(dto.Description, b.Description); Assert.AreEqual(dto.BasePrice, b.BasePrice);
                Assert.AreEqual(dto.Rarity, b.Rarity.ToString()); Assert.AreEqual(dto.ItemImage.Name, b.Icon.name);
            }
        }
        static void CheckModules(BlockDefinition b, string muzzle)
        {
            BlockModuleId[] ids = b.BlockCategory switch {
                BlockCategory.Core => new[] { BlockModuleId.Core, BlockModuleId.PowerSource, BlockModuleId.PowerStorage },
                BlockCategory.Power => new[] { BlockModuleId.PowerStorage }, BlockCategory.Shield => new[] { BlockModuleId.ShieldEmitter },
                BlockCategory.Propulsion => new[] { BlockModuleId.Thruster }, BlockCategory.Weapon => new[] { BlockModuleId.WeaponMount }, _ => Array.Empty<BlockModuleId>() };
            CollectionAssert.AreEqual(ids, b.Modules.Select(m => m.Id));
            if (b.TryGetModule<CoreModule>(out var core)) { Assert.AreEqual(b.IdString == "block_core_small" ? 4000 : 8000, core.Params.MaxCells); Assert.AreEqual(b.IdString == "block_core_small" ? 100 : 150, core.Params.MaxExtentCells); }
            if (b.TryGetModule<PowerSourceModule>(out var source)) Assert.AreEqual(40, source.Params.Generation);
            if (b.TryGetModule<PowerStorageModule>(out var storage)) Assert.AreEqual(b.BlockCategory == BlockCategory.Core ? 200 : 40, storage.Params.Storage);
            if (b.TryGetModule<ThrusterModule>(out var thruster)) { Assert.AreEqual(b.IdString == "block_thruster_small" ? 20000 : 40000, thruster.Params.ThrustNewtons); Assert.AreEqual(0, thruster.Params.PowerDraw); }
            if (b.TryGetModule<ShieldEmitterModule>(out var shield)) { Assert.AreEqual(3000, shield.Params.Capacity); Assert.AreEqual(120, shield.Params.RegenPerSecond); Assert.AreEqual(4, shield.Params.RechargeDelaySeconds); Assert.AreEqual(8, shield.Params.PowerDraw); }
            if (b.TryGetModule<WeaponMountModule>(out var weapon)) { var f = muzzle.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); Assert.AreEqual(new float3(f[0], f[1], f[2]), weapon.MuzzleOffsetCells); }
        }
        [Test] public void Footprints_FromBlockSizeAndVoxelShape()
        {
            foreach (var file in context.Index.Files)
            {
                var dto = BlockConverter.Read(file, context.Report); if (dto == null) continue;
                context.IdMap.TryMap(LegacyIdKind.Block, dto.RegistryKey, out string id);
                var expected = dto.VoxelShape.Length == 0 ? Geometry(string.Join(",", dto.BlockSize)) : dto.VoxelShape.Select(c => new Vector3Int(c[0], c[1], c[2])).ToArray();
                CollectionAssert.AreEquivalent(expected, blocks[id].Cells, id);
            }
        }
        [Test] public void ClearanceDrops_Reported()
        {
            var drops = context.Report.Entries.Where(e => e.Code == "MIG-W033").ToArray(); Assert.AreEqual(3, drops.Length);
            Assert.AreEqual(1, drops.Count(e => e.AssetPath.EndsWith("weapon_railgun_medium.json", StringComparison.Ordinal)));
            Assert.AreEqual(2, drops.Count(e => e.AssetPath.EndsWith("weapon_ark_lance_medium.json", StringComparison.Ordinal)));
            var portal = context.Report.Entries.Single(e => e.Code == "MIG-W034"); StringAssert.EndsWith("block_portal_small.json", portal.AssetPath);
        }
        [Test] public void MeshBounds_Warnings()
        {
            CollectionAssert.AreEquivalent(new[] { "weapon_ark_lance_medium", "weapon_plasma_turret_medium" }, context.Report.Entries.Where(e => e.Code == "MIG-W032").Select(e => Path.GetFileNameWithoutExtension(e.AssetPath)));
        }
        static Vector3Int[] Coordinates(string text) => text.Length == 0 ? Array.Empty<Vector3Int>() : text.Split(';').Select(s => s.Split(',').Select(int.Parse).ToArray()).Select(v => new Vector3Int(v[0], v[1], v[2])).ToArray();
        static Vector3Int[] Geometry(string text)
        {
            if (text == "cross") return Coordinates("0,0,0;0,1,0;0,2,0;1,1,0;-1,1,0;0,1,1;0,1,-1");
            if (text == "thruster") return Coordinates("0,0,0;1,0,0;2,0,0;1,1,0;1,-1,0;0,0,1;1,0,1;2,0,1;1,1,1;1,-1,1");
            var d = text.Split(',').Select(int.Parse).ToArray(); var cells = new List<Vector3Int>();
            for (int x = 0; x < d[0]; x++) for (int y = 0; y < d[1]; y++) for (int z = 0; z < d[2]; z++) cells.Add(new Vector3Int(x, y, z)); return cells.ToArray();
        }
    }
}
