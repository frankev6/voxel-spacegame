using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>ABL-006 emergency repairs (owner, 2026-10-18: "fix a percentage of your lost/weakened ship at the heavy cost of energy
    /// and random hulls from your inventory if you're a player. it should prioritize fixing missing engines first"; defaults
    /// approved: granted by core blocks, 2 s channel, 35% of missing cells, 150 energy, 90 s cooldown, 4 cells per hull item).
    /// Makes ability_emergency_repair from the boost, grants it on every core block, and gives combat AI profiles a rule to use it
    /// below half hull.</summary>
    public static class EmergencyRepairContentBuilder
    {
        const string Abilities = "Assets/Game/Content/Abilities/", Blocks = "Assets/Game/Content/Blocks/", Ai = "Assets/Game/Content/Ai/";
        internal const string Id = "ability_emergency_repair";
        static readonly string[] CombatProfiles = { "ai_default", "ai_hollow_guard", "ai_skittish" };

        [MenuItem("Game/Content/Build Emergency Repair")]
        public static void Build()
        {
            string path = Abilities + Id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Abilities + "ability_speed_boost.asset", path))
                throw new System.InvalidOperationException("copy ability_speed_boost");
            var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path); var so = new SerializedObject(ability);
            so.FindProperty("id").stringValue = Id; so.FindProperty("formerIds").arraySize = 0;
            so.FindProperty("displayName").stringValue = "Emergency Repairs";
            so.FindProperty("description").stringValue = "Rebuilds 35% of the ship's missing structure over 2 s, engines first, and heals damage. Costs 150 energy and, for a pilot, hull plating from the hold (one per 4 cells).";
            so.FindProperty("kind").enumValueIndex = (int)AbilityKind.EmergencyRepair;
            so.FindProperty("energyCost").floatValue = 150; so.FindProperty("cooldownSeconds").floatValue = 90;
            so.FindProperty("maxCharges").intValue = 1; so.FindProperty("chargeRechargeSeconds").floatValue = 0;
            so.FindProperty("durationSeconds").floatValue = 2; so.FindProperty("statModifiers").arraySize = 0;
            so.FindProperty("shieldEnableThreshold").floatValue = 0;
            so.FindProperty("repairFraction").floatValue = .35f; so.FindProperty("healFraction").floatValue = .5f;
            so.FindProperty("hullCellsPerItem").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(ability);

            int granted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BlockDefinition", new[] { Blocks.TrimEnd('/') }))
            {
                var block = AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (block == null || !block.TryGetModule<CoreModule>(out _)) continue;
                var b = new SerializedObject(block); var list = b.FindProperty("grantedAbilities"); bool has = false;
                for (int i = 0; i < list.arraySize; i++) has |= list.GetArrayElementAtIndex(i).objectReferenceValue == ability;
                if (has) continue;
                list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = ability;
                b.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(block); granted++;
            }
            foreach (string profileId in CombatProfiles)
            {
                var profile = AssetDatabase.LoadMainAssetAtPath(Ai + profileId + ".asset");
                if (profile == null) { Debug.LogWarning("no AI profile " + profileId); continue; }
                var p = new SerializedObject(profile); var rules = p.FindProperty("abilityRules"); bool has = false;
                for (int i = 0; i < rules.arraySize; i++) has |= rules.GetArrayElementAtIndex(i).FindPropertyRelative("ability").objectReferenceValue == ability;
                if (has) continue;
                rules.arraySize++; var rule = rules.GetArrayElementAtIndex(rules.arraySize - 1);
                rule.FindPropertyRelative("ability").objectReferenceValue = ability;
                rule.FindPropertyRelative("trigger").enumValueIndex = (int)AiAbilityTrigger.HullBelow;
                rule.FindPropertyRelative("threshold").floatValue = .5f;
                p.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built " + Id + "; granted on " + granted + " more core blocks.");
        }
    }
}
