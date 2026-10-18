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
            // HAZ-018 (ADR-0119 decision 6): the mine family on the proximity mine's body. Payload values: MinePayload.
            // Gravity: GM 18 000 pulls 80 m/s² at its 15 m softening radius, 20 m/s² at 30 m and 1.3 m/s² at its 120 m edge.
            Mine("mine_repulsor", true, ("Payload", 1), ("PayloadStrength", 1200000), ("BlastRadius", 32), ("BlastDamage", 0), ("BlastImpulse", 0));
            Mine("mine_emp", true, ("Payload", 2), ("PayloadSeconds", 3), ("BlastRadius", 30), ("BlastDamage", 0), ("BlastImpulse", 0));
            Mine("mine_seeker", false, ("SeekSpeed", 22), ("TriggerRadius", 8), ("BlastDamage", 1800), ("BlastRadius", 22), ("BlastImpulse", 600000));
            Mine("mine_gravity", true, ("Payload", 3), ("PayloadStrength", 18000), ("PayloadSeconds", 6), ("BlastRadius", 120), ("BlastDamage", 0), ("BlastImpulse", 0));
            CanisterBlock();
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            CanisterBlueprint();
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the hazard content (ADR-0119).");
        }

        /// <summary>A copy of mine_proximity with the given fuse fields changed (field name, value; enums by index).</summary>
        static void Mine(string id, bool anchored, params (string field, float value)[] changes)
        {
            const string Hazards = "Assets/Game/Content/Hazards/";
            string path = Hazards + id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Hazards + "mine_proximity.asset", path))
                throw new System.InvalidOperationException("copy mine_proximity");
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id; so.FindProperty("anchored").boolValue = anchored;
            var fuse = so.FindProperty("fuse");
            foreach (var (field, value) in changes)
            {
                var prop = fuse.FindPropertyRelative(field);
                if (prop.propertyType == SerializedPropertyType.Enum) prop.enumValueIndex = (int)value; else prop.floatValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
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

        /// <summary>HAZ-019 (ADR-0119 decision 7): the energy canister block, built from the small capacitor. Flying within 5 m of it
        /// grants 60 energy (four speed boosts); two plasma bolts break it and its charge goes off.</summary>
        static void CanisterBlock()
        {
            const string Blocks = "Assets/Game/Content/Blocks/", path = Blocks + "block_canister.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + "block_capacitor_small.asset", path))
                throw new System.InvalidOperationException("copy block_capacitor_small");
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = "block_canister"; so.FindProperty("displayName").stringValue = "Energy Canister";
            so.FindProperty("description").stringValue = "A charged energy cell. Fly through it to top up your power; shoot it and it blows.";
            so.FindProperty("formerIds").arraySize = 0; so.FindProperty("cellHp").floatValue = 60; so.FindProperty("functionHp").floatValue = 100;
            so.FindProperty("tradeable").boolValue = false; so.FindProperty("basePrice").intValue = 0;
            var modules = so.FindProperty("modules"); modules.arraySize = 2;
            modules.GetArrayElementAtIndex(0).managedReferenceValue = new Game.Data.Blocks.PickupModule { Params = new Game.Data.Blocks.PickupParams { Energy = 60, Reach = 5 } };
            modules.GetArrayElementAtIndex(1).managedReferenceValue = new Game.Data.Blocks.ExplosiveModule { Params = new Game.Data.Blocks.ExplosiveParams { BlastRadiusCells = 6, BlastDamage = 500 } };
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }

        /// <summary>HAZ-019: ship_device_canister, the canister block alone at 1 m cells (2 × 2 × 1 m).</summary>
        static void CanisterBlueprint()
        {
            using var service = new Game.Data.Services.ContentService(ContentDatabaseBuilder.Rebuild());
            var catalog = service.Catalog; var content = new Game.Voxel.Model.VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            ushort block = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_canister")));
            using var structure = new Game.Voxel.Model.VoxelStructure(1f);
            if (structure.TryPlaceBlock(new Game.Voxel.Model.BlockPlacement { Block = block, Origin = default, Color = 6 }, content) != Game.Voxel.Model.PlaceResult.Ok)
                throw new System.InvalidOperationException("place block_canister");
            const string bpPath = "Assets/Game/Content/Ships/ship_device_canister.asset";
            var blueprint = AssetDatabase.LoadAssetAtPath<ShipBlueprintDefinition>(bpPath);
            if (blueprint == null) { blueprint = ScriptableObject.CreateInstance<ShipBlueprintDefinition>(); AssetDatabase.CreateAsset(blueprint, bpPath); }
            var so = new SerializedObject(blueprint);
            so.FindProperty("id").stringValue = "ship_device_canister"; so.FindProperty("displayName").stringValue = "ENERGY CANISTER";
            so.FindProperty("cellSize").floatValue = 1f; so.ApplyModifiedPropertiesWithoutUndo();
            structure.TryGetCellBounds(out var min, out var max);
            blueprint.EditorSetPayload(Game.Voxel.Model.VoxelStructureCodec.Encode(structure, Game.Voxel.Data.VoxsBlockNames.FromCatalog(catalog), Game.Voxel.Data.VoxsEncodeOptions.ForAsset),
                structure.SolidCellCount, new Vector3Int(min.x, min.y, min.z), new Vector3Int(max.x, max.y, max.z));
            EditorUtility.SetDirty(blueprint);
        }
    }
}
