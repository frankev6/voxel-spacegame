using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>QA-001 (owner: "make a new large gun for a cruiser level ship, i feel like im fighting with pebbles"). The siege
    /// cannon: a 4x2x4 heavy turret firing slow armour-piercing shells that burst in a 4 m blast. The burst is what makes a
    /// cruiser's broadside felt: 1600 at the centre with the quadratic falloff clears ~3 m of hull (100 HP) and ~3.5 m of xeno
    /// flesh (30 HP) per shell, so three or four hits sever a HUSK's waist (owner: "is is not possible for me to sever an alien
    /// ship in half by breaking accross its body?"). Its prop is own work, generated here in cell units (origin at the
    /// centre of cell 0, +z forward, +y up); functional props are shaded by their layer's plating in mesh space.</summary>
    public static class HeavyWeaponBuilder
    {
        const string Blocks = "Assets/Game/Content/Blocks/", Weapons = "Assets/Game/Content/Weapons/", Meshes = "Assets/Game/Art/Nocturne/Meshes/";
        public const string WeaponId = "wpn_siege_cannon_heavy", BlockId = "weapon_siege_cannon_heavy";

        [MenuItem("Game/Content/Build Heavy Weapons")]
        public static void Build()
        {
            System.IO.Directory.CreateDirectory(Meshes);
            var mesh = SaveMesh(SiegeCannonMesh());
            var weapon = Weapon("wpn_railgun_medium", WeaponId, "Siege Cannon", so =>
            {
                Set(so, "damageType", 2);                         // Explosive
                Set(so, "damage", 500f); Set(so, "penetrationCells", 3f);
                Set(so, "explosionRadius", 4f); Set(so, "explosionDamage", 1600f); Set(so, "explodeOnExpire", true);
                Set(so, "impulsePerHit", 1000f);                // SPEC-21 table 49: twice the hit damage
                Set(so, "fireRate", .3f); Set(so, "burstCount", 1); Set(so, "burstInterval", 0f); Set(so, "pelletsPerRound", 1);
                Set(so, "spreadDeg", .4f); Set(so, "projectileSpeed", 420f); Set(so, "projectileRadius", .35f);
                Set(so, "lifetimeMin", 2f); Set(so, "lifetimeMax", 2f); Set(so, "homingTurnRateDeg", 0f);
                Set(so, "traverseHalfArcDeg", 150f); Set(so, "traverseRateDeg", 45f); Set(so, "maxElevationDeg", 20f); Set(so, "fireConeDeg", 4f);
                Set(so, "powerDraw", 25f); Set(so, "defaultFireGroups", 2);   // Group2 (right mouse) with the lance: the heavy weapons
                Set(so, "visual", 0);                             // standard tracer, drawn thick and hot
                so.FindProperty("tracerColor").colorValue = new Color(1f, .78f, .42f, 1);
                var plasma = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Weapons + "wpn_plasma_turret_medium.asset");
                so.FindProperty("fireCue").objectReferenceValue = new SerializedObject(plasma).FindProperty("fireCue").objectReferenceValue;
            });
            Block("weapon_plasma_turret_medium", BlockId, "Siege Cannon", "A cruiser's heavy turret (4x2x4). Twin barrels fire slow armour-piercing shells that burst through a hull section.", mesh, so =>
            {
                Cells(so.FindProperty("cells"), Footprint());
                Cells(so.FindProperty("clearance"), Clearance());
                Set(so, "cellHp", 200f); Set(so, "functionHp", 1500f); Set(so, "cellMass", 40f);
                Set(so, "rarity", 3); Set(so, "basePrice", 900);
                var modules = so.FindProperty("modules");
                for (int i = 0; i < modules.arraySize; i++)
                {
                    var element = modules.GetArrayElementAtIndex(i);
                    var w = element.FindPropertyRelative("weapon");
                    if (w == null) continue;
                    w.objectReferenceValue = weapon;
                    var muzzle = element.FindPropertyRelative("MuzzleOffsetCells");
                    float3 m = (new float3(1, .65f, 6.6f) - Corner) * Stretch + Corner;
                    muzzle.FindPropertyRelative("x").floatValue = m.x; muzzle.FindPropertyRelative("y").floatValue = m.y; muzzle.FindPropertyRelative("z").floatValue = m.z;
                }
            });
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); ContentDatabaseBuilder.Rebuild();
            GameLog.Info("Content", "Built heavy weapons (siege cannon).");
        }

        static IEnumerable<int3> Footprint()
        {
            for (int z = 0; z < 4; z++) for (int y = 0; y < 2; y++) for (int x = 0; x < 4; x++) yield return new int3(x, y, z);
        }
        // The barrels run 5 cells past the gunhouse at the upper cell row; that space must stay open.
        static IEnumerable<int3> Clearance()
        {
            for (int z = 4; z < 9; z++) for (int x = 0; x < 4; x++) yield return new int3(x, 1, z);
        }
        // The model is drawn on a 3x2x3 grid and stretched to the 4x2x4 block from the footprint's corner, so a cruiser's
        // heavy turret reads as one (2 m gunhouse, barrels to 4.5 m) next to the 1 m plasma turrets.
        static readonly float3 Stretch = new float3(4f / 3, 1.15f, 4f / 3), Corner = -.5f;

        // ------------------------------------------------------------------------------------------------------------ mesh
        sealed class Builder
        {
            public readonly List<Vector3> V = new(); public readonly List<Vector3> N = new(); public readonly List<Vector2> U = new(); public readonly List<int> T = new();
            public void Quad(float3 a, float3 b, float3 c, float3 d, float3 inside)
            {
                float3 n = math.normalize(math.cross(b - a, c - a));
                if (math.dot(n, (a + c) * .5f - inside) < 0) n = -n;
                int i = V.Count;
                foreach (var p in new[] { a, b, c, d }) { V.Add(p); N.Add(n); U.Add(new Vector2(p.x + p.z, p.y + p.z)); }
                T.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            public void Fan(IList<float3> ring, float3 n)
            {
                int i = V.Count;
                foreach (var p in ring) { V.Add(p); N.Add(n); U.Add(new Vector2(p.x, p.z + p.y)); }
                for (int k = 1; k + 1 < ring.Count; k++) T.AddRange(new[] { i, i + k + 1, i + k });
            }
            /// A prism: the convex polygon (counter-clockwise seen from +axis) extruded from a0 to a1 along axis.
            public void Prism(IList<float2> poly, int axis, float a0, float a1, Func<float2, float, float3> place)
            {
                int n = poly.Count;
                var lo = new float3[n]; var hi = new float3[n];
                for (int k = 0; k < n; k++) { lo[k] = place(poly[k], a0); hi[k] = place(poly[k], a1); }
                float3 inside = 0; for (int k = 0; k < n; k++) inside += (lo[k] + hi[k]) / (2f * n);
                for (int k = 0; k < n; k++) { int j = (k + 1) % n; Quad(lo[k], lo[j], hi[j], hi[k], inside); }
                float3 axisN = 0; axisN[axis] = 1;
                var top = new List<float3>(hi); Fan(top, axisN);
                var bottom = new List<float3>(lo); bottom.Reverse(); Fan(bottom, -axisN);
            }
            public void Cylinder(float2 centreXY, float r, float z0, float z1, int segments = 16)
            {
                int i0 = V.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float a = 2 * math.PI * s / segments; float2 d = new float2(math.cos(a), math.sin(a));
                    foreach (float z in new[] { z0, z1 })
                    {
                        V.Add(new Vector3(centreXY.x + d.x * r, centreXY.y + d.y * r, z)); N.Add(new Vector3(d.x, d.y, 0)); U.Add(new Vector2(s / (float)segments, z));
                    }
                }
                for (int s = 0; s < segments; s++) { int a = i0 + 2 * s; T.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 }); }
                var ring0 = new List<float3>(); var ring1 = new List<float3>();
                for (int s = 0; s < segments; s++)
                {
                    float a = 2 * math.PI * s / segments; float3 p = new float3(centreXY.x + math.cos(a) * r, centreXY.y + math.sin(a) * r, 0);
                    ring1.Add(new float3(p.x, p.y, z1)); ring0.Insert(0, new float3(p.x, p.y, z0));
                }
                Fan(ring1, new float3(0, 0, 1)); Fan(ring0, new float3(0, 0, -1));
            }
        }
        static float2[] Octagon(float2 c, float r)
        {
            var o = new float2[8];
            for (int k = 0; k < 8; k++) { float a = math.PI / 8 + k * math.PI / 4; o[k] = c + r * new float2(math.cos(a), math.sin(a)); }
            return o;
        }
        internal static Mesh SiegeCannonMesh()
        {
            var b = new Builder();
            // Plinth: an octagonal ring under the gunhouse (x, z plane, extruded up y).
            b.Prism(Octagon(new float2(1, 1), 1.45f), 1, -.5f, .1f, (p, y) => new float3(p.x, y, p.y));
            // Gunhouse: side profile (z, y) with a sloped glacis, extruded across x; a narrower roof deck on top.
            float2[] house = { new(-.6f, .1f), new(2.3f, .1f), new(2.3f, .55f), new(1.55f, 1.25f), new(-.6f, 1.25f) };
            b.Prism(house, 0, -.35f, 2.35f, (p, x) => new float3(x, p.y, p.x));
            float2[] roof = { new(-.4f, 1.25f), new(1.2f, 1.25f), new(.95f, 1.45f), new(-.4f, 1.45f) };
            b.Prism(roof, 0, .15f, 1.85f, (p, x) => new float3(x, p.y, p.x));
            // Rear bustle (ammunition) and a rangefinder bar across the roof.
            b.Prism(new float2[] { new(-1.05f, .2f), new(-.6f, .2f), new(-.6f, 1.05f), new(-1.05f, .9f) }, 0, .2f, 1.8f, (p, x) => new float3(x, p.y, p.x));
            b.Prism(new float2[] { new(.3f, 1.45f), new(.55f, 1.45f), new(.55f, 1.6f), new(.3f, 1.6f) }, 0, -.55f, 2.55f, (p, x) => new float3(x, p.y, p.x));
            // Twin barrels: mantlet sleeves, long tubes, muzzle brakes.
            foreach (float x in new[] { .58f, 1.42f })
            {
                b.Cylinder(new float2(x, .65f), .3f, 1.9f, 2.9f);
                b.Cylinder(new float2(x, .65f), .17f, 2.9f, 6.15f);
                b.Cylinder(new float2(x, .65f), .25f, 6.15f, 6.6f);
            }
            // Every face carries its outward normal; wind each triangle to face it (Unity: front = cross(b-a, c-a)).
            for (int t = 0; t < b.T.Count; t += 3)
            {
                Vector3 a = b.V[b.T[t]], c1 = b.V[b.T[t + 1]], c2 = b.V[b.T[t + 2]];
                if (Vector3.Dot(Vector3.Cross(c1 - a, c2 - a), b.N[b.T[t]] + b.N[b.T[t + 1]] + b.N[b.T[t + 2]]) < 0)
                    (b.T[t + 1], b.T[t + 2]) = (b.T[t + 2], b.T[t + 1]);
            }
            for (int k = 0; k < b.V.Count; k++)
            {
                b.V[k] = (Vector3)(((float3)b.V[k] - Corner) * Stretch + Corner);
                b.N[k] = (Vector3)math.normalize((float3)b.N[k] / Stretch);
            }
            var mesh = new Mesh { name = "siege_cannon" };
            mesh.SetVertices(b.V); mesh.SetNormals(b.N); mesh.SetUVs(0, b.U); mesh.SetTriangles(b.T, 0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------------------------------------------------- assets
        static Mesh SaveMesh(Mesh mesh)
        {
            string path = Meshes + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        static void Block(string template, string id, string name, string description, Mesh prop, Action<SerializedObject> edit)
        {
            string path = Blocks + id + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !AssetDatabase.CopyAsset(Blocks + template + ".asset", path))
                throw new InvalidOperationException("copy " + template);
            var asset = AssetDatabase.LoadMainAssetAtPath(path); var so = new SerializedObject(asset);
            so.FindProperty("id").stringValue = id; so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = description; so.FindProperty("formerIds").arraySize = 0;
            so.FindProperty("propMesh").objectReferenceValue = prop;
            edit(so);
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
        static void Cells(SerializedProperty list, IEnumerable<int3> cells)
        {
            list.arraySize = 0;
            foreach (var c in cells)
            {
                list.arraySize++;
                var e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("x").intValue = c.x; e.FindPropertyRelative("y").intValue = c.y; e.FindPropertyRelative("z").intValue = c.z;
            }
        }
        static void Set(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field) ?? throw new InvalidOperationException("No field " + field);
            if (p.propertyType == SerializedPropertyType.Float) p.floatValue = value;
            else if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0;
            else p.intValue = (int)value;
        }
        static void Set(SerializedObject so, string field, bool value) => Set(so, field, value ? 1f : 0f);
    }
}
