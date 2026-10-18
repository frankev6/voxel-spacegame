using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>RACE-016 (owner: "the material things that can be collided with should be voxel ... fences should be destructible"):
    /// the track's solid parts as voxel blueprints at 1 m cells (debris at 2 m, like the wrecks). Field-wall emitter posts, gate
    /// pylons and drifting debris (no gantry over the finish: the camera collided with it). All are Track Frame (60 HP a cell, one plasma bolt) or hull: guns and rams break them. The energy between
    /// them (fences, curtains) and the guide markings stay light (TrackDressing).</summary>
    public static class TrackStructureBuilder
    {
        const string Ships = "Assets/Game/Content/Ships/", Blocks = "Assets/Game/Content/Blocks/";
        public const string Post = "ship_track_post", Pylon = "ship_track_pylon", Debris = "ship_track_debris_";
        public const int DebrisKinds = 8;
        /// <summary>Emitter head height (m) of a post: WallEmitterSystem probes here.</summary>
        public const float EmitterHeight = 9;
        // palette: 1 graphite, 5 gold, 11 soot, 13 steel blue, 14 bare metal, 16 light grey, 12 olive, 10 brown
        const byte Graphite = 1, Gold = 5, Soot = 11, Steel = 13, Bare = 14, Light = 16, Olive = 12, Brown = 10;

        internal static void EnsureBlock()
        {
            string path = Blocks + "block_track_frame.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + "block_hull.asset", path))
                throw new InvalidOperationException("copy block_hull");
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = "block_track_frame"; so.FindProperty("displayName").stringValue = "Track Frame";
            so.FindProperty("description").stringValue = "Race-track structure: emitter posts, pylons and gantries. Lighter than hull: a short burst or a ram breaks it.";
            so.FindProperty("formerIds").arraySize = 0; so.FindProperty("cellHp").floatValue = 60; so.FindProperty("cellMass").floatValue = 30;
            so.FindProperty("tradeable").boolValue = false; so.FindProperty("basePrice").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }

        /// <summary>The blueprints by id. Each 1 m-cell structure is centred on the column x, z ∈ [−r, r]; its centre sits at (+0.5, +0.5)
        /// from the grid origin, which Place() accounts for.</summary>
        internal static Dictionary<string, ShipBlueprintDefinition> Build(ContentService service)
        {
            var catalog = service.Catalog;
            var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            ushort frame = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_track_frame")));
            ushort hull = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            var result = new Dictionary<string, ShipBlueprintDefinition>();

            // Emitter post: a 3 m square shaft from 7 m below the flight plane to a steel emitter head at 8–10 m.
            result[Post] = Make(service, content, Post, "Field Emitter Post", 1, (put) =>
            {
                for (int y = -7; y <= 10; y++)
                    for (int x = -1; x <= 1; x++)
                        for (int z = -1; z <= 1; z++)
                        {
                            bool corner = math.abs(x) == 1 && math.abs(z) == 1;
                            if (y == 10 && corner) continue;
                            byte c = y <= -6 ? Soot : y >= 8 ? (y == 10 ? Bare : Steel) : y == 0 ? Light : y == -3 ? Gold : Graphite;
                            put(frame, x, y, z, c);
                        }
            });

            // Gate pylon: a stepped tower with cut corners, from 20 m below the plane to 28 m above; gold-and-soot hazard stripes at the
            // flight plane, light-grey bands where the gate's light rings sit.
            result[Pylon] = Make(service, content, Pylon, "Gate Pylon", 1, (put) =>
            {
                for (int y = -20; y <= 28; y++)
                {
                    int r = y <= -13 ? 4 : y <= 12 ? 3 : y <= 24 ? 2 : 1;
                    for (int x = -r; x <= r; x++)
                        for (int z = -r; z <= r; z++)
                        {
                            if (r > 1 && math.abs(x) == r && math.abs(z) == r) continue;
                            bool outer = math.abs(x) == r || math.abs(z) == r;
                            byte c = r >= 3 ? Graphite : r == 2 ? Steel : Bare;
                            if (y == -13 || y == 3 || y == 4 || y == 17 || y == 18) c = Light;
                            if (y >= -2 && y <= 0 && outer) c = ((x + z + y) & 2) == 0 ? Gold : Soot;
                            if (!outer && y > -19 && y < 27 && r > 1) c = Graphite;
                            put(frame, x, y, z, c);
                        }
                }
            });

            // Debris: torn hull plates with ribs, and girders, at 2 m cells. The first half drift 6–36 m below the flight plane; the
            // rest sit on it (placed far outside the fences).
            for (int k = 0; k < DebrisKinds; k++)
            {
                uint seed = (uint)(k * 7919 + 13);
                int depth = k < DebrisKinds / 2 ? -3 - (int)(Hash(k, 1, seed) % 15) : -1;
                bool girder = k % 4 == 3;
                byte paint = k % 2 == 0 ? Graphite : Olive;
                result[Debris + k] = Make(service, content, Debris + k, "Battle Debris", 2, (put) =>
                {
                    if (girder)
                    {
                        int len = 9 + (int)(Hash(k, 2, seed) % 8);
                        for (int x = -len; x <= len; x++) { put(hull, x, depth, 0, Bare); put(hull, x, depth + 1, 0, Graphite); if ((x & 3) == 0) { put(hull, x, depth, 1, Soot); put(hull, x, depth, -1, Soot); } }
                        return;
                    }
                    int rx = 3 + (int)(Hash(k, 3, seed) % 5), rz = 2 + (int)(Hash(k, 4, seed) % 4);
                    for (int x = -rx; x <= rx; x++)
                        for (int z = -rz; z <= rz; z++)
                        {
                            float e = math.lengthsq(new float2(x / (rx + .5f), z / (rz + .5f)));
                            float jag = (Hash(x, z, seed) % 100) / 100f * .5f;
                            if (e > 1 - jag * .6f) continue;
                            uint h = Hash(x, z, seed + 5);
                            byte c = h % 7 == 0 ? Soot : h % 11 == 0 ? Brown : paint;
                            int step = (x + rx) / 3 * (k % 3 == 1 ? 1 : 0);   // some plates are stepped, as if bent
                            put(hull, x, depth + step, z, c);
                            if ((x & 3) == 0 && h % 3 != 0) put(hull, x, depth + step + 1, z, Bare);   // ribs
                        }
                });
            }
            return result;
        }

        public const string Arm = "ship_track_arm", Door = "ship_track_door";

        /// <summary>HAZ-013: the moving structures (ADR-0119 decision 1) at 1 m cells, for the playground and later tracks.
        /// Arm: a 5 m hub column and a 40 m striped beam along +x, swept about the hub (the grid origin). Door: a 24 m × 12 m blast door
        /// slab, 2 m thick, centred on the grid origin across x.</summary>
        [MenuItem("Game/Content/Build Mechanism Props")]
        public static void BuildMechanismProps()
        {
            EnsureBlock(); AssetDatabase.SaveAssets();
            using (var service = new ContentService(ContentDatabaseBuilder.Rebuild()))
            {
                var catalog = service.Catalog;
                var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
                ushort frame = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_track_frame")));
                ushort hull = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_hull")));
                Make(service, content, Arm, "Sweeper Arm", 1, (put) =>
                {
                    for (int y = -6; y <= 6; y++) for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
                        if (!(math.abs(x) == 2 && math.abs(z) == 2)) put(hull, x, y, z, y == 6 ? Bare : Steel);
                    for (int x = 3; x <= 42; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    {
                        bool outer = y != 0 || z != 0;
                        byte c = x >= 39 ? Light : outer && ((x / 3) & 1) == 0 ? Gold : outer ? Soot : Graphite;
                        put(frame, x, y, z, c);
                    }
                });
                Make(service, content, Door, "Blast Door", 1, (put) =>
                {
                    for (int x = -12; x <= 11; x++) for (int y = -6; y <= 5; y++) for (int z = 0; z <= 1; z++)
                    {
                        bool edge = x <= -11 || x >= 10 || y <= -5 || y >= 4;
                        byte c = edge ? (((x + y) & 2) == 0 ? Gold : Soot) : (x & 3) == 0 ? Bare : Graphite;
                        put(hull, x, y, z, c);
                    }
                });
            }
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the mechanism props (HAZ-013).");
        }

        /// <summary>Track-space position for a 1 m-cell structure whose centre column should stand at `centre`, spawned at `yaw`.</summary>
        internal static Vector2 Place(Vector2 centre, float yaw, float cell = 1)
        {
            float h = cell * .5f, c = math.cos(yaw), s = math.sin(yaw);
            // quaternion.RotateY(yaw) takes local (x, z) to (x·cos + z·sin, −x·sin + z·cos)
            return centre - new Vector2(h * c + h * s, -h * s + h * c);
        }

        static uint Hash(int x, int y, uint seed) { uint h = (uint)x * 73856093u ^ (uint)y * 19349663u ^ seed * 83492791u; h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return h; }

        static ShipBlueprintDefinition Make(ContentService service, VoxelContent content, string id, string name, float cell, Action<Action<ushort, int, int, int, byte>> build)
        {
            using var structure = new VoxelStructure(cell);
            build((block, x, y, z, colour) => structure.TryPlaceBlock(new BlockPlacement { Block = block, Origin = new int3(x, y, z), Orient = 0, Color = colour }, content));
            NocturneShipBuilder.RemoveLoose(structure, content, 0);
            string path = Ships + id + ".asset";
            var blueprint = AssetDatabase.LoadAssetAtPath<ShipBlueprintDefinition>(path);
            if (blueprint == null) { blueprint = ScriptableObject.CreateInstance<ShipBlueprintDefinition>(); AssetDatabase.CreateAsset(blueprint, path); }
            var so = new SerializedObject(blueprint);
            so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = name;
            so.FindProperty("cellSize").floatValue = cell; so.ApplyModifiedPropertiesWithoutUndo();
            structure.TryGetCellBounds(out var min, out var max);
            blueprint.EditorSetPayload(VoxelStructureCodec.Encode(structure, VoxsBlockNames.FromCatalog(service.Catalog), VoxsEncodeOptions.ForAsset),
                structure.SolidCellCount, new Vector3Int(min.x, min.y, min.z), new Vector3Int(max.x, max.y, max.z));
            EditorUtility.SetDirty(blueprint);
            Debug.Log($"{id}: {structure.SolidCellCount} cells at {cell} m, bounds {min}..{max}");
            return blueprint;
        }
    }
}
