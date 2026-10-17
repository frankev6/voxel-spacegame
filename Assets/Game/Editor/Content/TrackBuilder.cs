using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Hazards;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>RACE-004 / HAZ-006: writes the zone definitions and track_01.asset from ArtSource/Racing/tracks/track_01.json, which
    /// export_track.py produces from the approved Battle Graveyard layout. Re-running rebuilds them in place.</summary>
    public static class TrackBuilder
    {
        const string Source = "ArtSource/Racing/tracks/", Hazards = "Assets/Game/Content/Hazards/", Racing = "Assets/Game/Content/Racing/";

        [MenuItem("Game/Content/Build Race Track")]
        public static void Build()
        {
            Directory.CreateDirectory(Hazards); Directory.CreateDirectory(Racing);
            var boost = Zone("zone_boost_strip", ZoneShape.Box, new Vector2(56, 270), 0,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Accelerate, A = 200, B = 60, Flags = ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices });
            Zone("zone_slow_field", ZoneShape.Circle, new Vector2(80, 0), 0,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Drag, A = .7f, B = 20, Flags = ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices | ZoneEffectFlags.AffectsDebris });
            var gate = Zone("zone_gate", ZoneShape.Box, new Vector2(70, 2), 0,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Gate, A = 1, Flags = ZoneEffectFlags.AffectsShips });
            // Field wall: spring 400/s², damping 8/s, 2 HP of ram damage per m/s of approach (SPEC-91 §5.2).
            var wall = Zone("zone_field_wall", ZoneShape.Path, default, 3,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Repel, A = 400, B = 8, C = 2, Flags = ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices | ZoneEffectFlags.AffectsDebris });
            var bounds = Zone("zone_track_bounds", ZoneShape.Path, default, 1,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Bounds, Flags = ZoneEffectFlags.AffectsShips });
            WreckBuilder.EnsureBlock();
            AssetDatabase.SaveAssets();
            var mine = Mine();
            Track("track_01", boost, gate, wall, bounds, mine);
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built track_01 and its zones (RACE-004).");
        }

        /// <summary>Batch entry: Unity -batchmode -executeMethod Game.Editor.Content.TrackBuilder.BuildAndQuit.</summary>
        public static void BuildAndQuit()
        {
            int code = 0;
            try { Build(); } catch (Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        static ZoneDefinition Zone(string id, ZoneShape shape, Vector2 half, float radius, params ZoneDefinition.Effect[] effects)
        {
            string path = Hazards + id + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(path);
            if (def == null) { def = ScriptableObject.CreateInstance<ZoneDefinition>(); AssetDatabase.CreateAsset(def, path); }
            var so = new SerializedObject(def);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("shape").enumValueIndex = (int)shape;
            so.FindProperty("halfExtents").vector2Value = half; so.FindProperty("pathRadius").floatValue = radius;
            var list = so.FindProperty("effects"); list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Kind").enumValueIndex = (int)effects[i].Kind;
                e.FindPropertyRelative("A").floatValue = effects[i].A; e.FindPropertyRelative("B").floatValue = effects[i].B;
                e.FindPropertyRelative("C").floatValue = effects[i].C; e.FindPropertyRelative("Id").intValue = effects[i].Id;
                e.FindPropertyRelative("Flags").intValue = (int)effects[i].Flags;
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>HAZ-008: a spiked proximity mine at 0.5 m cells (a 3-cell core, a spike on each face), its definition.</summary>
        static MineDefinition Mine()
        {
            using var service = new Game.Data.Services.ContentService(ContentDatabaseBuilder.Rebuild());
            var catalog = service.Catalog; var content = new Game.Voxel.Model.VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            ushort hull = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            using var structure = new Game.Voxel.Model.VoxelStructure(.5f);
            void Put(int x, int y, int z, byte colour) => structure.TryPlaceBlock(new Game.Voxel.Model.BlockPlacement { Block = hull, Origin = new Unity.Mathematics.int3(x, y, z), Color = colour }, content);
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++) Put(x, y, z, (byte)((x + y + z & 1) == 0 ? 2 : 11));
            foreach (var d in new[] { new Unity.Mathematics.int3(1, 0, 0), new Unity.Mathematics.int3(-1, 0, 0), new Unity.Mathematics.int3(0, 1, 0),
                new Unity.Mathematics.int3(0, -1, 0), new Unity.Mathematics.int3(0, 0, 1), new Unity.Mathematics.int3(0, 0, -1) })
                for (int k = 2; k <= 3; k++) Put(d.x * k, d.y * k, d.z * k, 11);
            string bpPath = "Assets/Game/Content/Ships/ship_device_mine_proximity.asset";
            var blueprint = AssetDatabase.LoadAssetAtPath<ShipBlueprintDefinition>(bpPath);
            if (blueprint == null) { blueprint = ScriptableObject.CreateInstance<ShipBlueprintDefinition>(); AssetDatabase.CreateAsset(blueprint, bpPath); }
            var so = new SerializedObject(blueprint);
            so.FindProperty("id").stringValue = "ship_device_mine_proximity"; so.FindProperty("displayName").stringValue = "PROXIMITY MINE";
            so.FindProperty("cellSize").floatValue = .5f; so.ApplyModifiedPropertiesWithoutUndo();
            structure.TryGetCellBounds(out var min, out var max);
            blueprint.EditorSetPayload(Game.Voxel.Model.VoxelStructureCodec.Encode(structure, Game.Voxel.Data.VoxsBlockNames.FromCatalog(catalog), Game.Voxel.Data.VoxsEncodeOptions.ForAsset),
                structure.SolidCellCount, new Vector3Int(min.x, min.y, min.z), new Vector3Int(max.x, max.y, max.z));
            EditorUtility.SetDirty(blueprint);
            string path = Hazards + "mine_proximity.asset";
            var mine = AssetDatabase.LoadAssetAtPath<MineDefinition>(path);
            if (mine == null) { mine = ScriptableObject.CreateInstance<MineDefinition>(); AssetDatabase.CreateAsset(mine, path); }
            var m = new SerializedObject(mine);
            m.FindProperty("id").stringValue = "mine_proximity"; m.FindProperty("blueprint").objectReferenceValue = blueprint;
            var fuse = m.FindProperty("fuse");
            fuse.FindPropertyRelative("ArmSeconds").floatValue = 1.5f; fuse.FindPropertyRelative("TriggerRadius").floatValue = 16;
            fuse.FindPropertyRelative("FuseSeconds").floatValue = .6f; fuse.FindPropertyRelative("BlastRadius").floatValue = 18;
            fuse.FindPropertyRelative("BlastDamage").floatValue = 450; fuse.FindPropertyRelative("MinTriggerMass").floatValue = 2000;
            m.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(mine);
            AssetDatabase.SaveAssets();
            return mine;
        }

        static MineFieldDefinition MineField(string id, MineDefinition mine, int count, float radius, float spacing)
        {
            string path = Hazards + id + ".asset";
            var field = AssetDatabase.LoadAssetAtPath<MineFieldDefinition>(path);
            if (field == null) { field = ScriptableObject.CreateInstance<MineFieldDefinition>(); AssetDatabase.CreateAsset(field, path); }
            var so = new SerializedObject(field);
            so.FindProperty("id").stringValue = id; so.FindProperty("mine").objectReferenceValue = mine; so.FindProperty("count").intValue = count;
            so.FindProperty("area").enumValueIndex = (int)ZoneShape.Circle; so.FindProperty("halfExtents").vector2Value = new Vector2(radius, radius);
            so.FindProperty("minSpacing").floatValue = spacing;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(field);
            return field;
        }

        static Vector2 V(JToken t) => new Vector2((float)t[0], (float)t[1]);

        static void Track(string id, ZoneDefinition boost, ZoneDefinition gate, ZoneDefinition wall, ZoneDefinition bounds, MineDefinition mine)
        {
            var json = JObject.Parse(File.ReadAllText(Source + id + ".json"));
            string path = Racing + id + ".asset";
            var track = AssetDatabase.LoadAssetAtPath<TrackDefinition>(path);
            if (track == null) { track = ScriptableObject.CreateInstance<TrackDefinition>(); AssetDatabase.CreateAsset(track, path); }
            var so = new SerializedObject(track); so.FindProperty("id").stringValue = id; so.ApplyModifiedPropertiesWithoutUndo();
            track.DisplayName = (string)json["name"]; track.DefaultLaps = (int)json["laps"];
            track.GateZone = gate; track.BoundsZone = bounds;

            var gateArray = (JArray)json["gates"];
            var byId = new TrackGate[gateArray.Count];
            foreach (JObject g in gateArray)
            {
                var next = new List<int>(); foreach (var n in (JArray)g["next"]) next.Add((int)n);
                var p = V(g["position"]); float yaw = (float)g["yaw"];
                // Respawn 40 m past the gate, on the line, facing along it.
                var forward = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
                byId[(int)g["id"]] = new TrackGate { Position = p, Yaw = yaw, Width = (float)g["width"], Required = (bool)g["required"],
                    Next = next.ToArray(), RespawnPosition = p + forward * 40, RespawnYaw = yaw };
            }
            track.Gates = byId;

            var zones = new List<TrackZone>();
            float radius = (float)json["wall_radius"];
            foreach (JArray w in (JArray)json["walls"])
            {
                var pts = new List<Vector2>(); foreach (var q in w) pts.Add(V(q));
                zones.Add(new TrackZone { Zone = wall, Path = pts.ToArray(), PathRadius = radius });
            }
            foreach (JObject b in (JArray)json["boost"])
                zones.Add(new TrackZone { Zone = boost, Position = V(b["center"]), Yaw = (float)b["yaw"],
                    HalfExtents = new Vector2((float)b["width"] / 2, (float)b["length"] / 2) });
            track.Zones = zones.ToArray();

            var slots = new List<TrackSlot>();
            foreach (JObject s in (JArray)json["start"]) slots.Add(new TrackSlot { Position = V(s["position"]), Yaw = (float)s["yaw"] });
            track.StartGrid = slots.ToArray();

            // RACE-012: the wrecks, as 2 m-cell hulls placed at their cell origins.
            var names = new[] { "ship_wreck_dreadnought_stern", "ship_wreck_dreadnought_bow", "ship_wreck_carrier", "ship_wreck_battleship", "ship_wreck_engine_section",
                "ship_wreck_cruiser", "ship_wreck_cruiser_burned", "ship_wreck_frigate_a", "ship_wreck_frigate_b", "ship_wreck_frigate_c" };
            var titles = new[] { "DREADNOUGHT (STERN)", "DREADNOUGHT (BOW)", "CARRIER", "BATTLESHIP", "ENGINE SECTION", "CRUISER", "BURNED-OUT CRUISER",
                "FRIGATE", "FRIGATE", "FRIGATE" };
            var hangar = new List<Unity.Mathematics.float2>();
            foreach (var q in (JArray)json["paths"]["hangar"]["points"]) hangar.Add(new Unity.Mathematics.float2((float)q[0], (float)q[1]));
            float hangarHalf = (float)json["paths"]["hangar"]["width"] / 2;
            var pieces = new List<TrackPiece>(); int total = 0;
            using (var service = new Game.Data.Services.ContentService(ContentDatabaseBuilder.Rebuild()))
            {
                var wrecks = (JArray)json["wrecks"];
                for (int i = 0; i < wrecks.Count && i < names.Length; i++)
                {
                    var w = (JObject)wrecks[i];
                    var (blueprint, position, cells) = WreckBuilder.Build(service, names[i], titles[i], w, (string)w["kind"] == "carrier" ? hangar : null, hangarHalf);
                    pieces.Add(new TrackPiece { Blueprint = blueprint, Position = position, Yaw = 0, Kind = TrackPieceKind.Wall });
                    total += cells;
                }
            }
            AssetDatabase.SaveAssets();
            track = AssetDatabase.LoadAssetAtPath<TrackDefinition>(path);   // reload after the database rebuild
            track.Pieces = pieces.ToArray();

            // HAZ-008: the battle's old minefields, never cleared (proximity mines; the EMP, seeker and repulsor payloads are HAZ-018).
            var fields = new List<TrackMines>(); int f = 0;
            foreach (JObject m in (JArray)json["mines"])
            {
                int n = (int)m["n"]; float r = (float)m["r"];
                var field = MineField($"minefield_track_01_{f}", mine, n, r, math.max(14, r / math.sqrt(n) * .9f));
                fields.Add(new TrackMines { Field = field, Position = V(m["c"]), Yaw = 0, Seed = (uint)(1000 + f) });
                f++;
            }
            track.MineFields = fields.ToArray();
            Debug.Log($"{id}: {pieces.Count} wrecks, {total} cells");
            EditorUtility.SetDirty(track);
            var errors = TrackValidator.Check(track);
            if (errors.Count > 0) throw new InvalidOperationException(id + ": " + string.Join("; ", errors));
            Debug.Log($"{id}: {track.Gates.Length} gates, {track.Zones.Length} zones, {track.StartGrid.Length} start slots");
        }
    }
}
