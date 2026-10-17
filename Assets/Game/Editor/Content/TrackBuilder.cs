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
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Accelerate, A = 200, B = 60, Flags = ZoneEffectFlags.AffectsShips });   // ships only: a strip flung its mines into the fences
            Zone("zone_slow_field", ZoneShape.Circle, new Vector2(80, 0), 0,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Drag, A = .7f, B = 20, Flags = ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices | ZoneEffectFlags.AffectsDebris });
            var gate = Zone("zone_gate", ZoneShape.Box, new Vector2(70, 2), 0,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Gate, A = 1, Flags = ZoneEffectFlags.AffectsShips });
            // Field wall: spring 400/s², damping 8/s, 2 HP of ram damage per m/s of approach (SPEC-91 §5.2).
            var wall = Zone("zone_field_wall", ZoneShape.Path, default, 3,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Repel, A = 400, B = 8, C = 2, Flags = ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices | ZoneEffectFlags.AffectsDebris });
            var bounds = Zone("zone_track_bounds", ZoneShape.Path, default, 1,
                new ZoneDefinition.Effect { Kind = ZoneEffectKind.Bounds, Flags = ZoneEffectFlags.AffectsShips });
            WreckBuilder.EnsureBlock(); TrackStructureBuilder.EnsureBlock(); EnsureMineCasing();
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
        /// <summary>RACE-016: mine casing: sturdy (a bolt is 60, a cell 700: about a dozen hits on one spot before it breaks and the
        /// charge cooks off) and light, so shots shove a mine around first.</summary>
        static void EnsureMineCasing()
        {
            string path = "Assets/Game/Content/Blocks/block_mine_casing.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset("Assets/Game/Content/Blocks/block_hull.asset", path))
                throw new InvalidOperationException("copy block_hull");
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = "block_mine_casing"; so.FindProperty("displayName").stringValue = "Mine Casing";
            so.FindProperty("description").stringValue = "A mine's armoured shell. Shots knock the mine around; enough of them crack it and set the charge off.";
            so.FindProperty("formerIds").arraySize = 0; so.FindProperty("cellHp").floatValue = 700; so.FindProperty("cellMass").floatValue = 6;
            so.FindProperty("tradeable").boolValue = false; so.FindProperty("basePrice").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }

        static MineDefinition Mine()
        {
            using var service = new Game.Data.Services.ContentService(ContentDatabaseBuilder.Rebuild());
            var catalog = service.Catalog; var content = new Game.Voxel.Model.VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            ushort casing = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_mine_casing")));
            using var structure = new Game.Voxel.Model.VoxelStructure(.5f);
            void Put(int x, int y, int z, byte colour) => structure.TryPlaceBlock(new Game.Voxel.Model.BlockPlacement { Block = casing, Origin = new Unity.Mathematics.int3(x, y, z), Color = colour }, content);
            // RACE-016: a 3.5 m sphere of mine casing (soot and graphite, a dark-red band round its waist) with six 1.5 m spikes:
            // big enough to hit, about 1.1 t so a bolt knocks it ~12 m/s.
            for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++) for (int z = -3; z <= 3; z++)
            {
                if (x * x + y * y + z * z > 11) continue;
                Put(x, y, z, (byte)(y == 0 ? 2 : (x + y + z & 1) == 0 ? 1 : 11));
            }
            foreach (var d in new[] { new Unity.Mathematics.int3(1, 0, 0), new Unity.Mathematics.int3(-1, 0, 0), new Unity.Mathematics.int3(0, 1, 0),
                new Unity.Mathematics.int3(0, -1, 0), new Unity.Mathematics.int3(0, 0, 1), new Unity.Mathematics.int3(0, 0, -1) })
                for (int k = 4; k <= 6; k++) Put(d.x * k, d.y * k, d.z * k, k == 6 ? (byte)14 : (byte)11);
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
            // RACE-016 (owner: "it should be impactful ... hurt and also send you flying"): 2500 at the centre over 28 m, and a push of
            // 900 kN·s (a 25 t racer at the centre leaves at ~36 m/s) through the same explosion path as every explosive.
            fuse.FindPropertyRelative("ArmSeconds").floatValue = 1.5f; fuse.FindPropertyRelative("TriggerRadius").floatValue = 14;
            fuse.FindPropertyRelative("FuseSeconds").floatValue = .5f; fuse.FindPropertyRelative("BlastRadius").floatValue = 28;
            fuse.FindPropertyRelative("BlastDamage").floatValue = 2500; fuse.FindPropertyRelative("MinTriggerMass").floatValue = 300;
            fuse.FindPropertyRelative("BlastImpulse").floatValue = 900000; fuse.FindPropertyRelative("CasingHp").floatValue = 600;   // ~10 bolts
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

        // RACE-016: the dressing's materials. HDR colours: bloom turns anything over 1 into light.
        static TrackLook Look() => new TrackLook
        {
            Curtain = Holo("gate", 0, new Color(1f, .62f, .2f) * 2.2f), Fence = Holo("fence", 1, new Color(.25f, .62f, 1f) * 2.0f),
            Pad = Holo("boost", 2, new Color(.35f, 1f, .5f) * 1.8f), Halo = Holo("halo", 3, new Color(1f, .22f, .15f) * 2.6f),
            Glow = Holo("glow", 4, Color.white * 3f), Ribbon = Holo("ribbon", 5, new Color(.3f, .7f, 1f) * 2.2f),
            Solid = Holo("light", 6, Color.white * 2.6f), Surface = Holo("lane", 7, new Color(.45f, .62f, 1f) * 1.1f),
            Sign = Holo("board", 8, new Color(1f, .58f, .15f) * 2.6f), Dust = Holo("dust", 9, new Color(.7f, .8f, 1f, .55f) * .8f),
            Metal = Metal(),
        };
        static Material Holo(string name, int mode, Color color)
        {
            var shader = Shader.Find("Game/Race/Holo") ?? throw new InvalidOperationException("Game/Race/Holo shader missing");
            string path = Racing + "mat_race_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader; mat.SetFloat("_Mode", mode); mat.SetColor("_Color", color); mat.renderQueue = 3000 + mode;
            EditorUtility.SetDirty(mat); return mat;
        }
        static Material Metal()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit missing");
            string path = Racing + "mat_race_metal.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader; mat.SetColor("_BaseColor", new Color(.30f, .32f, .36f)); mat.SetFloat("_Metallic", .85f); mat.SetFloat("_Smoothness", .62f);
            mat.enableInstancing = true; EditorUtility.SetDirty(mat); return mat;
        }

        static bool Inside(Vector2 p, List<Vector2> poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }
        static float RouteDistance(TrackPath path, Vector2 p)
        {
            var pts = path.Points; float best = float.MaxValue; int n = pts.Length;
            for (int i = 0; i < (path.Closed ? n : n - 1); i++)
            {
                Vector2 a = pts[i], ab = pts[(i + 1) % n] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + t * ab));
            }
            return best;
        }

        /// <summary>RACE-016: battle debris outside the fences, every 70–130 m of wall on the side away from every route, clear of wrecks
        /// and mine fields (a mine scattered into a fragment cooks off); plates that sit on the flight plane only well clear of it. Seeded: the same track every build.</summary>
        static int DebrisPieces(List<TrackPiece> pieces, Dictionary<string, ShipBlueprintDefinition> parts, List<List<Vector2>> walls, List<TrackPath> routes,
            Func<Vector2, bool> blocked)
        {
            var rng = new System.Random(16); int count = 0;
            float R() => (float)rng.NextDouble();
            bool Clear(Vector2 q, float margin)
            {
                if (blocked(q)) return false;
                foreach (var r in routes) if (RouteDistance(r, q) < r.Width * .5f + margin) return false;
                return true;
            }
            foreach (var pts in walls)
            {
                float next = R() * 60, along = 0;
                for (int i = 1; i < pts.Count && count < 180; i++)
                {
                    float seg = Vector2.Distance(pts[i - 1], pts[i]);
                    while (next <= along + seg && count < 180)
                    {
                        Vector2 q = Vector2.Lerp(pts[i - 1], pts[i], (next - along) / Mathf.Max(seg, 1e-3f));
                        next += 70 + R() * 60;
                        Vector2 d = (pts[i] - pts[i - 1]).normalized, normal = new Vector2(-d.y, d.x);
                        float side = Clear(q + normal * 30, 4) ? 1 : Clear(q - normal * 30, 4) ? -1 : 0;
                        if (side == 0) continue;
                        float out_ = 18 + R() * R() * 200;
                        Vector2 at = q + normal * side * out_;
                        if (!Clear(at, 12)) continue;
                        int kind = rng.Next(TrackStructureBuilder.DebrisKinds);
                        if (kind >= TrackStructureBuilder.DebrisKinds / 2 && out_ < 45) kind -= TrackStructureBuilder.DebrisKinds / 2;   // on-plane pieces stay well clear
                        float yaw = R() * Mathf.PI * 2;
                        pieces.Add(new TrackPiece { Blueprint = parts[TrackStructureBuilder.Debris + kind], Position = TrackStructureBuilder.Place(at, yaw, 2), Yaw = yaw, Kind = TrackPieceKind.Decor });
                        count++;
                    }
                    along += seg;
                }
            }
            return count;
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
            track.Look = Look();
            var routes = new List<TrackPath>();
            foreach (var kv in (JObject)json["paths"])
            {
                var r = (JObject)kv.Value; var pts = new List<Vector2>(); foreach (var q in (JArray)r["points"]) pts.Add(V(q));
                routes.Add(new TrackPath { Name = kv.Key, Points = pts.ToArray(), Width = (float)r["width"], Closed = (bool)r["closed"], Interior = (bool)r["interior"] });
            }
            track.Paths = routes.ToArray();

            // RACE-016: wreck outlines. A route inside a wreck (the carrier's hangar) has its hull as the barrier: no field walls, no
            // pylons there.
            var outlines = new List<List<Vector2>>();
            foreach (JObject w in (JArray)json["wrecks"]) { var o = new List<Vector2>(); foreach (var q in (JArray)w["outline"]) o.Add(V(q)); outlines.Add(o); }
            bool InWreck(Vector2 q) { foreach (var o in outlines) if (Inside(q, o)) return true; return false; }

            var gateArray = (JArray)json["gates"];
            var byId = new TrackGate[gateArray.Count];
            foreach (JObject g in gateArray)
            {
                var next = new List<int>(); foreach (var n in (JArray)g["next"]) next.Add((int)n);
                var p = V(g["position"]); float yaw = (float)g["yaw"];
                // Respawn 40 m past the gate, on the line, facing along it.
                var forward = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
                byId[(int)g["id"]] = new TrackGate { Position = p, Yaw = yaw, Width = (float)g["width"], Required = (bool)g["required"],
                    Next = next.ToArray(), RespawnPosition = p + forward * 40, RespawnYaw = yaw, Interior = InWreck(p) };
            }
            track.Gates = byId;

            // Gate pylons stand just outside each end of the gate line.
            var pylons = new List<(Vector2 centre, float yaw)>();
            foreach (var g in byId)
            {
                if (g.Interior) continue;
                var right = new Vector2(Mathf.Cos(g.Yaw), -Mathf.Sin(g.Yaw));
                pylons.Add((g.Position - right * (g.Width * .5f + 4.5f), g.Yaw)); pylons.Add((g.Position + right * (g.Width * .5f + 4.5f), g.Yaw));
            }

            // Field walls, split into spans between emitter posts about every 50 m. A post that would stand on a gate pylon is the
            // pylon: its head carries the field there.
            var zones = new List<TrackZone>(); var posts = new List<Vector2>(); var keptWalls = new List<List<Vector2>>();
            float radius = (float)json["wall_radius"];
            foreach (JArray w in (JArray)json["walls"])
            {
                var pts = new List<Vector2>(); foreach (var q in w) pts.Add(V(q));
                int inside = 0; foreach (var q in pts) if (InWreck(q)) inside++;
                if (inside * 2 > pts.Count) continue;
                keptWalls.Add(pts);
                var along = new float[pts.Count];
                for (int i = 1; i < pts.Count; i++) along[i] = along[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
                float length = along[pts.Count - 1]; int n = Mathf.Max(1, Mathf.RoundToInt(length / 50)); float step = length / n;
                Vector2 At(float d)
                {
                    for (int i = 1; i < pts.Count; i++) if (along[i] >= d) return Vector2.Lerp(pts[i - 1], pts[i], (d - along[i - 1]) / Mathf.Max(1e-4f, along[i] - along[i - 1]));
                    return pts[pts.Count - 1];
                }
                Vector2 Emitter(Vector2 q)
                {
                    foreach (var (c, _) in pylons) if (Vector2.Distance(c, q) < 9) return c;
                    posts.Add(q); return q;
                }
                var emitters = new Vector2[n + 1];
                for (int k = 0; k <= n; k++) emitters[k] = Emitter(At(k * step));
                int next = 1;
                for (int k = 0; k < n; k++)
                {
                    float d0 = k * step, d1 = (k + 1) * step;
                    var span = new List<Vector2> { At(d0) };
                    while (next < pts.Count - 1 && along[next] < d1) { if (along[next] > d0) span.Add(pts[next]); next++; }
                    span.Add(At(d1));
                    zones.Add(new TrackZone { Zone = wall, Path = span.ToArray(), PathRadius = radius, Emitters = true, EmitterA = emitters[k], EmitterB = emitters[k + 1] });
                }
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
            var pieces = new List<TrackPiece>(); var lights = new List<TrackLight>(); int total = 0;
            using (var service = new Game.Data.Services.ContentService(ContentDatabaseBuilder.Rebuild()))
            {
                var wrecks = (JArray)json["wrecks"];
                for (int i = 0; i < wrecks.Count && i < names.Length; i++)
                {
                    var w = (JObject)wrecks[i];
                    var (blueprint, position, cells) = WreckBuilder.Build(service, names[i], titles[i], w, (string)w["kind"] == "carrier" ? hangar : null, hangarHalf, lights);
                    pieces.Add(new TrackPiece { Blueprint = blueprint, Position = position, Yaw = 0, Kind = TrackPieceKind.Wall });
                    total += cells;
                }
                // RACE-016: the solid track parts.
                var parts = TrackStructureBuilder.Build(service, byId[0].Width);
                foreach (var q in posts) pieces.Add(new TrackPiece { Blueprint = parts[TrackStructureBuilder.Post], Position = TrackStructureBuilder.Place(q, 0), Kind = TrackPieceKind.Pylon });
                foreach (var (c, yaw) in pylons) pieces.Add(new TrackPiece { Blueprint = parts[TrackStructureBuilder.Pylon], Position = TrackStructureBuilder.Place(c, yaw), Yaw = yaw, Kind = TrackPieceKind.Pylon });
                pieces.Add(new TrackPiece { Blueprint = parts[TrackStructureBuilder.Gantry], Position = TrackStructureBuilder.Place(byId[0].Position, byId[0].Yaw), Yaw = byId[0].Yaw, Kind = TrackPieceKind.Wall });
                var minefields = new List<(Vector2 c, float r)>();
                foreach (JObject m in (JArray)json["mines"]) minefields.Add((V(m["c"]), (float)m["r"]));
                int debris = DebrisPieces(pieces, parts, keptWalls, routes, q => InWreck(q) || minefields.Exists(f => Vector2.Distance(q, f.c) < f.r + 30));
                Debug.Log($"{id}: {posts.Count} emitter posts, {pylons.Count} pylons, {debris} debris");
            }
            AssetDatabase.SaveAssets();
            track = AssetDatabase.LoadAssetAtPath<TrackDefinition>(path);   // reload after the database rebuild
            track.Pieces = pieces.ToArray(); track.Lights = lights.ToArray(); track.Look = Look(); track.Paths = routes.ToArray();

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
            Debug.Log($"{id}: {pieces.Count} pieces, {total} wreck cells");
            EditorUtility.SetDirty(track);
            var errors = TrackValidator.Check(track);
            if (errors.Count > 0) throw new InvalidOperationException(id + ": " + string.Join("; ", errors));
            Debug.Log($"{id}: {track.Gates.Length} gates, {track.Zones.Length} zones, {track.StartGrid.Length} start slots");
        }
    }
}
