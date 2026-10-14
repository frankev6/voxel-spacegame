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
    /// <summary>ADR-0115: builds the Xeno content from code (own work, procedural): tissue/sac blocks, organ blocks with small
    /// procedural meshes, the acid weapon and grown hulls sampled from signed-distance primitives on the 0.5 m grid. A port
    /// of the look book's ArtSource/LookDev/ld_aliens.py (HUSK, BROODMOTHER); look-book axes (x, forward y, up z) map to
    /// game (x, up y, forward z). Re-running rebuilds every generated asset in place.</summary>
    public static class XenoContentBuilder
    {
        const string Blocks = "Assets/Game/Content/Blocks/", Ships = "Assets/Game/Content/Ships/", Weapons = "Assets/Game/Content/Weapons/";
        const string Meshes = "Assets/Game/Art/Xeno/Meshes/";
        public const int TissueLayer = 8, OrganLayer = 9;

        [MenuItem("Game/Content/Build Xeno Content")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(Meshes);
            var sac = SaveMesh(Ellipsoid("xeno_sac", new float3(.5f, .5f, .5f), 18, 12));
            var eye = SaveMesh(Ellipsoid("xeno_eye", new float3(.46f, .4f, .5f), 18, 12));
            var vent = SaveMesh(Ellipsoid("xeno_vent", new float3(.5f, .32f, .5f), 18, 10));
            Block("block_hull", "block_xeno_tissue", "Xeno tissue", "Grown carapace. Damage exposes the living flesh beneath.", TissueLayer, null,
                so => { Set(so, "shape", 5); Set(so, "cellHp", 80f); Set(so, "cellMass", 20f); });
            // Sacs share the tissue layer; their maximum depth makes them bulge from the shell and glow through the smooth depth channel.
            Block("block_hull", "block_xeno_sac", "Xeno sac", "A glowing fluid sac in the carapace.", TissueLayer, null,
                so => { Set(so, "shape", 5); Set(so, "cellHp", 40f); Set(so, "cellMass", 15f); });
            Block("block_core_small", "block_xeno_heart", "Xeno heart", "The organ that keeps a grown ship alive.", OrganLayer, sac, null);
            Block("block_thruster_small", "block_xeno_fin", "Xeno fin", "A pulsing propulsion vent.", OrganLayer, vent, so =>
            {
                var modules = so.FindProperty("modules");
                for (int i = 0; i < modules.arraySize; i++)
                {
                    var thrust = modules.GetArrayElementAtIndex(i).FindPropertyRelative("Params.ThrustNewtons");
                    if (thrust != null) thrust.floatValue = 90000;
                }
            });
            var acid = Weapon("wpn_plasma_turret_small", "wpn_xeno_acid", "Acid spitter", so =>
            {
                Set(so, "damage", 45f); Set(so, "impulsePerHit", 90f); Set(so, "fireRate", 1.5f); Set(so, "projectileSpeed", 340f); Set(so, "spreadDeg", 3f);
                so.FindProperty("tracerColor").colorValue = new Color(.72f, 1f, .18f, 1);
            });
            Block("weapon_plasma_turret_small", "weapon_xeno_spitter", "Xeno spitter", "An eye that spits corrosive acid.", OrganLayer, eye, so =>
            {
                var modules = so.FindProperty("modules");
                for (int i = 0; i < modules.arraySize; i++)
                {
                    var weapon = modules.GetArrayElementAtIndex(i).FindPropertyRelative("weapon");
                    if (weapon != null) weapon.objectReferenceValue = acid;
                }
            });
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            // The catalog must contain the new blocks before hulls reference them.
            using var service = new ContentService(ContentDatabaseBuilder.Rebuild());
            Hull(service, "ship_xeno_husk", "HUSK", Husk());
            Hull(service, "ship_xeno_broodmother", "BROODMOTHER", Broodmother());
            AssetDatabase.SaveAssets(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built Xeno content (ADR-0115).");
        }

        // ---------------------------------------------------------------------------------------------------- designs
        sealed class Design
        {
            public readonly List<(bool add, Func<float3, float> sdf, float k)> Prims = new();
            public readonly List<Func<float3, float>> Sacs = new();
            public float3 Heart; public readonly List<float3> Spitters = new(); public readonly List<float3> Fins = new();
            public float2 Lumps; public float Seed;
            public void Add(Func<float3, float> sdf, float k = 1.5f) => Prims.Add((true, sdf, k));
            public void Sub(Func<float3, float> sdf, float k = .8f) => Prims.Add((false, sdf, k));
        }
        // Look-book coordinates (x, y forward, z up) to game cells (x, y up, z forward), with a forward shift.
        static float3 G(float x, float y, float z, float shift) => new float3(x, z, y - shift);
        static Func<float3, float> Ell(float3 c, float3 r) => p => (math.length((p - c) / r) - 1) * math.cmin(r);
        static Func<float3, float> Cone(float3 a, float3 b, float ra, float rb) => p =>
        {
            float3 ba = b - a; float t = math.saturate(math.dot(p - a, ba) / math.dot(ba, ba));
            return math.length(p - (a + t * ba)) - math.lerp(ra, rb, t);
        };
        static Func<float3, float> Torus(float3 c, float3 n, float major, float minor) => p =>
        {
            n = math.normalize(n); float3 q = p - c; float h = math.dot(q, n);
            return math.sqrt(math.square(math.length(q - h * n) - major) + h * h) - minor;
        };

        static Design Husk()
        {
            const float s = 10;   // forward shift: head near +17, tail near -24
            var d = new Design { Seed = 71 };
            d.Add(Ell(G(0, 14, 3, s), new float3(15, 2.6f, 13)), 2);
            d.Add(Ell(G(0, 13, 5, s), new float3(5, 3.2f, 10)), 2.5f);
            foreach (int sx in new[] { -1, 1 })
            {
                d.Add(Cone(G(sx * 4.5f, 22, 3.2f, s), G(sx * 6.5f, 33, 3.8f, s), 2, .5f));
                d.Add(Cone(G(sx * 3.5f, 12, 4.5f, s), G(sx * 13.5f, 7, 3.2f, s), 1.3f, .55f), 1.2f);
                d.Add(Cone(G(sx * 3.5f, 18, 4.2f, s), G(sx * 12.5f, 18, 3, s), 1.1f, .5f), 1.2f);
                for (int i = 0; i < 4; i++) d.Sacs.Add(Ell(G(sx * (6.5f + i * 2), 15 - i * 2.2f, 5.6f, s), new float3(.9f, .9f, .9f)));
                d.Spitters.Add(G(sx * 2.2f, 26.5f, 4.6f, s));
                d.Fins.Add(G(sx * 3.5f, 1, 2.4f, s));
            }
            d.Add(Cone(G(0, 3, 3.5f, s), G(0, -14, 4.5f, s), 2.2f, .45f), 2);
            d.Sub(Ell(G(0, 27, 3.2f, s), new float3(2.6f, 1.8f, 2.2f)));
            for (int i = 0; i < 5; i++)   // dorsal spines, thickened to read at 0.5 m
                d.Add(Cone(G(0, 6 + i * 3.5f, 7.6f - math.abs(i - 2) * .5f, s), G(0, 2.5f + i * 3.5f, 11.5f - math.abs(i - 2) * .8f, s), .9f, .25f), .6f);
            d.Sacs.Add(Ell(G(0, 27, 2.6f, s), new float3(1.6f, 1.4f, 1.6f)));
            d.Heart = G(0, 13, 4.2f, s);
            return d;
        }
        static Design Broodmother()
        {
            const float s = 42;
            var d = new Design { Seed = 72 };
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f), y = 6 + i * 9, r = 6.5f + 5.5f * math.sin(math.PI * (.15f + .75f * t));
                d.Add(Ell(G(0, y, 8, s), new float3(r * .8f, r * .55f, 6)), 1.2f);
                d.Add(Ell(G(0, y + 1.5f, 8 + r * .32f, s), new float3(r * 1.18f, 2.1f, 5.6f)), .5f);
                foreach (int sx in new[] { -1, 1 })
                {
                    d.Add(Cone(G(sx * r * .95f, y + 1.5f, 8 + r * .2f, s), G(sx * (r * 1.18f + 4), y - 3.5f, 7.5f, s), 1.6f, .45f), .6f);
                    d.Sacs.Add(Ell(G(sx * r * .62f, y + 1, 8 - r * .42f, s), new float3(1f, .9f, 1f)));
                    if (i > 0 && i < n - 1)    // legs hanging underneath
                        d.Add(Cone(G(sx * r * .45f, y, 8 - r * .45f, s), G(sx * r * .78f, y + 3.5f, -1.5f, s), 1.1f, .55f), .5f);
                }
                if (i >= 1 && i <= n - 2)
                    d.Add(Cone(G(0, y + 1.5f, 8 + r * .32f + 1.8f, s), G(0, y - 3.5f, 8 + r * .32f + 6 - math.abs(i - 4) * .6f, s), 1.1f, .3f), .5f);
            }
            d.Add(Ell(G(0, 89, 9.5f, s), new float3(13, 3.2f, 9)), 1.2f);
            d.Add(Ell(G(0, 92, 7.5f, s), new float3(6.5f, 4.5f, 7.5f)), 1.5f);
            foreach (int sx in new[] { -1, 1 })
            {
                d.Add(Cone(G(sx * 11.5f, 86, 9.5f, s), G(sx * 17, 66, 9, s), 2.4f, .5f), .8f);
                d.Add(Cone(G(sx * 4, 96, 9.5f, s), G(sx * 2.2f, 107, 11.5f, s), 1.8f, .45f));
                d.Add(Cone(G(sx * 3, -4, 8.5f, s), G(sx * 7.5f, -22, 9.5f, s), 1.5f, .4f), .8f);
                for (int j = 0; j < 3; j++) d.Spitters.Add(G(sx * (3 + j * 1.6f), 95.5f - j * 1.2f, 11.6f - j * .3f, s));
                d.Fins.Add(G(sx * 2.5f, -6, 7, s));
            }
            d.Sub(Ell(G(0, 98.5f, 5.5f, s), new float3(3.4f, 2, 3)));
            d.Add(Ell(G(0, -2, 8.5f, s), new float3(7.5f, 1.8f, 5.5f)), 1.2f);
            d.Sacs.Add(Ell(G(0, 98.5f, 5.6f, s), new float3(2.4f, 2.2f, 2.4f)));
            d.Heart = G(0, 46, 8, s);
            return d;
        }

        // ---------------------------------------------------------------------------------------------------- hull sampling
        static float Smin(float a, float b, float k) { float h = math.saturate(.5f + .5f * (b - a) / k); return b * (1 - h) + a * h - k * h * (1 - h); }
        static float Field(Design d, float3 p)
        {
            float value = 1e3f;
            foreach (var (add, sdf, k) in d.Prims)
                value = add ? Smin(value, sdf(p), k) : -Smin(-value, sdf(p), k);
            return value;
        }
        static void Hull(ContentService service, string id, string name, Design design)
        {
            var catalog = service.Catalog;
            var content = new VoxelContent { Blocks = service.BlockDefs, Orientations = service.Orientations };
            ushort Index(string block) => catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new ContentId(block)));
            using var structure = new VoxelStructure();
            // Palette index 31 is "Xeno chitin" (DESIGN-SYSTEM §10.13): debris and salvage read dark, not as team paint.
            ushort tissue = structure.FindOrAddPalette(new PaletteEntry { Block = Index("block_xeno_tissue"), Color = 31 });
            ushort sac = structure.FindOrAddPalette(new PaletteEntry { Block = Index("block_xeno_sac"), Color = 31 });
            // Bounds from a coarse scan: every primitive is within 70 cells of the origin for these designs.
            int3 lo = new int3(-48, -24, -80), hi = new int3(48, 40, 80);
            for (int z = lo.z; z <= hi.z; z++) for (int y = lo.y; y <= hi.y; y++) for (int x = lo.x; x <= hi.x; x++)
            {
                float3 p = new float3(x, y, z) + .5f;
                float value = Field(design, p);
                if (value >= 0) continue;
                bool isSac = false;
                foreach (var s in design.Sacs) if (s(p) < 0) { isSac = true; break; }
                byte flags = XenoDepth.With(0, isSac ? 4 : -value);
                structure.SetCell(new int3(x, y, z), Cell.Palette(isSac ? sac : tissue, 255, flags));
            }
            void Place(string block, float3 at, byte orient)
            {
                var cell = (int3)math.floor(at); ushort index = Index(block);
                // Organs replace the tissue under their whole rotated footprint; never over another organ.
                ref var def = ref content.Blocks.Value.Blocks[index];
                for (int i = 0; i < def.Cells.Length; i++)
                    if (structure.GetCell(cell + content.Orientations.Value.Rotate(def.Cells[i], orient)).IsInstance) { Debug.LogWarning($"{id}: {block} at {cell} overlaps an organ"); return; }
                for (int i = 0; i < def.Cells.Length; i++) structure.SetCell(cell + content.Orientations.Value.Rotate(def.Cells[i], orient), default);
                var result = structure.TryPlaceBlock(new BlockPlacement { Block = index, Origin = cell, Orient = orient }, content);
                if (result != PlaceResult.Ok) Debug.LogWarning($"{id}: {block} at {cell} -> {result}");
            }
            Place("block_xeno_heart", design.Heart, 0);
            foreach (var p in design.Spitters) Place("weapon_xeno_spitter", p, 0);
            foreach (var p in design.Fins) Place("block_xeno_fin", p, 8);
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
            Debug.Log($"{id}: {structure.SolidCellCount} cells, bounds {min}..{max}");
        }

        // ---------------------------------------------------------------------------------------------------- assets
        static void Block(string template, string id, string name, string description, int layer, Mesh prop, Action<SerializedObject> edit)
        {
            string path = Blocks + id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + template + ".asset", path))
                throw new InvalidOperationException("copy " + template);
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = description; so.FindProperty("formerIds").arraySize = 0;
            Set(so, "materialLayer", layer); Set(so, "tradeable", false); Set(so, "basePrice", 0);
            if (prop != null) so.FindProperty("propMesh").objectReferenceValue = prop;
            edit?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
        static WeaponDefinition Weapon(string template, string id, string name, Action<SerializedObject> edit)
        {
            string path = Weapons + id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Weapons + template + ".asset", path))
                throw new InvalidOperationException("copy " + template);
            var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = name; so.FindProperty("formerIds").arraySize = 0;
            edit(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
            return asset;
        }
        static void Set(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field) ?? throw new InvalidOperationException("No field " + field);
            if (p.propertyType == SerializedPropertyType.Float) p.floatValue = value; else p.intValue = (int)value;
        }
        static void Set(SerializedObject so, string field, bool value) => so.FindProperty(field).boolValue = value;

        static Mesh Ellipsoid(string name, float3 radius, int segments, int rings)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings, phi = math.PI * v;
                for (int sIndex = 0; sIndex <= segments; sIndex++)
                {
                    float u = sIndex / (float)segments, theta = 2 * math.PI * u;
                    float3 unit = new float3(math.sin(phi) * math.cos(theta), math.cos(phi), math.sin(phi) * math.sin(theta));
                    vertices.Add(unit * radius); normals.Add(math.normalize(unit / radius)); uvs.Add(new Vector2(u, v));
                }
            }
            for (int r = 0; r < rings; r++) for (int sIndex = 0; sIndex < segments; sIndex++)
            {
                int a = r * (segments + 1) + sIndex, b = a + segments + 1;
                tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }
        static Mesh SaveMesh(Mesh mesh)
        {
            string path = Meshes + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
    }
}
