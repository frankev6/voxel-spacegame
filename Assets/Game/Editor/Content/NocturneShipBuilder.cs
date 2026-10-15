using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Blocks;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>QA-001 (owner: "make a new frigate that aligns with the new versions we've been working on, and make a larger
    /// cruise one based on the designs we planed for nocture, i want to see how we can handle bigger ships"). Builds game
    /// blueprints from the look book's NOCTURNE designs as exported by ArtSource/Bricks/export_lookbook_ships.py:
    /// WRAITH (frigate, rebuilt on the slope kit) and REQUIEM (heavy cruiser, ~115 m, 120k cells).
    /// Every cut cell was snapped to the look book's kit k3, which is the game's ADR-0018 kit, and is matched here against the
    /// game's own shapes in all 24 orientations by sampled occupancy; long-slope halves pair into real 1x2 long slopes.
    /// Look-book modules (engines, guns, bridge) become game blocks seated on the module box's open face, with the rest of the
    /// box closed in dark trim. Look-book axes (x right, y forward, z up) map to game (-x, up y, forward z), keeping handedness.</summary>
    public static class NocturneShipBuilder
    {
        const string Source = "ArtSource/Bricks/ships/", Ships = "Assets/Game/Content/Ships/";
        const int Samples = 6, Words = (Samples * Samples * Samples + 63) / 64, MaxError = 40;   // 40 of 216 samples
        // Team roles resolve in the brick shader (0 primary, 18 secondary, 19 accent); 11 gunmetal and 27 dark iron are paints.
        static readonly Dictionary<string, byte> TagColour = new() { { "hull", 0 }, { "hull2", 18 }, { "accent", 19 }, { "armor", 11 }, { "trim", 27 } };

        [MenuItem("Game/Content/Build NOCTURNE Ships")]
        public static void Build()
        {
            using var service = new ContentService(ContentDatabaseBuilder.Rebuild());
            BuildShip(service, "ship_nocturne_wraith", "WRAITH", 0);
            // Owner: "lets design a ship that looks really good, and "borrows" slightly from the requiem instead of copying
            // outright, and feels good". Three cruiser options drawn on the kit (ArtSource/LookDev/ld_cruisers.py).
            // Owner, picking: "the C lance is good as the new requiem, but i also liked your other options so just add them all
            // as ships." REQUIEM is the spinal lance cruiser, VESPER the nave line cruiser, DIRGE the broadside cruiser.
            BuildShip(service, "ship_nocturne_requiem", "REQUIEM", 2);
            BuildShip(service, "ship_nocturne_vesper", "VESPER", 2);
            BuildShip(service, "ship_nocturne_dirge", "DIRGE", 2);
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built NOCTURNE ships (WRAITH, REQUIEM).");
        }

        // ------------------------------------------------------------------------------------------------- shape matching
        struct Bits
        {
            public ulong W0, W1, W2, W3;
            public void Set(int i) { if (i < 64) W0 |= 1UL << i; else if (i < 128) W1 |= 1UL << (i - 64); else if (i < 192) W2 |= 1UL << (i - 128); else W3 |= 1UL << (i - 192); }
            public static int Diff(in Bits a, in Bits b) => math.countbits(a.W0 ^ b.W0) + math.countbits(a.W1 ^ b.W1) + math.countbits(a.W2 ^ b.W2) + math.countbits(a.W3 ^ b.W3);
            public int Count => math.countbits(W0) + math.countbits(W1) + math.countbits(W2) + math.countbits(W3);
        }
        static float3 Sample(int i) => (new float3(i % Samples, i / Samples % Samples, i / (Samples * Samples)) + .5f) / Samples;
        static Bits Occupancy(Func<float3, bool> inside)
        {
            var bits = new Bits();
            for (int i = 0; i < Samples * Samples * Samples; i++) if (inside(Sample(i))) bits.Set(i);
            return bits;
        }
        sealed class Kit
        {
            public readonly Bits[] Wedge = new Bits[24], Hip = new Bits[24], High = new Bits[24], Low = new Bits[24];
            public readonly int3[] LowOffset = new int3[24];
            public Kit(ref OrientationTablesBlob tables)
            {
                for (int o = 0; o < 24; o++)
                {
                    var inverse = math.inverse(tables.Rotations[o]);
                    int3 up = tables.Rotate(new int3(0, 1, 0), (byte)o);
                    if (!math.all(up == (int3)math.round(math.rotate(tables.Rotations[o], new float3(0, 1, 0)))))
                        throw new InvalidOperationException("Orientation table rotation and cell rotation disagree at " + o);
                    LowOffset[o] = up;
                    float3 Canon(float3 p, int3 offset) => math.rotate(inverse, p - .5f + offset) + .5f;
                    Wedge[o] = Occupancy(p => { var q = Canon(p, 0); return q.y + q.z <= 1; });
                    Hip[o] = Occupancy(p => { var q = Canon(p, 0); return q.y + q.z <= 1 && q.x + q.z <= 1; });
                    High[o] = Occupancy(p => { var q = Canon(p, 0); return q.y + 2 * q.z <= 2; });
                    Low[o] = Occupancy(p => { var q = Canon(p, up); return q.y - 1 + 2 * q.z <= 1; });
                }
            }
        }

        sealed class Target { public int3 Cell; public byte Colour; public Bits Bits; public bool Done; }

        // ---------------------------------------------------------------------------------------------------------- build
        static void BuildShip(ContentService service, string id, string name, int reactors)
        {
            var catalog = service.Catalog;
            var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            ushort Index(string block) => catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId(block)));
            var kit = new Kit(ref service.Orientations.Value);
            var json = JObject.Parse(File.ReadAllText(Source + id + ".json"));

            // Look-book cell (x, y, z) -> game cell (-x - 1, z, y), recentred: centred along the keel, keel bottom at 0.
            int3 lo = int.MaxValue, hi = int.MinValue;
            foreach (JArray c in (JArray)json["cells"]) { var p = new int3((int)c[0], (int)c[1], (int)c[2]); lo = math.min(lo, p); hi = math.max(hi, p); }
            int3 shift = new int3(0, -lo.z, -(lo.y + hi.y) / 2);
            int3 Map(int x, int y, int z) => new int3(-x - 1, z, y) + shift;

            var targets = new Dictionary<int3, Target>();
            var full = new List<(int3 cell, byte colour)>();
            foreach (JArray c in (JArray)json["cells"])
            {
                var cell = Map((int)c[0], (int)c[1], (int)c[2]);
                byte colour = TagColour.TryGetValue((string)c[3], out var k) ? k : (byte)0;
                if (c[4].Type == JTokenType.Null) { full.Add((cell, colour)); continue; }
                // Cell-local look-book half-spaces n . p <= d become game-local ones: n' = (-n.x, n.z, n.y), d' = d - n.x.
                var planes = new List<(float3 n, float d)>();
                foreach (JArray pl in (JArray)c[4])
                {
                    var n = (JArray)pl[0]; float nx = (float)n[0], ny = (float)n[1], nz = (float)n[2], d = (float)pl[1];
                    planes.Add((new float3(-nx, nz, ny), d - nx));
                }
                var bits = Occupancy(p => { foreach (var (n, d) in planes) if (math.dot(n, p) > d + 1e-5f) return false; return true; });
                targets[cell] = new Target { Cell = cell, Colour = colour, Bits = bits };
            }

            using var structure = new VoxelStructure();
            ushort cube = Index("block_hull"), wedge = Index("block_hull_angled"), hip = Index("block_hull_hip"), slope = Index("block_hull_slope_long");
            int failed = 0;
            void Put(ushort block, int3 at, byte orient, byte colour)
            {
                if (structure.TryPlaceBlock(new BlockPlacement { Block = block, Origin = at, Orient = orient, Color = colour }, content) != PlaceResult.Ok) failed++;
            }
            foreach (var (cell, colour) in full) Put(cube, cell, 0, colour);

            // Long slopes first: a cell whose best fit is one half pairs with the neighbour holding the other half.
            int longSlopes = 0, singles = 0, cubes = 0, dropped = 0;
            var ordered = new List<Target>(targets.Values);
            ordered.Sort((a, b) => a.Cell.z != b.Cell.z ? a.Cell.z.CompareTo(b.Cell.z) : a.Cell.y != b.Cell.y ? a.Cell.y.CompareTo(b.Cell.y) : a.Cell.x.CompareTo(b.Cell.x));
            foreach (var t in ordered)
            {
                if (t.Done) continue;
                int bestSingle = int.MaxValue;
                for (int o = 0; o < 24; o++) bestSingle = math.min(bestSingle, math.min(Bits.Diff(t.Bits, kit.Wedge[o]), Bits.Diff(t.Bits, kit.Hip[o])));
                int bestPair = int.MaxValue; int3 root = 0; byte pairOrient = 0; Target partner = null;
                for (int o = 0; o < 24; o++)
                {
                    // t as the High root
                    if (targets.TryGetValue(t.Cell + kit.LowOffset[o], out var low) && !low.Done)
                    {
                        int e = Bits.Diff(t.Bits, kit.High[o]) + Bits.Diff(low.Bits, kit.Low[o]);
                        if (e < bestPair) { bestPair = e; root = t.Cell; pairOrient = (byte)o; partner = low; }
                    }
                    // t as the Low half
                    if (targets.TryGetValue(t.Cell - kit.LowOffset[o], out var high) && !high.Done)
                    {
                        int e = Bits.Diff(high.Bits, kit.High[o]) + Bits.Diff(t.Bits, kit.Low[o]);
                        if (e < bestPair) { bestPair = e; root = high.Cell; pairOrient = (byte)o; partner = high; }
                    }
                }
                if (partner != null && bestPair <= 2 * MaxError && bestPair < 2 * bestSingle)
                {
                    Put(slope, root, pairOrient, t.Colour); t.Done = partner.Done = true; longSlopes++;
                }
            }
            foreach (var t in ordered)
            {
                if (t.Done) continue;
                t.Done = true;
                int best = int.MaxValue; ushort block = cube; byte orient = 0;
                for (int o = 0; o < 24; o++)
                {
                    int w = Bits.Diff(t.Bits, kit.Wedge[o]), h = Bits.Diff(t.Bits, kit.Hip[o]);
                    if (w < best) { best = w; block = wedge; orient = (byte)o; }
                    if (h < best) { best = h; block = hip; orient = (byte)o; }
                }
                if (best <= MaxError) { Put(block, t.Cell, orient, t.Colour); singles++; }
                else if (t.Bits.Count * 2 > Samples * Samples * Samples) { Put(cube, t.Cell, 0, t.Colour); cubes++; }
                else dropped++;
            }

            // Modules: the game block sits on the box face open to space, facing out; the rest of the box is dark trim.
            int modules = 0;
            foreach (JArray m in (JArray)json["mods"])
            {
                string kind = (string)m[0]; var mn = (JArray)m[1]; var rsz = (JArray)m[4];
                int3 a = Map((int)mn[0], (int)mn[1], (int)mn[2]), b = Map((int)mn[0] + (int)rsz[0] - 1, (int)mn[1] + (int)rsz[1] - 1, (int)mn[2] + (int)rsz[2] - 1);
                int3 boxMin = math.min(a, b), boxMax = math.max(a, b);
                modules += PlaceModule(structure, content, catalog, kind, boxMin, boxMax, ref failed);
            }
            if (reactors > 0) failed += SeatCoreAndReactors(structure, content, Index("block_core_small"), Index("block_reactor_small"), reactors);
            int hollowed = reactors > 0 ? Hollow(structure, content, cube, 10) : 0;
            var (bridges, loose) = Connect(structure, content, cube, Index("block_core_small"));

            string path = Ships + id + ".asset";
            var blueprint = AssetDatabase.LoadAssetAtPath<ShipBlueprintDefinition>(path);
            if (blueprint == null) { blueprint = ScriptableObject.CreateInstance<ShipBlueprintDefinition>(); AssetDatabase.CreateAsset(blueprint, path); }
            var so = new SerializedObject(blueprint);
            so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = name;
            so.FindProperty("cellSize").floatValue = .5f; so.ApplyModifiedPropertiesWithoutUndo();
            structure.TryGetCellBounds(out var min, out var max);
            blueprint.EditorSetPayload(VoxelStructureCodec.Encode(structure, VoxsBlockNames.FromCatalog(catalog), VoxsEncodeOptions.ForAsset),
                structure.SolidCellCount, new Vector3Int(min.x, min.y, min.z), new Vector3Int(max.x, max.y, max.z));
            EditorUtility.SetDirty(blueprint);
            Debug.Log($"{id}: {structure.SolidCellCount} cells, bounds {min}..{max}; {full.Count} cubes, {longSlopes} long slopes, {singles} wedges/hips, " +
                $"{cubes} cut cells kept as cubes, {dropped} slivers dropped, {modules} module blocks, {failed} placements refused, " +
                $"{bridges} bridge cubes, {loose} loose cells removed, {hollowed} interior cells hollowed; mass {structure.GetMass(content).Mass:0} kg");
        }

        // ---------------------------------------------------------------------------------------------------- connectivity
        // QA-001 (owner: "when i ran "playground ship=ship_nocturne_requiem", it doesn't work, only worked with the wraith"):
        // a ship is one piece only through shared faces (SPEC-51 §5.5), and the look book's slope snapping left parts of REQUIEM
        // touching the hull at an edge or corner, so the editor refused its loadout commit. A part meeting the core's piece
        // at an edge or corner gets one hull cube in its colour on a shared face; anything still apart is removed.
        static int3[] Faces => Directions;   // declared below; a field initialiser here would run first and read null
        static (int bridges, int loose) Connect(VoxelStructure structure, VoxelContent content, ushort cube, ushort core)
        {
            int bridges = 0;
            for (int pass = 0; pass < 16; pass++)
            {
                var main = MainPiece(structure, core, out var all);
                if (main.Count == all.Count) return (bridges, 0);
                var handled = new HashSet<int3>(main); var solid = new HashSet<int3>(all);
                int joined = 0;
                foreach (var cell in all)
                {
                    if (handled.Contains(cell)) continue;
                    // One bridge per separate part and pass: an edge (two axes) or corner (three axes) neighbour in the core's
                    // piece, joined through an empty cell one axis step away. A corner contact becomes an edge one next pass.
                    bool done = false;
                    for (int dz = -1; dz <= 1 && !done; dz++) for (int dy = -1; dy <= 1 && !done; dy++) for (int dx = -1; dx <= 1 && !done; dx++)
                    {
                        var d = new int3(dx, dy, dz);
                        if (math.csum(math.abs(d)) < 2 || !main.Contains(cell + d)) continue;
                        structure.TryGetBlockAt(cell, out var owner, out _);
                        foreach (var step in Faces)
                        {
                            if (math.any(step != 0 & step != d) || !structure.GetCell(cell + step).IsAir) continue;
                            if (structure.TryPlaceBlock(new BlockPlacement { Block = cube, Origin = cell + step, Orient = 0, Color = owner.Color }, content) != PlaceResult.Ok) continue;
                            bridges++; joined++; done = true; break;
                        }
                    }
                    if (done) Flood(cell, solid, handled);
                }
                if (joined == 0) break;
            }
            // Parts with no edge or corner contact are removed.
            return (bridges, RemoveLoose(structure, content, core));
        }
        /// <summary>Removes every block with a cell outside the piece face-connected to a `core` block (or, without one, the
        /// largest piece); returns the cells removed. Also used by XenoContentBuilder.</summary>
        internal static int RemoveLoose(VoxelStructure structure, VoxelContent content, ushort core)
        {
            var keep = MainPiece(structure, core, out var cells);
            int before = structure.SolidCellCount;
            foreach (var cell in cells)
                if (!keep.Contains(cell) && !structure.GetCell(cell).IsAir) structure.TryRemoveBlockAt(cell, content, out _);
            return before - structure.SolidCellCount;
        }
        static void Flood(int3 from, HashSet<int3> set, HashSet<int3> handled)
        {
            var stack = new Stack<int3>(); stack.Push(from); handled.Add(from);
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                foreach (var f in Faces) if (set.Contains(c + f) && handled.Add(c + f)) stack.Push(c + f);
            }
        }
        // Face-connected cells reachable from the core (or, without one, the largest piece); `all` holds every solid cell.
        static HashSet<int3> MainPiece(VoxelStructure structure, ushort core, out List<int3> all)
        {
            all = new List<int3>(); var solid = new HashSet<int3>();
            if (!structure.TryGetCellBounds(out var min, out var max)) return solid;
            int3 seed = int.MinValue;
            for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
            {
                var c = new int3(x, y, z); if (structure.GetCell(c).IsAir) continue;
                all.Add(c); solid.Add(c);
                if (seed.x == int.MinValue && structure.TryGetBlockAt(c, out var b, out _) && b.Block == core) seed = c;
            }
            HashSet<int3> Fill(int3 from, HashSet<int3> unvisited)
            {
                var piece = new HashSet<int3> { from }; var stack = new Stack<int3>(); stack.Push(from); unvisited.Remove(from);
                while (stack.Count > 0)
                {
                    var c = stack.Pop();
                    foreach (var f in Faces) if (unvisited.Remove(c + f)) { piece.Add(c + f); stack.Push(c + f); }
                }
                return piece;
            }
            if (seed.x != int.MinValue) return Fill(seed, solid);
            HashSet<int3> best = new();
            while (solid.Count > 0) { var e = solid.GetEnumerator(); e.MoveNext(); var piece = Fill(e.Current, solid); if (piece.Count > best.Count) best = piece; }
            return best;
        }

        // -------------------------------------------------------------------------------------------------------- modules
        static readonly Dictionary<string, string> ModuleBlock = new()
        {
            { "thruster_s", "block_thruster_small" }, { "thruster_l", "block_thruster_medium" }, { "thruster_xl", "block_thruster_large" }, { "rcs", "block_thruster_large" },
            { "gatling", "weapon_plasma_turret_small" }, { "autocannon", "weapon_plasma_turret_small" }, { "heavy_cannon", "weapon_plasma_turret_medium" },
            { "flak", "weapon_flak_cannon_medium" }, { "railgun", "weapon_railgun_medium" }, { "nova", "weapon_missile_launcher_small" },
            { "storm", "weapon_ark_lance_medium" }, { "siege", "weapon_siege_cannon_heavy" }, { "core_1", "block_core_small" }
        };
        static readonly int3[] Directions = { new(0, 1, 0), new(0, -1, 0), new(1, 0, 0), new(-1, 0, 0), new(0, 0, 1), new(0, 0, -1) };

        static int PlaceModule(VoxelStructure structure, VoxelContent content, ContentCatalog catalog, string kind, int3 boxMin, int3 boxMax, ref int failed)
        {
            ref var tables = ref content.Orientations.Value;
            bool engine = kind.StartsWith("thruster", StringComparison.Ordinal), rcs = kind == "rcs";
            // The open face: most empty cells just outside it (engines always fire aft, -z).
            int3 face = new int3(0, 0, -1);
            if (!engine)
            {
                int bestOpen = -1;
                foreach (var d in Directions)
                {
                    int open = 0;
                    for (int z = boxMin.z; z <= boxMax.z; z++) for (int y = boxMin.y; y <= boxMax.y; y++) for (int x = boxMin.x; x <= boxMax.x; x++)
                    {
                        var c = new int3(x, y, z);
                        bool onFace = math.all(d == 0 | (d > 0 & c == boxMax) | (d < 0 & c == boxMin));
                        if (onFace && structure.GetCell(c + d).IsAir) open++;
                    }
                    if (open > bestOpen) { bestOpen = open; face = d; }
                }
            }
            int placed = 0;
            if (ModuleBlock.TryGetValue(kind, out var blockId))
            {
                ushort block = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId(blockId)));
                ref var def = ref content.Blocks.Value.Blocks[block];
                // Engines use the fighter's orient 8 (thrust forward, nozzle aft); other blocks point their up axis out of the face.
                byte orient = 0; int bestScore = int.MinValue;
                for (int o = 0; o < 24; o++)
                {
                    int3 up = tables.Rotate(new int3(0, 1, 0), (byte)o), fwd = tables.Rotate(new int3(0, 0, 1), (byte)o);
                    // A manoeuvring thruster's nozzle (its +z) points out of the open face; its thrust pushes the hull the other way.
                    int score = engine ? (math.all(fwd == new int3(0, 0, -1)) ? 2 : 0) + (math.all(up == new int3(0, 1, 0)) ? 1 : 0)
                        : rcs ? (math.all(fwd == face) ? 4 : 0) + (math.all(up == new int3(0, 1, 0)) ? 1 : 0)
                        : (math.all(up == face) ? 4 : 0) + (math.all(fwd == new int3(0, 0, 1)) ? 2 : math.all(fwd == new int3(0, 1, 0)) ? 1 : 0);
                    if (score > bestScore) { bestScore = score; orient = (byte)o; }
                }
                int3 fpMin = int.MaxValue, fpMax = int.MinValue;
                for (int i = 0; i < def.Cells.Length; i++) { var r = tables.Rotate(def.Cells[i], orient); fpMin = math.min(fpMin, r); fpMax = math.max(fpMax, r); }
                int3 size = fpMax - fpMin + 1, box = boxMax - boxMin + 1;
                // Tile engines and manoeuvring pods across their face; one block for everything else, centred on the face.
                int3 count = engine || rcs ? math.max(1, box / size) : 1;
                int3 axis = math.abs(face);
                count = math.select(count, 1, axis == 1);
                int3 used = count * size, start = boxMin + (box - used) / 2;
                start = math.select(start, math.select(boxMin, boxMax - size + 1, face > 0), axis == 1);
                for (int k = 0; k < count.x * count.y * count.z; k++)
                {
                    int3 slot = new int3(k % count.x, k / count.x % count.y, k / (count.x * count.y));
                    int3 origin = start + slot * size - fpMin;
                    for (int i = 0; i < def.Cells.Length; i++) structure.SetCell(origin + tables.Rotate(def.Cells[i], orient), default);
                    var result = structure.TryPlaceBlock(new BlockPlacement { Block = block, Origin = origin, Orient = orient, Color = 27 }, content);
                    if (result == PlaceResult.Ok) placed++;
                    else { failed++; Debug.LogWarning($"ship_ module {kind} at {origin} refused: {result}"); }
                }
            }
            // Close the rest of the box (bridge housings, gun plinths, engine blocks, silo towers) in dark trim.
            ushort trim = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            for (int z = boxMin.z; z <= boxMax.z; z++) for (int y = boxMin.y; y <= boxMax.y; y++) for (int x = boxMin.x; x <= boxMax.x; x++)
            {
                var c = new int3(x, y, z);
                if (structure.GetCell(c).IsAir) structure.TryPlaceBlock(new BlockPlacement { Block = trim, Origin = c, Orient = 0, Color = 27 }, content);
            }
            return placed;
        }

        // Cruisers are framed, not solid: a one-cell skin, transverse bulkheads every `bulkheadEvery` cells, and full slabs
        // through every function block so reactors and the core stay seated and connected. A solid 48 m hull weighed ~390 t.
        static int Hollow(VoxelStructure structure, VoxelContent content, ushort cube, int bulkheadEvery)
        {
            if (!structure.TryGetCellBounds(out var min, out var max)) return 0;
            var slabs = new HashSet<int>();
            using (var blocks = new Unity.Collections.NativeList<StructureBlock>(Unity.Collections.Allocator.Temp))
            {
                structure.GetBlocks(blocks);
                foreach (var b in blocks)
                {
                    if (b.InstanceIndex < 0) continue;
                    ref var def = ref content.Blocks.Value.Blocks[b.Placement.Block];
                    for (int i = 0; i < def.Cells.Length; i++)
                    {
                        int z = b.Placement.Origin.z + content.Orientations.Value.Rotate(def.Cells[i], b.Placement.Orient).z;
                        for (int dz = -1; dz <= 1; dz++) slabs.Add(z + dz);
                    }
                }
            }
            var interior = new List<int3>();
            for (int z = min.z; z <= max.z; z++)
            {
                if (z % bulkheadEvery == 0 || slabs.Contains(z)) continue;
                for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
                {
                    var c = new int3(x, y, z);
                    if (!structure.TryGetBlockAt(c, out var b, out int instance) || instance >= 0 || b.Block != cube) continue;
                    bool inside = true;
                    for (int dz = -1; dz <= 1 && inside; dz++) for (int dy = -1; dy <= 1 && inside; dy++) for (int dx = -1; dx <= 1 && inside; dx++)
                        inside = !structure.GetCell(c + new int3(dx, dy, dz)).IsAir;
                    if (inside) interior.Add(c);
                }
            }
            foreach (var c in interior) structure.SetCell(c, default);
            return interior.Count;
        }

        // A core and reactors where the hull is thickest, near its middle (REQUIEM's look-book design carries no core module).
        static int SeatCoreAndReactors(VoxelStructure structure, VoxelContent content, ushort core, ushort reactor, int reactors)
        {
            int failed = 0;
            structure.TryGetCellBounds(out var min, out var max);
            int3 centre = (min + max) / 2; centre.x = 0;
            int3 Interior(int3 near)
            {
                for (int r = 0; r < 40; r++)
                    for (int dy = -r; dy <= r; dy++) for (int dz = -r; dz <= r; dz++)
                    {
                        var c = near + new int3(0, dy, dz); bool solid = true;
                        for (int ex = -2; ex <= 3 && solid; ex++) for (int ey = -2; ey <= 3 && solid; ey++) for (int ez = -2; ez <= 3 && solid; ez++)
                            solid = !structure.GetCell(c + new int3(ex, ey, ez)).IsAir;
                        if (solid) return c;
                    }
                return near;
            }
            void Seat(ushort block, int3 at)
            {
                ref var def = ref content.Blocks.Value.Blocks[block];
                for (int i = 0; i < def.Cells.Length; i++) structure.SetCell(at + content.Orientations.Value.Rotate(def.Cells[i], 0), default);
                if (structure.TryPlaceBlock(new BlockPlacement { Block = block, Origin = at, Orient = 0, Color = 27 }, content) != PlaceResult.Ok) failed++;
            }
            Seat(core, Interior(centre));
            for (int i = 0; i < reactors; i++) Seat(reactor, Interior(centre + new int3(0, 0, (i % 2 == 0 ? 1 : -1) * (12 + 8 * (i / 2)))));
            return failed;
        }
    }
}
