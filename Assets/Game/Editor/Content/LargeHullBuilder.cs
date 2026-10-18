using System.Collections.Generic;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>DATA-013 (ADR-0121, owner: "Yes, as real large blocks"): block_hull_large_2 and block_hull_large_3, cubes of hull material
    /// placed as one block; block_hull_large_4 for the Hollow's rock (DEV-023, owner: "2x2s and 3x3s or even 4x4s"). Same per-cell HP and mass as block_hull; achiral (self-mirrored); all 24 orientations.</summary>
    public static class LargeHullBuilder
    {
        const string Blocks = "Assets/Game/Content/Blocks/";

        [MenuItem("Game/Content/Build Large Hull Blocks")]
        public static void Build()
        {
            Make(2); Make(3); Make(4);
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the large hull blocks (ADR-0121).");
        }

        internal static void Make(int side)
        {
            string id = "block_hull_large_" + side, path = Blocks + id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + "block_hull.asset", path))
                throw new System.InvalidOperationException("copy block_hull");
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = $"Hull Block {side}×{side}×{side}";
            so.FindProperty("description").stringValue = $"{side * side * side} hull cells placed as one block: build big hulls fast. Shots still take it apart cell by cell.";
            so.FindProperty("formerIds").arraySize = 0;
            var cells = new List<Vector3Int>();
            for (int z = 0; z < side; z++) for (int y = 0; y < side; y++) for (int x = 0; x < side; x++) cells.Add(new Vector3Int(x, y, z));
            var p = so.FindProperty("cells"); p.arraySize = cells.Count;
            for (int i = 0; i < cells.Count; i++) p.GetArrayElementAtIndex(i).vector3IntValue = cells[i];
            so.FindProperty("shapes").arraySize = 0; so.FindProperty("shape").enumValueIndex = 0;
            so.FindProperty("allowedOrientations.Bits").intValue = (1 << 24) - 1;
            so.FindProperty("mirrorPartner").objectReferenceValue = null;
            int hullPrice = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(Blocks + "block_hull.asset")).FindProperty("basePrice").intValue;
            so.FindProperty("basePrice").intValue = hullPrice * side * side * side;   // from block_hull, so re-running is stable
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
    }
}
