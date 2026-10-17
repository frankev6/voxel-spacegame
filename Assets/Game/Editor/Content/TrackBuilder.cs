using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Hazards;
using Newtonsoft.Json.Linq;
using UnityEditor;
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
            AssetDatabase.SaveAssets();
            Track("track_01", boost, gate, wall, bounds);
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

        static Vector2 V(JToken t) => new Vector2((float)t[0], (float)t[1]);

        static void Track(string id, ZoneDefinition boost, ZoneDefinition gate, ZoneDefinition wall, ZoneDefinition bounds)
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
            EditorUtility.SetDirty(track);
            var errors = TrackValidator.Check(track);
            if (errors.Count > 0) throw new InvalidOperationException(id + ": " + string.Join("; ", errors));
            Debug.Log($"{id}: {track.Gates.Length} gates, {track.Zones.Length} zones, {track.StartGrid.Length} start slots");
        }
    }
}
