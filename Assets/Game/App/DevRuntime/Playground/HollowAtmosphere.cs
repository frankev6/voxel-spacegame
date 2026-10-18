#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-023 the Hollow's air (owner: "i asked for atmosphere so it doesn't feel dark and empty"): a soft halo of lit dust
    /// round every lamp, dust motes drifting round the player under the roof, and lamps that live (beacons pulse, the welders'
    /// flashers flicker, the rest breathe a little). Plain GameObjects under the Hollow's dressing, which follows the floating origin.</summary>
    internal sealed class HollowAtmosphere
    {
        readonly struct Lamp
        {
            internal readonly UnityEngine.Light Light; internal readonly float Base, Phase; internal readonly int Kind;   // 0 lamp, 1 beacon, 2 flash
            internal Lamp(UnityEngine.Light light, float intensity, float phase, int kind) { Light = light; Base = intensity; Phase = phase; Kind = kind; }
        }

        const float Near = 420;
        readonly List<Lamp> lamps = new();
        readonly List<Object> owned = new();
        Material additive;
        ParticleSystem dust;

        /// <summary>A real light to animate; `kind` "beacon", "flash" or anything else (a lamp).</summary>
        internal void AddLamp(UnityEngine.Light light, string kind, float phase) =>
            lamps.Add(new Lamp(light, light.intensity, phase, kind == "beacon" ? 1 : kind == "flash" ? 2 : 0));

        /// <summary>Halos for the lamps parented under `low` (below the roof) and `roof` (above it, hidden while inside), and the dust.</summary>
        internal void Build(Transform low, Transform roof)
        {
            additive = Additive(); if (additive == null) return;
            var under = new List<(Vector3, Color, float)>(); var over = new List<(Vector3, Color, float)>();
            foreach (var l in lamps)
            {
                // seen from the flight camera ~150 m up: tens of metres of lit air, faint enough to see the rock through
                float size = l.Kind == 1 ? 36 : l.Kind == 2 ? 30 : 24;
                var c = l.Light.color; c.a = l.Kind == 0 ? .15f : .24f;
                (l.Light.transform.parent == roof ? over : under).Add((l.Light.transform.localPosition, c, size));
            }
            Halos(low, "hollow halos", under); Halos(roof, "hollow roof halos", over);
            dust = Dust(low);
        }

        /// <summary>Per frame: the lamps live; the dust follows the player (`player`, dressing-local) and only drifts under the roof.</summary>
        internal void Tick(float time, bool inside, Vector3 player)
        {
            // only the lamps round the player live: the flight camera sees a few hundred metres, and 600 light writes a frame cost
            foreach (var l in lamps)
            {
                if (l.Light == null) continue;
                var at = l.Light.transform.localPosition;
                if ((at.x - player.x) * (at.x - player.x) + (at.z - player.z) * (at.z - player.z) > Near * Near) continue;
                float k = l.Kind == 1 ? .55f + .45f * math.sin(time * 2.2f + l.Phase)
                    : l.Kind == 2 ? (math.frac(math.sin(math.floor(time * 14 + l.Phase * 31) * 12.9898f) * 43758.55f) > .82f ? 1.7f : .12f)
                    : .9f + .1f * math.sin(time * 6.3f + l.Phase) * math.sin(time * 2.9f + l.Phase * 1.7f);
                l.Light.intensity = l.Base * k;
            }
            if (dust == null) return;
            dust.transform.localPosition = new Vector3(player.x, 0, player.z);
            var emission = dust.emission; emission.enabled = inside;
        }

        internal void Dispose()
        {
            foreach (var o in owned) if (o != null) Object.Destroy(o);
            owned.Clear(); lamps.Clear(); dust = null; additive = null;
        }

        void Halos(Transform parent, string name, List<(Vector3 at, Color colour, float size)> halos)
        {
            if (halos.Count == 0) return;
            var ps = System(parent, name, halos.Count, false);
            var particles = new ParticleSystem.Particle[halos.Count];
            for (int i = 0; i < halos.Count; i++)
                particles[i] = new ParticleSystem.Particle { position = halos[i].at, startSize = halos[i].size, startColor = halos[i].colour,
                    remainingLifetime = 1e6f, startLifetime = 1e6f };
            ps.Play(); ps.SetParticles(particles, particles.Length); ps.Pause();
        }

        ParticleSystem Dust(Transform parent)
        {
            var ps = System(parent, "hollow dust", 1500, true);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8, 14); main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 2.2f);   // a few pixels each from the flight camera
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.9f, .96f, 1f, .16f), new Color(.75f, .9f, 1f, .08f));   // pale, lit by LEDs
            var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 150;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(320, 36, 240); shape.position = new Vector3(0, -6, 0);   // the tunnels' air, floor to just above the plane
            var noise = ps.noise; noise.enabled = true; noise.strength = .6f; noise.frequency = .08f; noise.scrollSpeed = .05f;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(1, .75f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            ps.Play();
            return ps;
        }

        ParticleSystem System(Transform parent, string name, int max, bool world)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); owned.Add(go);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = world; main.duration = world ? 5 : 1; main.maxParticles = max;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startLifetime = 1e6f; main.prewarm = world;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = additive; r.renderMode = ParticleSystemRenderMode.Billboard;
            r.maxParticleSize = 10; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        /// <summary>URP's unlit particle shader, additive, with a soft round sprite.</summary>
        Material Additive()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "hollow soft dot", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = math.length(new float2(x + .5f - n / 2f, y + .5f - n / 2f)) / (n / 2f);
                float a = math.saturate(1 - d); a = a * a * (3 - 2 * a); a *= a;
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels); tex.Apply(true, true); owned.Add(tex);
            var m = new Material(shader) { name = "hollow additive", renderQueue = (int)RenderQueue.Transparent }; owned.Add(m);
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }
    }
}
#endif
