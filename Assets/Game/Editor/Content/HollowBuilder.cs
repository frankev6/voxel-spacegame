using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>DEV-023 option E, the Hollow (owner, 2026-10-18: "Lets build this latest version into the game"): the pirate haven in
    /// an asteroid as voxel blueprints. The geometry comes from the hub design (ArtSource/Hub/hollow_export.py, then
    /// hollow_codegen.py writes ArtSource/Hub/hollow/hollow_blocks.bin and the runtime layout HollowLayout.g.cs). The rock is built of
    /// large hull blocks, 2×2×2 to 4×4×4 at 2 m cells (owner: "the asteroid should be built of larger voxels ... 2x2s and 3x3s or even
    /// 4x4s"). Gun kits (core, reactor, capacitor, medium plasma turrets) go into the forts and into one blueprint per static gun.</summary>
    public static class HollowBuilder
    {
        const string Ships = "Assets/Game/Content/Ships/", Ai = "Assets/Game/Content/Ai/";
        const string Source = "ArtSource/Hub/hollow/hollow_blocks.bin";
        public const string GunHeavy = "ship_hollow_gun_heavy", GunLight = "ship_hollow_gun_light";
        public const string TrafficProfile = "ai_hollow_traffic", GuardProfile = "ai_hollow_guard";
        const byte Soot = 11, Graphite = 1, Steel = 13, Red = 21;

        /// <summary>Rock and section tones (palette 40–46) and neon (48–50), set here so the palette asset stays the one source.</summary>
        [Game.Core.StaticStateOk("constant palette additions")]
        static readonly (int index, string name, Color32 colour)[] Palette =
        {
            // light grey stone (owner: "subtle large moon craters, soft, and light gray"); RockSurface.hlsl shades the craters
            // within a few percent of each other: the blocks must not read as a patchwork, the craters carry the variation
            (40, "Rock 1", new Color32(0xB2, 0xB0, 0xAB, 255)), (41, "Rock 2", new Color32(0xB5, 0xB3, 0xAE, 255)),
            (42, "Rock 3", new Color32(0xB0, 0xAE, 0xA9, 255)), (43, "Rock 4", new Color32(0xB4, 0xB2, 0xAC, 255)),
            (44, "Rock 5", new Color32(0xB7, 0xB5, 0xB0, 255)), (45, "Rock 6", new Color32(0xB1, 0xAF, 0xAA, 255)),
            (46, "Rock section", new Color32(0x8E, 0x8C, 0x88, 255)),
            (48, "Neon pink", new Color32(0xFF, 0x2E, 0x88, 255)), (49, "Neon cyan", new Color32(0x22, 0xE6, 0xFF, 255)),
            (50, "Neon amber", new Color32(0xFF, 0xB0, 0x00, 255)),
        };

        [MenuItem("Game/Content/Build Hollow")]
        public static void Build()
        {
            if (!File.Exists(Source)) throw new FileNotFoundException("Run python3 ArtSource/Hub/hollow_codegen.py first", Source);
            TrackStructureBuilder.EnsureBlock(); LargeHullBuilder.Make(4); EnsurePalette();
            for (int side = 1; side <= 4; side++) EnsureRock(side);
            Game.Editor.Art.NocturneBrickLook.BakeRock();   // the rock layer's shading (style 6)
            EnsureProfile(TrafficProfile, "ai_civilian", 12); EnsureProfile(GuardProfile, "ai_default", 60);   // a tight patrol: the posts sit close to the rock
            AssetDatabase.SaveAssets();
            int pieces = 0, cells = 0;
            using (var service = new ContentService(ContentDatabaseBuilder.Rebuild()))
            {
                var catalog = service.Catalog;
                var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
                ushort Block(string id) => catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId(id)));
                ushort hull = Block("block_hull"), frame = Block("block_track_frame");
                var large = new[] { hull, hull, Block("block_hull_large_2"), Block("block_hull_large_3"), Block("block_hull_large_4") };
                var rock = new[] { Block(RockBlock), Block(RockBlock), Block(RockBlock + "_large_2"), Block(RockBlock + "_large_3"), Block(RockBlock + "_large_4") };
                var kit = new Kit(Block("block_core_small"), Block("block_reactor_small"), Block("block_capacitor_small"), Block("weapon_plasma_turret_medium"));

                using var reader = new BinaryReader(File.OpenRead(Source));
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "HOLW" || reader.ReadInt32() != 1) throw new InvalidDataException(Source);
                int count = reader.ReadInt32();
                for (int p = 0; p < count; p++)
                {
                    string id = Read(reader), name = Read(reader);
                    int blocks = reader.ReadInt32();
                    var list = new (short i, short j, short k, byte s, byte c, byte m)[blocks];
                    for (int b = 0; b < blocks; b++)
                        list[b] = (reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                    int guns = reader.ReadInt32();
                    var mounts = new (short i, short j, short k, byte big, byte orient)[guns];
                    for (int g = 0; g < guns; g++) mounts[g] = (reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadByte(), reader.ReadByte());
                    var made = Make(service, content, id, name, 2, put =>
                    {
                        // gun kits first, so the rock never takes their cells
                        foreach (var g in mounts) kit.Place(put, new int3(g.i, g.j, g.k), g.big != 0, g.orient);
                        foreach (var b in list) put(b.m == 2 && b.s == 1 ? frame : (b.m == 0 ? rock : large)[math.clamp((int)b.s, 1, 4)], new int3(b.i, b.j, b.k), b.c, 0);
                    });
                    pieces++; cells += made;
                }
                // the static guns: a plinth below the flight plane, the kit at the plane, its turrets facing +z
                cells += Make(service, content, GunHeavy, "Hollow Heavy Battery", 2, put => Gun(put, large[2], kit, true));
                cells += Make(service, content, GunLight, "Hollow Light Gun", 2, put => Gun(put, large[2], kit, false));
                // the approach field's boulders: four lumpy rocks, hollow shells of 2×2×2 rock blocks
                for (int k = 0; k < BoulderRadii.Length; k++)
                {
                    int size = k;
                    cells += Make(service, content, Boulder + k, "Hollow Boulder " + BoulderRadii[k] + " m", 2, put => Rock(put, rock[2], BoulderRadii[size], size));
                }
            }
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", $"Built the Hollow: {pieces} pieces and two guns, {cells} cells.");
        }

        public const string RockBlock = "block_rock";

        /// <summary>Rock blocks (owner: "the texture of the asteroid should be rock not the ship hull texture"): block_hull and the large
        /// hull cubes with the rock material layer, so the Hollow's stone shades as stone (RockSurface.hlsl) and keeps hull's HP and mass.</summary>
        static void EnsureRock(int side)
        {
            const string blocks = "Assets/Game/Content/Blocks/";
            string from = side == 1 ? "block_hull" : "block_hull_large_" + side, id = side == 1 ? RockBlock : RockBlock + "_large_" + side, path = blocks + id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(blocks + from + ".asset", path)) throw new InvalidOperationException("copy " + from);
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id; so.FindProperty("formerIds").arraySize = 0;
            so.FindProperty("displayName").stringValue = side == 1 ? "Rock" : $"Rock {side}×{side}×{side}";
            so.FindProperty("description").stringValue = "Asteroid stone: scenery for stations built into rock.";
            so.FindProperty("materialLayer").intValue = Game.Editor.Art.NocturneBrickLook.Rock;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }

        static string Read(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt16()));

        delegate void Put(ushort block, int3 origin, byte colour, byte orient);

        /// <summary>A gun's working parts at `at`: core, reactor and capacitor behind, one medium turret (two when heavy) in front facing
        /// local +z, its barrels' clearance ahead left open; the whole kit turned by the yaw-only orientation `orient` (0, 4, 8, 12).</summary>
        readonly struct Kit
        {
            readonly ushort core, reactor, capacitor, turret;
            internal Kit(ushort core, ushort reactor, ushort capacitor, ushort turret) { this.core = core; this.reactor = reactor; this.capacitor = capacitor; this.turret = turret; }
            internal void Place(Put put, int3 at, bool big, byte orient)
            {
                void Part(ushort block, int x, int z, byte colour) => put(block, at + OrientationTables.Rotate(new int3(x, 0, z), orient), colour, orient);
                if (big)
                {
                    Part(core, -3, -3, Soot); Part(reactor, 1, -3, Graphite); Part(capacitor, -1, -3, Graphite);
                    Part(turret, -3, 0, Steel); Part(turret, 1, 0, Steel);
                }
                else
                {
                    Part(core, -2, -2, Soot); Part(reactor, 0, -2, Graphite); Part(turret, -1, 0, Steel);
                }
            }
        }

        public const string Boulder = "ship_hollow_boulder_";
        /// <summary>A static tile of the Hollow's rock or a district's structures: scenery held where it spawns, cut from the layout into
        /// tiles, so it may be several pieces (the boulders and guns are whole, free or armed grids).</summary>
        public static bool IsScenery(string id) => id.StartsWith("ship_hollow_", StringComparison.Ordinal) && !id.StartsWith(Boulder, StringComparison.Ordinal) && id != GunHeavy && id != GunLight;
        [Game.Core.StaticStateOk("constant sizes")] public static readonly int[] BoulderRadii = { 10, 20, 30, 44 };

        /// <summary>A lumpy rock of radius `r` m centred on the grid origin, flattened to ±0.6 r (at most ±40 m): a 4 m shell of 2×2×2
        /// blocks on a 4 m lattice, toned like the Hollow. One piece: a free asteroid grid, so the shell counts any of the 26 lattice
        /// neighbours outside (its blocks then meet face to face) and only the largest face-connected lump is kept.</summary>
        static void Rock(Put put, ushort large2, int r, int seed)
        {
            float rv = math.min(.6f * r, 40);
            int n = (int)math.ceil(r / 4f) + 1, nv = (int)math.ceil(rv / 4f) + 1;
            float Field(float3 p)
            {
                float3 q = p / new float3(r, rv, r);
                float bump = .18f * math.sin(p.x * .21f + seed * 1.7f) * math.cos(p.z * .17f - seed) + .12f * math.sin(p.y * .3f + p.x * .11f + seed * 2.3f);
                return math.length(q) - 1 - bump;
            }
            var shell = new HashSet<int3>();
            for (int x = -n; x < n; x++) for (int y = -nv; y < nv; y++) for (int z = -n; z < n; z++)
            {
                var c = new float3(x * 4 + 2, y * 4 + 2, z * 4 + 2);
                if (Field(c) > 0) continue;
                bool outside = false;
                for (int dx = -1; dx <= 1 && !outside; dx++) for (int dy = -1; dy <= 1 && !outside; dy++) for (int dz = -1; dz <= 1 && !outside; dz++)
                    outside = Field(c + new float3(dx, dy, dz) * 4) > 0;
                if (outside) shell.Add(new int3(x, y, z));
            }
            var keep = new List<int3>();
            var open = new HashSet<int3>(shell);
            while (open.Count > 0)
            {
                var lump = new List<int3>(); var stack = new Stack<int3>();
                foreach (var start in open) { stack.Push(start); break; }
                open.Remove(stack.Peek());
                while (stack.Count > 0)
                {
                    var p = stack.Pop(); lump.Add(p);
                    for (int d = 0; d < 6; d++) { var q = p; q[d >> 1] += (d & 1) == 0 ? 1 : -1; if (open.Remove(q)) stack.Push(q); }
                }
                if (lump.Count > keep.Count) keep = lump;
            }
            foreach (var p in keep)
            {
                uint h = math.hash(new int4(p, seed));
                put(large2, p * 2, (byte)(40 + h % 6), 0);
            }
        }

        static void Gun(Put put, ushort large2, Kit kit, bool big)
        {
            int r = big ? 4 : 3;
            for (int x = -r; x < r; x += 2) for (int z = -r; z < r; z += 2) put(large2, new int3(x, -2, z), x == -r || z == -r ? Red : Graphite, 0);
            kit.Place(put, int3.zero, big, 0);
        }

        /// <summary>Like TrackStructureBuilder.Make, but keeps every island: a rock tile or a district's structures are many pieces.</summary>
        static int Make(ContentService service, VoxelContent content, string id, string name, float cell, Action<Put> build)
        {
            using var structure = new VoxelStructure(cell);
            int failed = 0;
            build((block, origin, colour, orient) =>
            {
                if (structure.TryPlaceBlock(new BlockPlacement { Block = block, Origin = origin, Orient = orient, Color = colour }, content) != PlaceResult.Ok) failed++;
            });
            if (failed > 0) Debug.LogWarning($"{id}: {failed} blocks did not fit");
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
            return structure.SolidCellCount;
        }

        static void EnsurePalette()
        {
            var palette = AssetDatabase.LoadMainAssetAtPath("Assets/Game/Content/Config/config_voxel_palette.asset");
            var so = new SerializedObject(palette);
            var colours = so.FindProperty("colors"); var names = so.FindProperty("names");
            foreach (var (index, name, colour) in Palette)
            {
                colours.GetArrayElementAtIndex(index).colorValue = colour;
                names.GetArrayElementAtIndex(index).stringValue = name;
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(palette);
        }

        /// <summary>An AI profile copied from `from` with a tighter patrol: traffic and guards are steered by moving their anchor.</summary>
        static void EnsureProfile(string id, string from, float patrol)
        {
            string path = Ai + id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Ai + from + ".asset", path)) throw new InvalidOperationException("copy " + from);
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id; so.FindProperty("formerIds").arraySize = 0;
            so.FindProperty("patrolRadius").floatValue = patrol;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
    }
}
