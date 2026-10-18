using Game.Core;
using Game.Data.Definitions;
using Game.Data.Hazards;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>ADR-0119 hazard content built from code (own work): the gravity well zone (HAZ-015). Re-running rebuilds each asset in place.</summary>
    public static class HazardContentBuilder
    {
        [MenuItem("Game/Content/Build Hazard Content")]
        public static void Build()
        {
            // A moon-sized well: GM 720 000 m³/s² gives a 60 m/s circular orbit at 200 m; softened inside 40 m; reach 450 m.
            TrackBuilder.Zone("zone_gravity_moon", ZoneShape.Circle, new Vector2(450, 0), 0,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Attract, A = 720000, B = 40,
                    Flags = ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices | ZoneEffectFlags.AffectsDebris });
            Switch();
            BlastDoorMechanism();
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the hazard content (ADR-0119).");
        }

        /// <summary>HAZ-014: shooting a switch of the group slams its door (a Toggle mover of the group) shut; 5 s later it reopens.</summary>
        static void BlastDoorMechanism()
        {
            const string path = "Assets/Game/Content/Hazards/mechanism_blast_door.asset";
            var def = AssetDatabase.LoadAssetAtPath<MechanismDefinition>(path);
            if (def == null) { def = ScriptableObject.CreateInstance<MechanismDefinition>(); AssetDatabase.CreateAsset(def, path); }
            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = "mechanism_blast_door";
            var rules = so.FindProperty("rules"); rules.arraySize = 1;
            var rule = rules.GetArrayElementAtIndex(0);
            rule.FindPropertyRelative("When").FindPropertyRelative("Kind").enumValueIndex = (int)MechanismDefinition.TriggerKind.SwitchDamaged;
            var d = rule.FindPropertyRelative("Do"); d.arraySize = 2;
            d.GetArrayElementAtIndex(0).FindPropertyRelative("Kind").enumValueIndex = (int)MechanismDefinition.ActionKind.MoverReverse;
            d.GetArrayElementAtIndex(0).FindPropertyRelative("Value").floatValue = -1;      // to the second point: shut
            d.GetArrayElementAtIndex(1).FindPropertyRelative("Kind").enumValueIndex = (int)MechanismDefinition.ActionKind.MoverReverse;
            d.GetArrayElementAtIndex(1).FindPropertyRelative("Value").floatValue = 1;       // back to the first point: open
            d.GetArrayElementAtIndex(1).FindPropertyRelative("DelaySeconds").floatValue = 5;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(def);
        }

        /// <summary>HAZ-014: the shootable switch (ADR-0119 decision 2). One cell, its function HP high so it takes many hits; any hit of
        /// 1 HP or more throws it. Built from the salvage magnet (a one-cell functional prop) with its module replaced.</summary>
        static void Switch()
        {
            const string Blocks = "Assets/Game/Content/Blocks/", path = Blocks + "block_switch.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + "block_salvage_magnet_small.asset", path))
                throw new System.InvalidOperationException("copy block_salvage_magnet_small");
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = "block_switch"; so.FindProperty("displayName").stringValue = "Switch";
            so.FindProperty("description").stringValue = "A shootable switch. Hitting it fires its mechanism: a blast door slams, a field turns off, a gate jams.";
            so.FindProperty("formerIds").arraySize = 0; so.FindProperty("cellHp").floatValue = 400; so.FindProperty("functionHp").floatValue = 5000;
            so.FindProperty("tradeable").boolValue = false; so.FindProperty("basePrice").intValue = 0;
            var modules = so.FindProperty("modules"); modules.arraySize = 1;
            modules.GetArrayElementAtIndex(0).managedReferenceValue = new Game.Data.Blocks.SwitchModule { Params = new Game.Data.Blocks.SwitchParams { TriggerDamage = 1 } };
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
    }
}
