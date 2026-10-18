using System;
using System.Collections.Generic;
using Game.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>ADR-0120 (owner: "make new thruster engines, maybe some go from 0-100% faster but have less max thrust so theres a
    /// give and take, but also it makes sense for thrusters meant to rotate rather than be the main thrust, and maybe larger engines
    /// too, for the bigger ships"). Builds the engine line from code (own work, procedural): five engine blocks with lathed nozzle
    /// meshes, and the Standard spool on the existing thrusters. Re-running rebuilds every generated asset in place.
    /// Mesh units are cells in block-local axes: the root cell's centre is the origin and the exhaust leaves along +Z.</summary>
    public static class EngineContentBuilder
    {
        const string Blocks = "Assets/Game/Content/Blocks/", Meshes = "Assets/Game/Art/Engines/Meshes/";

        /// <summary>One engine row of the line: footprint w × w × length cells, exhaust clearance behind it.</summary>
        public readonly struct Engine
        {
            public readonly string Id, Name, Description; public readonly int Width, Length, Clearance;
            public readonly float Thrust, SpoolUp, SpoolDown, Boost, CellMass, CellHp, FunctionHp; public readonly int Price;
            public Engine(string id, string name, string description, int width, int length, int clearance, float thrust,
                float spoolUp, float spoolDown, float boost, float cellMass, float cellHp, float functionHp, int price)
            {
                Id = id; Name = name; Description = description; Width = width; Length = length; Clearance = clearance; Thrust = thrust;
                SpoolUp = spoolUp; SpoolDown = spoolDown; Boost = boost; CellMass = cellMass; CellHp = cellHp; FunctionHp = functionHp; Price = price;
            }
        }

        // Thrust density is the trade: Standard thrusters give about 10 kN per cell and spool in 0.25 s. Sprint engines answer in a
        // tenth of a second and overdrive hardest under boost, at 8 kN per cell. Torch engines give about 17 kN per cell but take
        // 1.5–2.5 s to spool and run on after release. Verniers are single cells for turning and strafing at a long lever arm.
        public static readonly Engine[] Line =
        {
            new("block_thruster_vernier", "Vernier Thruster", "A one-cell manoeuvring jet. Answers instantly; mount it far from the centre to turn and strafe.",
                1, 1, 1, 10000, .04f, .04f, 1f, 20, 60, 60, 10),
            new("block_engine_sprint_small", "Sprint Engine S", "A racing engine: full thrust in under a tenth of a second and the hardest boost overdrive, but less thrust per cell.",
                1, 2, 3, 16000, .08f, .06f, 2f, 16, 70, 150, 24),
            new("block_engine_sprint_medium", "Sprint Engine M", "A racing engine: full thrust in about a tenth of a second and the hardest boost overdrive, but less thrust per cell.",
                2, 3, 4, 96000, .12f, .08f, 2f, 16, 80, 600, 90),
            new("block_engine_torch_large", "Torch Engine L", "A heavy fusion torch: nearly twice the thrust per cell, but it takes 1.5 s to spool and keeps pushing after you let go.",
                3, 4, 6, 600000, 1.5f, 1f, 1.3f, 40, 160, 2500, 420),
            new("block_engine_torch_capital", "Torch Engine XL", "A capital-ship torch for the biggest hulls: the most thrust in the game, 2.5 s to spool, and a long exhaust.",
                5, 7, 10, 3200000, 2.5f, 1.6f, 1.2f, 45, 200, 9000, 2400),
        };
        // The existing thrusters become the Standard line (ADR-0120 decision 3).
        static readonly string[] Standard = { "block_thruster_small", "block_thruster_medium", "block_thruster_large", "block_xeno_fin" };
        const float StandardUp = .25f, StandardDown = .2f;

        [MenuItem("Game/Content/Build Engine Line")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(Meshes);
            foreach (var engine in Line)
            {
                var mesh = SaveMesh(Model(engine));
                Block(engine, mesh);
            }
            foreach (var id in Standard)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(Blocks + id + ".asset");
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                SetParams(so, null, StandardUp, StandardDown, 0);
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built the engine line (ADR-0120).");
        }

        // ---------------------------------------------------------------------------------------------------- assets
        static void Block(in Engine e, Mesh prop)
        {
            string path = Blocks + e.Id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + "block_thruster_small.asset", path))
                throw new InvalidOperationException("copy block_thruster_small");
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = e.Id; so.FindProperty("displayName").stringValue = e.Name;
            so.FindProperty("description").stringValue = e.Description; so.FindProperty("formerIds").arraySize = 0;
            so.FindProperty("basePrice").intValue = e.Price;
            so.FindProperty("cellMass").floatValue = e.CellMass; so.FindProperty("cellHp").floatValue = e.CellHp;
            so.FindProperty("functionHp").floatValue = e.FunctionHp;
            so.FindProperty("propMesh").objectReferenceValue = prop;
            so.FindProperty("allowedOrientations.Bits").intValue = (1 << 24) - 1;   // round nozzles: any of the 24 orientations
            var cells = new List<Vector3Int>(); var clear = new List<Vector3Int>();
            for (int z = 0; z < e.Length + e.Clearance; z++) for (int y = 0; y < e.Width; y++) for (int x = 0; x < e.Width; x++)
                (z < e.Length ? cells : clear).Add(new Vector3Int(x, y, z));
            Fill(so.FindProperty("cells"), cells); Fill(so.FindProperty("clearance"), clear);
            SetParams(so, e.Thrust, e.SpoolUp, e.SpoolDown, e.Boost);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
        static void Fill(SerializedProperty array, List<Vector3Int> values)
        {
            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) array.GetArrayElementAtIndex(i).vector3IntValue = values[i];
        }
        static void SetParams(SerializedObject so, float? thrust, float up, float down, float boost)
        {
            var modules = so.FindProperty("modules");
            for (int i = 0; i < modules.arraySize; i++)
            {
                var row = modules.GetArrayElementAtIndex(i).FindPropertyRelative("Params");
                if (row?.FindPropertyRelative("ThrustNewtons") == null) continue;
                if (thrust.HasValue) row.FindPropertyRelative("ThrustNewtons").floatValue = thrust.Value;
                row.FindPropertyRelative("SpoolUpSeconds").floatValue = up;
                row.FindPropertyRelative("SpoolDownSeconds").floatValue = down;
                row.FindPropertyRelative("BoostThrottle").floatValue = boost;
            }
        }
        static Mesh SaveMesh(Mesh mesh)
        {
            string path = Meshes + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // CopySerialized leaves a loaded mesh's GPU buffers as they were: re-upload, or open views keep the old shape.
                EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh);
                existing.MarkModified(); existing.UploadMeshData(false); EditorUtility.SetDirty(existing); return existing;
            }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        // ---------------------------------------------------------------------------------------------------- models
        sealed class Builder
        {
            readonly List<CombineInstance> parts = new();
            // uv1.y tags a surface for PropInstanced (above any lightmap range, below the chunk flag 8): 2 = bare nozzle metal,
            // 3 = a nozzle's inner face, which also glows with throttle. Plated parts carry 0.
            public const float Plated = 0, Nozzle = 2, HotFace = 3;
            // Revolves a (radius, z) profile about the engine axis. Smooth parts share seam vertices; flat parts (8-sided
            // housings) give each face its own, so the faceting reads.
            public void Lathe(float2 axis, IList<float2> profile, int segments, bool flat = false, float phase = 0, bool inside = false, float surface = Plated)
            {
                var v = new List<Vector3>(); var t = new List<int>();
                Vector3 P(float r, float z, int s) { float a = phase + 2 * math.PI * s / segments; return new Vector3(axis.x + r * math.cos(a), axis.y + r * math.sin(a), z); }
                for (int i = 0; i + 1 < profile.Count; i++)
                    for (int s = 0; s < segments; s++)
                    {
                        var a = P(profile[i].x, profile[i].y, s); var b = P(profile[i].x, profile[i].y, s + 1);
                        var c = P(profile[i + 1].x, profile[i + 1].y, s + 1); var d = P(profile[i + 1].x, profile[i + 1].y, s);
                        int k = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                        if (inside) t.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 }); else t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                    }
                var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(t, 0);
                if (!flat) { mesh.Optimize(); Weld(mesh); }
                mesh.RecalculateNormals(); Tag(mesh, surface);
                parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.identity });
            }
            static void Tag(Mesh mesh, float surface)
            {
                var uv = new Vector2[mesh.vertexCount];
                for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(0, surface);
                mesh.uv2 = uv;
            }
            // Merges coincident vertices so RecalculateNormals smooths across a smooth part's quads.
            static void Weld(Mesh mesh)
            {
                var v = mesh.vertices; var t = mesh.triangles; var map = new Dictionary<Vector3Int, int>(); var outV = new List<Vector3>();
                for (int i = 0; i < t.Length; i++)
                {
                    var p = v[t[i]]; var key = Vector3Int.RoundToInt(p * 10000);
                    if (!map.TryGetValue(key, out int index)) { index = outV.Count; outV.Add(p); map[key] = index; }
                    t[i] = index;
                }
                mesh.Clear(); mesh.SetVertices(outV); mesh.SetTriangles(t, 0);
            }
            public void Box(Vector3 min, Vector3 max) => Box((min + max) * .5f, max - min, Quaternion.identity);
            public void Box(Vector3 centre, Vector3 size, Quaternion rotation)
            {
                var cube = UnityEngine.Object.Instantiate(Resources.GetBuiltinResource<Mesh>("Cube.fbx")); Tag(cube, Plated);
                parts.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(centre, rotation, size) });
            }
            public Mesh Done(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.CombineMeshes(parts.ToArray(), true, true);
                mesh.RecalculateTangents(); mesh.RecalculateBounds();
                return mesh;
            }
        }
        static List<float2> P(params float[] rz) { var list = new List<float2>(); for (int i = 0; i < rz.Length; i += 2) list.Add(new float2(rz[i], rz[i + 1])); return list; }

        static Mesh Model(in Engine e)
        {
            var b = new Builder();
            float w = e.Width, half = w * .5f, z0 = -.5f, z1 = e.Length - .5f, L = e.Length;
            var axis = new float2((w - 1) * .5f);
            string name = e.Id.Replace("block_", "SM_");
            if (e.Width == 1 && e.Length == 1)
            {
                // Vernier: an eight-sided collar with a short cone nozzle.
                b.Lathe(axis, P(0, z0, .44f, z0, .44f, .05f, .3f, .12f, 0, .12f), 8, flat: true, phase: math.PI / 8);
                b.Lathe(axis, P(.12f, .12f, .2f, .3f, .3f, .5f, .28f, .5f), 16, surface: Builder.Nozzle);
                b.Lathe(axis, P(.28f, .5f, .18f, .3f, .1f, .14f, 0, .14f), 16, surface: Builder.HotFace);   // reversed profile: faces the axis
                return b.Done(name);
            }
            if (e.Boost >= 2f)
            {
                // Sprint (Vestari, high tech): a smooth slim body, two collar rings, a long narrow bell.
                float throat = z0 + L * .62f;
                b.Lathe(axis, P(0, z0, half * .62f, z0, half * .86f, z0 + .25f, half * .86f, throat - .3f, half * .56f, throat, 0, throat), 20);   // closed: no see-through seam at the bell
                foreach (float f in new[] { .18f, .42f })
                {
                    float zc = z0 + L * f;
                    b.Lathe(axis, P(half * .86f, zc - .08f, half * .97f, zc - .05f, half * .97f, zc + .05f, half * .86f, zc + .08f), 20);
                }
                b.Lathe(axis, P(half * .56f, throat, half * .6f, throat + (z1 - throat) * .5f, half * .9f, z1, half * .84f, z1), 24, surface: Builder.Nozzle);   // the lip closes onto the inner face
                b.Lathe(axis, P(half * .84f, z1, half * .54f, throat + (z1 - throat) * .5f, half * .5f, throat + .02f, 0, throat + .02f), 24, surface: Builder.HotFace);   // reversed profile: faces the axis
                return b.Done(name);
            }
            {
                // Torch (Khuchad, heavy): an eight-sided armoured housing, a ribbed combustion chamber, struts and a wide bell.
                // The capital torch (XL) is its own design: a longer housing finned with radiators, six chamber ribs and
                // eight struts, and a banded bell.
                bool capital = e.Width >= 5;
                float chamber = z0 + L * (capital ? .4f : .45f), throat = z0 + L * .62f, d = z1 - throat;
                b.Lathe(axis, P(0, z0, half * .9f, z0, half, z0 + .2f, half, chamber - .15f, half * .8f, chamber, 0, chamber), 8, flat: true, phase: math.PI / 8);
                b.Lathe(axis, P(half * .62f, chamber, half * .62f, throat, half * .4f, throat + .1f, 0, throat + .1f), 16, surface: Builder.Nozzle);
                int ribs = capital ? 6 : 3;
                for (int r = 0; r < ribs; r++)
                {
                    float zc = chamber + (throat - chamber) * (r + .5f) / ribs, rib = capital && r % 3 == 1 ? .82f : .74f;
                    b.Lathe(axis, P(half * .62f, zc - .06f, half * rib, zc - .03f, half * rib, zc + .03f, half * .62f, zc + .06f), 16);
                }
                int struts = capital ? 8 : 4;
                for (int s = 0; s < struts; s++)
                {
                    float a = math.PI / 4 + s * 2 * math.PI / struts, r = half * .78f;
                    var c = new Vector3(axis.x + r * math.cos(a), axis.y + r * math.sin(a), (chamber + throat) * .5f + .1f);
                    float t = (capital ? .05f : .08f) * w;
                    b.Box(c - new Vector3(t, t, (throat - chamber) * .55f), c + new Vector3(t, t, (throat - chamber) * .55f));
                }
                if (capital)
                {
                    // Radiator fins on the four diagonal faces of the housing, three per face, inside the square footprint.
                    for (int s = 0; s < 4; s++)
                    {
                        float a = math.PI / 4 + s * math.PI / 2;
                        var radial = new Vector3(math.cos(a), math.sin(a), 0); var across = new Vector3(-radial.y, radial.x, 0);
                        var rotation = Quaternion.Euler(0, 0, math.degrees(a));
                        for (int f = -1; f <= 1; f++)
                        {
                            var c = new Vector3(axis.x, axis.y, (z0 + .2f + chamber - .15f) * .5f) + radial * half * 1.0f + across * f * half * .16f;
                            b.Box(c, new Vector3(half * .24f, .05f * w, chamber - z0 - .7f), rotation);
                        }
                    }
                }
                var bell = P(half * .4f, throat + .1f, half * .5f, throat + d * .4f, half * .76f, throat + d * .8f, half * .98f, z1, half * .92f, z1);   // the lip closes onto the inner face
                b.Lathe(axis, bell, 24, surface: Builder.Nozzle);
                b.Lathe(axis, P(half * .92f, z1, half * .7f, throat + d * .8f, half * .44f, throat + d * .4f, half * .3f, throat + .2f, 0, throat + .2f), 24, surface: Builder.HotFace);   // reversed profile: faces the axis
                if (capital)
                    foreach (float f in new[] { .45f, .78f })
                    {
                        // Stiffening bands round the bell.
                        float zb = throat + d * f, rb = BellRadius(bell, zb) + .04f * w;
                        b.Lathe(axis, P(rb - .05f * w, zb - .12f, rb, zb - .08f, rb, zb + .08f, rb - .05f * w, zb + .12f), 24);
                    }
                return b.Done(name);
            }
        }
        static float BellRadius(List<float2> profile, float z)
        {
            for (int i = 0; i + 1 < profile.Count; i++)
                if (z <= profile[i + 1].y) return math.lerp(profile[i].x, profile[i + 1].x, math.unlerp(profile[i].y, profile[i + 1].y, z));
            return profile[^1].x;
        }
    }
}
