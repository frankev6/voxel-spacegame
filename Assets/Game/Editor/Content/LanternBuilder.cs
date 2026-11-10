using System;
using System.IO;
using System.Text;
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
    /// <summary>SPEC-93 EXT-007, Tollhouse Nine ("the Lantern"): the town as voxel blueprints. The town's five levels come from
    /// ArtSource/Places/lantern_export.py (ArtSource/Places/lantern/lantern_blocks.bin, 4 m cells); the moving and breakable parts are
    /// made here: the doors (the big door, the Grate, the Cradle's floor hatch, the staff door, the Crown's hangar door), the six tether
    /// posts, the fuel depot's tanks and the station core itself. Owner (2026-11-10): "proceed with building it onto the game".
    /// Also the Rock den's stone (EXT-008) and its build kit, so one menu item makes all of the extraction content.</summary>
    public static class LanternBuilder
    {
        const string Ships = "Assets/Game/Content/Ships/";
        const string Source = "ArtSource/Places/lantern/lantern_blocks.bin";
        public const string Core = "ship_lantern_core", Post = "ship_lantern_post", Tank = "ship_lantern_tank_";
        public const byte Ivory = 60, Ivory2 = 61, Jade = 62, Brass = 63, Hull = 64, Hull2 = 65, Armor = 66, Deck = 67, Rust = 68, Teal = 69,
            Ochre = 70, Glass = 71, Glow = 72, Red = 73, TankWhite = 74, Truss = 75, Warm = 76;

        [StaticStateOk("constant palette additions")]
        static readonly (int index, string name, Color32 colour)[] Palette =
        {
            (60, "Lantern ivory", new Color32(226, 218, 196, 255)), (61, "Lantern ivory shade", new Color32(205, 196, 172, 255)),
            (62, "Lantern jade", new Color32(58, 168, 140, 255)), (63, "Lantern brass", new Color32(214, 160, 72, 255)),
            (64, "Lantern hull", new Color32(120, 126, 134, 255)), (65, "Lantern hull dark", new Color32(78, 84, 92, 255)),
            (66, "Lantern armour", new Color32(58, 62, 70, 255)), (67, "Lantern deck", new Color32(150, 140, 120, 255)),
            (68, "Shanty rust", new Color32(148, 92, 60, 255)), (69, "Shanty teal", new Color32(70, 110, 108, 255)),
            (70, "Shanty ochre", new Color32(176, 140, 80, 255)), (71, "Lantern glass", new Color32(120, 190, 230, 255)),
            (72, "Core glow", new Color32(150, 245, 255, 255)), (73, "Warning red", new Color32(210, 60, 40, 255)),
            (74, "Tank white", new Color32(220, 222, 216, 255)), (75, "Truss", new Color32(96, 92, 84, 255)),
            (76, "Warm window", new Color32(255, 200, 120, 255)),
            (77, "Den stone 1", new Color32(112, 104, 96, 255)), (78, "Den stone 2", new Color32(98, 92, 86, 255)),
            (79, "Den stone 3", new Color32(124, 114, 102, 255)), (80, "Den ore", new Color32(180, 120, 60, 255)),
        };

        [MenuItem("Game/Content/Build Lantern and Den")]
        public static void Build()
        {
            if (!File.Exists(Source)) throw new FileNotFoundException("Run python3 ArtSource/Places/lantern_export.py first", Source);
            EnsurePalette();
            AssetDatabase.SaveAssets();
            int pieces = 0, cells = 0;
            using (var service = new ContentService(ContentDatabaseBuilder.Rebuild()))
            {
                var catalog = service.Catalog;
                var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
                ushort Block(string id) => catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId(id)));
                ushort hull = Block("block_hull"), rock = Block("block_rock"), rock2 = Block("block_rock_large_2");
                using (var reader = new BinaryReader(File.OpenRead(Source)))
                {
                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "LANT" || reader.ReadInt32() != 1) throw new InvalidDataException(Source);
                    int count = reader.ReadInt32();
                    for (int p = 0; p < count; p++)
                    {
                        string id = Read(reader), name = Read(reader);
                        float cell = reader.ReadSingle();
                        int blocks = reader.ReadInt32();
                        var list = new (short i, short j, short k, byte c, byte m)[blocks];
                        for (int b = 0; b < blocks; b++) list[b] = (reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(), reader.ReadByte(), reader.ReadByte());
                        cells += Make(service, content, id, name, cell, put => { foreach (var b in list) put(b.m == 2 ? rock : hull, new int3(b.i, b.j, b.k), b.c, 0); });
                        pieces++;
                    }
                }
                // doors: slabs of 4 m cells centred on the grid origin (the layout's closed pose)
                cells += Make(service, content, "ship_lantern_door_big", "Tollhouse Nine: the big door", 4, put => Slab(put, hull, 2, 12, 14, Armor, Red));
                cells += Make(service, content, "ship_lantern_door_grate", "Tollhouse Nine: the Grate", 4, put => Grate(put, hull));
                cells += Make(service, content, "ship_lantern_door_hatch", "Tollhouse Nine: the Cradle's floor hatch", 4, put => Slab(put, hull, 14, 1, 14, Armor, Brass));
                cells += Make(service, content, "ship_lantern_door_staff", "Tollhouse Nine: the staff door", 4, put => Slab(put, hull, 1, 11, 7, Hull2, Warm));
                cells += Make(service, content, "ship_lantern_door_crown", "Tollhouse Nine: the Crown's hangar door", 4, put => Slab(put, hull, 1, 6, 12, Ivory2, Jade));
                // a tether post: a 6 × 10 × 6 m block of 2 m cells with a jade clamp on top (it breaks under fire: cutting by force)
                cells += Make(service, content, Post, "Tollhouse Nine: a tether post", 2, put =>
                {
                    for (int x = -1; x <= 1; x++) for (int y = -2; y <= 2; y++) for (int z = -1; z <= 1; z++) put(hull, new int3(x, y, z), y == 2 ? Jade : Armor, 0);
                });
                // the depot's tanks: upright cylinders of 4 m cells, white with a red band
                int[] radii = { 34, 29, 26 };
                for (int t = 0; t < radii.Length; t++)
                {
                    int r = radii[t];
                    cells += Make(service, content, Tank + t, "Tollhouse Nine: fuel tank " + (t + 1), 4, put => Cylinder(put, hull, r / 4f, 15, TankWhite, Red));
                }
                // the station core: a 40 m ball of 2 m cells, glowing jade-white
                cells += Make(service, content, Core, "Station core", 2, put => Ball(put, hull, 10, Glow, Jade));
                cells += DenBuilder.Build(service, content, Block, Make);
            }
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", $"Built Tollhouse Nine and the Rock: {pieces} town pieces, {cells} cells.");
        }

        public delegate void Put(ushort block, int3 origin, byte colour, byte orient);
        public delegate int Maker(ContentService service, VoxelContent content, string id, string name, float cell, Action<Put> build);

        static void Slab(Put put, ushort block, int sx, int sy, int sz, byte colour, byte edge)
        {
            for (int x = 0; x < sx; x++) for (int y = 0; y < sy; y++) for (int z = 0; z < sz; z++)
            {
                bool rim = y == 0 || y == sy - 1 || z == 0 || z == sz - 1 || (sx > 4 && (x == 0 || x == sx - 1));
                put(block, new int3(x - sx / 2, y - sy / 2, z - sz / 2), rim ? edge : colour, 0);
            }
        }

        /// <summary>The Grate: a lattice panel over the Cradle; the core's light shows up the Well through it.</summary>
        static void Grate(Put put, ushort block)
        {
            const int n = 26;
            for (int x = 0; x < n; x++) for (int z = 0; z < n; z++)
            {
                bool bar = x % 3 == 0 || z % 3 == 0 || x == n - 1 || z == n - 1;
                if (!bar) continue;
                put(block, new int3(x - n / 2, 0, z - n / 2), (x + z) % 2 == 0 ? Brass : Armor, 0);
                if (x == 0 || z == 0 || x == n - 1 || z == n - 1) put(block, new int3(x - n / 2, -1, z - n / 2), Armor, 0);
            }
        }

        static void Cylinder(Put put, ushort block, float r, int h, byte colour, byte band)
        {
            int n = (int)math.ceil(r);
            for (int x = -n; x <= n; x++) for (int z = -n; z <= n; z++)
            {
                float d = math.length(new float2(x + .5f, z + .5f));
                if (d > r) continue;
                bool wall = d > r - 1.2f;
                for (int y = 0; y < h; y++)
                    if (wall || y == 0 || y == h - 1) put(block, new int3(x, y - h / 2, z), y == h / 2 || y == h / 2 + 1 ? band : colour, 0);
            }
        }

        static void Ball(Put put, ushort block, int r, byte glow, byte collar)
        {
            for (int x = -r; x < r; x++) for (int y = -r; y < r; y++) for (int z = -r; z < r; z++)
            {
                float d = math.length(new float3(x + .5f, y + .5f, z + .5f));
                if (d > r) continue;
                put(block, new int3(x, y, z), math.abs(y + .5f) < 1.2f && d > r - 2 ? collar : glow, 0);
            }
        }

        static string Read(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt16()));

        /// <summary>A blueprint from what `build` places, keeping every island (a town piece is many lumps).</summary>
        public static int Make(ContentService service, VoxelContent content, string id, string name, float cell, Action<Put> build)
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
    }
}
