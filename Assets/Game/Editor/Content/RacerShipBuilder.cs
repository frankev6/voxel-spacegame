using Game.Core;
using Game.Data.Services;
using UnityEditor;

namespace Game.Editor.Content
{
    /// <summary>RACE-008 / RACE-013 (owner: "for ships i actually like all options you made, lets create all 6"). Builds the six
    /// racers from ArtSource/Racing/ships/*.json, which ArtSource/Racing/export_racers.py extrudes from the approved plan views
    /// in racer_silhouettes.py. The NOCTURNE builder's kit matching, modules and connectivity do the rest.</summary>
    public static class RacerShipBuilder
    {
        const string Source = "ArtSource/Racing/ships/";
        static readonly (string id, string name)[] Racers =
        {
            ("ship_racer_needle", "NEEDLE"), ("ship_racer_kite", "KITE"), ("ship_racer_gimbal", "GIMBAL"),
            ("ship_racer_battering_ram", "BATTERING RAM"), ("ship_racer_hammerhead", "HAMMERHEAD"),
        };

        [MenuItem("Game/Content/Build Racer Ships")]
        public static void Build()
        {
            EngineContentBuilder.Build();   // the racers fit the ADR-0120 engine line
            ImpactDriver();                  // the Rams' shove gun (RACE-013)
            using (var service = new ContentService(ContentDatabaseBuilder.Rebuild()))
                foreach (var (id, name) in Racers) NocturneShipBuilder.BuildShip(service, id, name, 1, Source);
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the six racer ships.");
        }

        /// <summary>RACE-013: wpn_impact_driver, a fixed kinetic slug that hurts little and shoves hard (SPEC-91 momentum: 400 kg at
        /// 600 m/s), on the block weapon_impact_driver.</summary>
        static void ImpactDriver()
        {
            const string weapons = "Assets/Game/Content/Weapons/", blocks = "Assets/Game/Content/Blocks/";
            string weaponPath = weapons + "wpn_impact_driver.asset", blockPath = blocks + "weapon_impact_driver.asset";
            if (AssetDatabase.LoadMainAssetAtPath(weaponPath) == null) AssetDatabase.CopyAsset(weapons + "wpn_plasma_turret_small_fixed.asset", weaponPath);
            var weapon = AssetDatabase.LoadMainAssetAtPath(weaponPath); var w = new SerializedObject(weapon);
            w.FindProperty("id").stringValue = "wpn_impact_driver"; w.FindProperty("displayName").stringValue = "Impact Driver";
            w.FindProperty("formerIds").arraySize = 0;
            w.FindProperty("damage").floatValue = 40; w.FindProperty("fireRate").floatValue = .5f; w.FindProperty("projectileSpeed").floatValue = 600;
            w.FindProperty("lifetimeMin").floatValue = 1.2f; w.FindProperty("lifetimeMax").floatValue = 1.2f; w.FindProperty("projectileMass").floatValue = 400;
            w.FindProperty("tracerColor").colorValue = new UnityEngine.Color(1, .45f, .15f, 1);
            w.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(weapon);
            if (AssetDatabase.LoadMainAssetAtPath(blockPath) == null) AssetDatabase.CopyAsset(blocks + "weapon_plasma_turret_small_fixed.asset", blockPath);
            var block = AssetDatabase.LoadMainAssetAtPath(blockPath); var b = new SerializedObject(block);
            b.FindProperty("id").stringValue = "weapon_impact_driver"; b.FindProperty("displayName").stringValue = "Impact Driver";
            b.FindProperty("description").stringValue = "A fixed slug launcher from the Khuchad yards. Little damage; it shoves whatever it hits.";
            b.FindProperty("formerIds").arraySize = 0;
            var modules = b.FindProperty("modules");
            for (int i = 0; i < modules.arraySize; i++)
            {
                var slot = modules.GetArrayElementAtIndex(i).FindPropertyRelative("weapon");
                if (slot != null) slot.objectReferenceValue = weapon;
            }
            b.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(block);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Batch entry: Unity -batchmode -executeMethod Game.Editor.Content.RacerShipBuilder.BuildAndQuit.</summary>
        public static void BuildAndQuit()
        {
            int code = 0;
            try { Build(); } catch (System.Exception e) { UnityEngine.Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
