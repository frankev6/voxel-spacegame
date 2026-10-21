#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>Feel prototype (owner, 2026-10-21: "one planet with its orbit coast"; docs/design/exploration/world-and-scale.md). The
    /// Hollow orbits VESHA, a planet whose surface lies <see cref="Depth"/> below the flight plane: never reachable, never in the way,
    /// always under you. Its coast is the Clan's tether, rising from the surface to a beacon-lit anchor in the plane south of the
    /// Hollow, with wrecks drifting round it (spawned by <see cref="PlaygroundHollow"/>). Plain GameObjects under the Hollow's
    /// dressing, which follows the floating origin. Tune live: `playground orbit=radius,depth`.</summary>
    internal sealed class HollowOrbit
    {
        internal const string Name = "VESHA";
        /// <summary>Where the tether meets the flight plane, Hollow-local; the planet's centre is straight below it.</summary>
        internal static readonly float2 Tether = new float2(150, -4300);
        internal const float CoastRadius = 1600;   // within this of the tether the player is "over the coast"
        [Game.Core.StaticStateOk("feel prototype tuning, main thread")] internal static float Radius = 80000, Depth = 2500;
        // 80 km: from the plane the horizon dips 14°, a curve the chase camera frames; the cap reaches past it from anywhere near the Hollow

        readonly List<Object> owned = new();
        GameObject root;

        /// <summary>The tether under the Hollow's dressing. The planet itself is drawn by <see cref="HollowSystem"/> (DEV-028) with the
        /// rest of the star system, from <see cref="Radius"/> and <see cref="Depth"/>.</summary>
        internal void Build(Transform dressing)
        {
            root = new GameObject("Hollow orbit (VESHA's tether)"); root.transform.SetParent(dressing, false);
            BuildTether();
        }

        /// <summary>After a live tuning change: the tether is rebuilt to the new depth (the planet follows on the next frame).</summary>
        internal void Place()
        {
            if (root == null) return;
            var dressing = root.transform.parent;
            Dispose();
            Build(dressing);
        }

        /// <summary>The tether: a pale cable banded with lit collars from the surface to the plane, and its anchor hub; its beacons are
        /// <see cref="Lights"/>.</summary>
        void BuildTether()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return;
            var cableMaterial = Own(new Material(lit) { name = "orbit tether" });
            cableMaterial.SetColor("_BaseColor", new Color(.42f, .44f, .47f)); cableMaterial.SetFloat("_Smoothness", .6f); cableMaterial.SetFloat("_Metallic", .55f);
            var collarMaterial = Own(new Material(lit) { name = "orbit tether collar" });
            collarMaterial.SetColor("_BaseColor", new Color(.1f, .12f, .14f)); collarMaterial.EnableKeyword("_EMISSION");
            collarMaterial.SetColor("_EmissionColor", new Color(.35f, .8f, 1.2f) * 2.2f);
            var top = new Vector3(Tether.x, 30, Tether.y);
            float length = Depth + 30 + 200;   // into the haze below the surface top
            var cable = Primitive(PrimitiveType.Cylinder, "tether cable", cableMaterial);
            cable.localPosition = top - Vector3.up * length * .5f; cable.localScale = new Vector3(10, length * .5f, 10);
            // lit collars every 90 m: the cable reads as a structure falling away into the air, not a line drawn on the screen
            for (float y = 60; y < Depth; y += 90)
            {
                var collar = Primitive(PrimitiveType.Cylinder, "tether collar", collarMaterial);
                collar.localPosition = top - Vector3.up * y; collar.localScale = new Vector3(14, 1.6f, 14);
            }
            var hub = Primitive(PrimitiveType.Cylinder, "anchor hub", cableMaterial);
            hub.localPosition = top; hub.localScale = new Vector3(60, 6, 60);
            var ring = Primitive(PrimitiveType.Cylinder, "anchor ring", collarMaterial);
            ring.localPosition = top + Vector3.up * 3.2f; ring.localScale = new Vector3(64, .6f, 64);
            var lamp = new GameObject("anchor lamp").AddComponent<UnityEngine.Light>(); lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = top + Vector3.up * 40; lamp.type = LightType.Point; lamp.range = 260; lamp.intensity = 6; lamp.color = new Color(.55f, .8f, 1f);
        }

        /// <summary>The tether's lights, as sprites in the Hollow's own glow mesh (dressing-local): amber beacons down the cable, pulsing,
        /// and a cyan ring round the anchor where ships would dock to the lift.</summary>
        internal static void Lights(Game.Presentation.Racing.HoloMesh glow)
        {
            var top = new Vector3(Tether.x, 30, Tether.y);
            for (float y = 0; y < Depth; y += 180) glow.Glow(top + new Vector3(9, -y - 20, 0), y < 10 ? 9 : 7, new Color(1f, .55f, .2f), 1, y * .0013f);
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * 2 * math.PI;
                glow.Glow(top + new Vector3(math.cos(a), 0, math.sin(a)) * 60, 5, new Color(.45f, .85f, 1f), 0, i * .04f);
            }
        }

        Transform Primitive(PrimitiveType type, string name, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(root.transform, false);
            Object.Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        T Own<T>(T o) where T : Object { owned.Add(o); return o; }

        internal void Dispose()
        {
            if (root != null) Object.Destroy(root);
            foreach (var o in owned) if (o != null) Object.Destroy(o);
            owned.Clear(); root = null;
        }
    }
}
#endif
