using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>RACE-012 (owner: "a group of derelict ships, like an ancient battle took place with large ships and we go through and
    /// around them"): the Battle Graveyard's wrecks as hollow 2 m-cell shells (ADR-0119 decision 10), from the hull outlines in
    /// track_01.json. Each is a deck over side walls reaching below the flight plane, with plates, superstructure, scorch and
    /// holes; the carrier's flight deck is an open trench along the hangar route so a racer in it stays in view from above.
    /// Wreck plate is tough: racers' guns scar it but cannot cut a capital hull in a race.</summary>
    public static class WreckBuilder
    {
        const string Ships = "Assets/Game/Content/Ships/", Blocks = "Assets/Game/Content/Blocks/";
        public const float Cell = 2f;
        const int Bottom = -8;   // cells: the walls reach 16 m below the flight plane

        // deck height (cells above the flight plane) and weathered paint per class (palette: 1 graphite, 10 brown, 11 soot, 12 olive drab,
        // 13 steel blue, 14 bare metal, 6 rust; never 17–19, the team slots). Two fleets: graphite and steel (dreadnought, cruisers),
        // olive and rust (the rest).
        static readonly Dictionary<string, (int deck, byte hull, byte hull2, byte plate, byte scorch)> Kinds = new()
        {
            { "dreadnought", (20, 1, 11, 13, 11) }, { "cruiser", (11, 1, 11, 13, 11) },
            { "carrier", (14, 12, 10, 6, 11) }, { "battleship", (17, 12, 10, 6, 11) }, { "frigate", (7, 12, 10, 6, 11) },
        };

        internal static void EnsureBlock()
        {
            string path = Blocks + "block_wreck_plate.asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + "block_hull.asset", path))
                throw new InvalidOperationException("copy block_hull");
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = "block_wreck_plate"; so.FindProperty("displayName").stringValue = "Wreck Plate";
            so.FindProperty("description").stringValue = "Capital-ship armour from an ancient battle. Guns scar it; they do not cut it.";
            so.FindProperty("formerIds").arraySize = 0; so.FindProperty("cellHp").floatValue = 4000; so.FindProperty("cellMass").floatValue = 40;
            so.FindProperty("tradeable").boolValue = false; so.FindProperty("basePrice").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }

        static float2 V(JToken t) => new float2((float)t[0], (float)t[1]);
        static bool Inside(float2 p, List<float2> poly)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }
        static float Dist(float2 p, List<float2> poly, bool closed)
        {
            float best = float.MaxValue;
            int n = poly.Count;
            for (int i = 0; i < (closed ? n : n - 1); i++)
            {
                float2 a = poly[i], b = poly[(i + 1) % n], ab = b - a;
                float t = math.saturate(math.dot(p - a, ab) / math.max(1e-6f, math.dot(ab, ab)));
                best = math.min(best, math.distance(p, a + t * ab));
            }
            return best;
        }
        static uint Hash(int x, int y, uint seed) { uint h = (uint)x * 73856093u ^ (uint)y * 19349663u ^ seed * 83492791u; h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return h; }

        /// <summary>Builds one wreck blueprint; returns it and the track-space position of its cell origin (spawn it there at yaw 0).</summary>
        internal static (ShipBlueprintDefinition blueprint, Vector2 position, int cells) Build(ContentService service, string id, string name, JObject wreck,
            List<float2> trench, float trenchHalfWidth)
        {
            var catalog = service.Catalog;
            var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            ushort plate = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_wreck_plate")));
            string kind = (string)wreck["kind"]; var (deck, hull, hull2, plateColour, scorch) = Kinds[kind]; byte trim = 14;
            var outline = new List<float2>(); foreach (var q in (JArray)wreck["outline"]) outline.Add(V(q));
            float2 lo = outline[0], hi = outline[0];
            foreach (var p in outline) { lo = math.min(lo, p); hi = math.max(hi, p); }
            float2 origin = math.floor(lo / Cell) * Cell;
            int nx = (int)math.ceil((hi.x - origin.x) / Cell) + 1, nz = (int)math.ceil((hi.y - origin.y) / Cell) + 1;
            uint seed = (uint)(int)wreck["seed"];
            float2 a = V(wreck["a"]), b = V(wreck["b"]); float length = math.distance(a, b); float2 axis = (b - a) / length;

            using var structure = new VoxelStructure(Cell);
            int placed = 0;
            void Put(int x, int y, int z, byte colour)
            {
                if (structure.TryPlaceBlock(new BlockPlacement { Block = plate, Origin = new int3(x, y, z), Orient = 0, Color = colour }, content) == PlaceResult.Ok) placed++;
            }
            for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    float2 p = origin + new float2(x + .5f, z + .5f) * Cell;
                    if (!Inside(p, outline)) continue;
                    float edge = Dist(p, outline, true);
                    bool trenchHere = trench != null && Dist(p, trench, false) < trenchHalfWidth;
                    if (trenchHere) continue;
                    bool trenchWall = trench != null && Dist(p, trench, false) < trenchHalfWidth + Cell * 1.5f;
                    float s = math.dot(p - a, axis) / length;   // 0 at the stern, 1 at the bow
                    uint h = Hash(x, z, seed);
                    // Deck height: the class's deck, lower toward the hull edge and the bow, raised superstructure mid-ships,
                    // plates one cell proud in 8-cell panels, and burned-through holes.
                    int top = deck;
                    if (edge < Cell * 6) top -= 2; if (edge < Cell * 3) top -= 2;
                    if (s > .85f) top -= (int)((s - .85f) / .15f * deck * .5f);
                    uint panel = Hash(x / 4, z / 4, seed + 7), big = Hash(x / 12, z / 12, seed + 3);
                    if (big % 4 == 0) top += 1;   // raised deck sections
                    bool tower = (kind == "dreadnought" && s > .30f && s < .42f || kind == "battleship" && s > .55f && s < .66f || kind == "cruiser" && s > .55f && s < .68f)
                                 && edge > Cell * 14 && math.abs(math.dot(p - a, new float2(-axis.y, axis.x))) < Cell * 10;
                    if (tower) top += kind == "dreadnought" ? 12 : 7;
                    bool hole = panel % 29 == 3 && edge > Cell * 5 && !tower;
                    bool wall = edge < Cell * 1.5f || trenchWall;
                    // Mostly hull, a second hull tone in large patches, sparse plates, scorch round the holes and in streaks.
                    byte deckColour = big % 3 == 1 ? hull2 : hull;
                    if (panel % 9 == 0) deckColour = plateColour;
                    if (h % 31 == 0 || panel % 29 == 4 || Hash(x / 2, z / 6, seed + 11) % 37 == 0) deckColour = scorch;
                    if (tower) deckColour = panel % 4 == 0 ? trim : hull;
                    if (wall)
                        for (int y = Bottom; y <= top; y++) Put(x, y, z, y == top ? deckColour : y % 5 == 0 ? scorch : hull);
                    else if (!hole)
                    {
                        Put(x, top, z, deckColour);
                        if (tower) for (int y = deck; y < top; y++) if (edge < Cell * 16 || (x + z) % 9 == 0) Put(x, y, z, trim);
                    }
                }

            string path = Ships + id + ".asset";
            var blueprint = AssetDatabase.LoadAssetAtPath<ShipBlueprintDefinition>(path);
            if (blueprint == null) { blueprint = ScriptableObject.CreateInstance<ShipBlueprintDefinition>(); AssetDatabase.CreateAsset(blueprint, path); }
            var so = new SerializedObject(blueprint);
            so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = name;
            so.FindProperty("cellSize").floatValue = Cell; so.ApplyModifiedPropertiesWithoutUndo();
            structure.TryGetCellBounds(out var min, out var max);
            blueprint.EditorSetPayload(VoxelStructureCodec.Encode(structure, VoxsBlockNames.FromCatalog(catalog), VoxsEncodeOptions.ForAsset),
                structure.SolidCellCount, new Vector3Int(min.x, min.y, min.z), new Vector3Int(max.x, max.y, max.z));
            EditorUtility.SetDirty(blueprint);
            Debug.Log($"{id}: {structure.SolidCellCount} cells at {Cell} m, {nx}×{nz} columns, bounds {min}..{max}");
            return (blueprint, new Vector2(origin.x, origin.y), structure.SolidCellCount);
        }
    }
}
