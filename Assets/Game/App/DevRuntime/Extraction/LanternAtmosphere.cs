#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Game.App.DevRuntime.Extraction.LanternLayout;

namespace Game.App.DevRuntime.Extraction
{
    /// <summary>EXT-014 Tollhouse Nine's atmosphere (owner, 2026-11-10: "make the inside of the station feel truly spectacle in terms of
    /// atmosphere, lighting, it should feel iconic"). Presentation only; nothing here touches the simulation.
    /// <list type="bullet">
    /// <item>The Well of lanterns: hundreds of paper lanterns rise up the Well in a slow spiral and spill out of its mouth into space;
    /// strings of small lanterns cross the Well at every market floor.</item>
    /// <item>The searchlights: a visible beam on every Light watcher, sweeping exactly as the watcher does (WatchSystem's yaw), so the
    /// beam you see is the cone that sees you.</item>
    /// <item>The Lantern itself: the core's cyan halo beats like a heart; with the Grate's panel lifted it shines up the Well as a
    /// column of light (a thin leak through the bars while it is shut).</item>
    /// <item>The blackout: when the market loses power its lanterns go out as a line climbing the Well, the grade drops and cools,
    /// and on the alarm red strobes turn in the galleries and the Cradle.</item>
    /// <item>A warm, teal-shadowed grade with stronger bloom while the camera is inside the town.</item>
    /// </list></summary>
    internal sealed class LanternAtmosphere : IDisposable
    {
        const int Lanterns = 380, StringsPerFloor = 3, Beads = 20;
        const float RiseBottom = MarketBottom + 4, RiseTop = 96;
        sealed class Beam { public Light Light; public float3 At, Facing; public float Sweep, Period, Phase, Range, HalfArc; public ushort District; }

        readonly Transform parent;
        readonly Material paper, beam, column, halo;
        readonly Mesh lantern, spotCone, wideCone, ball;
        readonly Matrix4x4[] batch = new Matrix4x4[1023];
        readonly float[] radius = new float[Lanterns], angle = new float[Lanterns], start = new float[Lanterns], rate = new float[Lanterns],
            size = new float[Lanterns], yaw = new float[Lanterns], swirl = new float[Lanterns];
        readonly List<Matrix4x4> beads = new();
        readonly List<Beam> searchlights = new();
        readonly List<Light> strobes = new();
        readonly bool[] lit = new bool[8];
        readonly GameObject volumeObject;
        readonly VolumeProfile profile;
        readonly Volume volume;
        readonly ColorAdjustments grade; readonly Bloom bloom; readonly Vignette vignette; readonly SplitToning split;
        float darkLine = float.NegativeInfinity, inside, dark, alarm, column01;
        static readonly int DarkBelowId = Shader.PropertyToID("_DarkBelow"), LitId = Shader.PropertyToID("_Lit"), IntensityId = Shader.PropertyToID("_Intensity");

        /// <summary>The core's beat, 0.75..1.35, for its lamp.</summary>
        internal float Heartbeat { get; private set; } = 1;

        internal LanternAtmosphere(Transform parent)
        {
            this.parent = parent;
            paper = Make("Game/Vfx/PaperLantern", "lantern paper");
            beam = Make("Game/Vfx/LightShaft", "searchlight beam"); beam?.SetColor("_Color", new Color(2.0f, 1.8f, 1.45f)); beam?.SetFloat("_NearFade", 18);
            column = Make("Game/Vfx/LightShaft", "the Lantern's column"); column?.SetColor("_Color", new Color(.7f, 2.4f, 2.7f)); column?.SetFloat("_Dust", .7f);
            halo = Make("Game/Vfx/LightShaft", "the Lantern's halo"); halo?.SetColor("_Color", new Color(.5f, 1.4f, 1.5f)); halo?.SetFloat("_Dust", .35f); halo?.SetFloat("_NearFade", 6);
            lantern = Lathe("paper lantern", 22, 16, t =>
            {
                float r = t < .06f ? math.lerp(0, .2f, t / .06f) : t > .94f ? math.lerp(.2f, 0, (t - .94f) / .06f) : .2f + .3f * math.pow(math.sin(math.PI * (t - .06f) / .88f), .7f);
                return new float2(r, t - .5f);
            }, -1, new Bounds(Vector3.zero, Vector3.one));
            spotCone = Lathe("searchlight cone", 10, 24, t => new float2(math.lerp(.03f, 1, t), t), -1, new Bounds(new Vector3(0, .5f, 0), new Vector3(2, 1, 2)));
            wideCone = Lathe("lantern column", 12, 32, t => new float2(math.lerp(.3f, 1, t), t), -1, new Bounds(new Vector3(0, .5f, 0), new Vector3(2, 1, 2)));
            ball = Lathe("halo", 16, 24, t => new float2(.5f * math.sin(math.PI * t), -.5f * math.cos(math.PI * t)), .3f, new Bounds(Vector3.zero, Vector3.one));

            var random = new Unity.Mathematics.Random(0x7011_9u);
            for (int i = 0; i < Lanterns; i++)
            {
                radius[i] = math.sqrt(random.NextFloat(.02f, 1)) * (WellRadius - 8);
                angle[i] = random.NextFloat(0, 2 * math.PI); start[i] = random.NextFloat();
                rate[i] = random.NextFloat(1.3f, 2.6f) / (RiseTop - RiseBottom);   // 1.3..2.6 m/s up
                size[i] = random.NextFloat(2.6f, 5f); yaw[i] = random.NextFloat(0, 360);
                swirl[i] = random.NextFloat(.012f, .03f);
            }
            // the strings: three chords across the Well at every market floor, sagging, a small lantern every few metres
            for (int f = 0; f < 6; f++)
            {
                float y = MarketBottom + (f + .9f) * (CrownDeck - MarketBottom) / 6;
                for (int k = 0; k < StringsPerFloor; k++)
                {
                    float a0 = math.radians(f * 37 + k * 120), a1 = a0 + math.radians(135);
                    var p0 = new float3(math.cos(a0), 0, math.sin(a0)) * (WellRadius + 2) + new float3(0, y, 0);
                    var p1 = new float3(math.cos(a1), 0, math.sin(a1)) * (WellRadius + 2) + new float3(0, y, 0);
                    for (int b = 1; b < Beads; b++)
                    {
                        float u = b / (float)Beads;
                        var p = math.lerp(p0, p1, u) - new float3(0, 11 * 4 * u * (1 - u), 0);
                        beads.Add(Matrix4x4.TRS((Vector3)p, Quaternion.Euler(0, random.NextFloat(0, 360), 0), Vector3.one * random.NextFloat(1.6f, 2.2f)));
                    }
                }
            }
            // emergency strobes: battery lamps, on through a blackout, only on the alarm
            for (int i = 0; i < 8; i++)
            {
                float a = math.radians(i * 45 + 22.5f), y = i % 2 == 0 ? -100 : -40;
                strobes.Add(ExtractionKit.Lamp(parent, new Vector3(math.cos(a) * (WellRadius + 6), y, math.sin(a) * (WellRadius + 6)), new Color(1, .12f, .08f), 110, 0));
            }
            for (int i = 0; i < 4; i++)
            {
                float a = math.radians(i * 90 + 45);
                strobes.Add(ExtractionKit.Lamp(parent, (Vector3)(LanternLayout.Core + new float3(math.cos(a) * 52, 14, math.sin(a) * 52)), new Color(1, .12f, .08f), 90, 0));
            }
            foreach (var s in strobes) s.enabled = false;

            profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Tollhouse Nine";
            grade = profile.Add<ColorAdjustments>(); grade.postExposure.Override(0); grade.contrast.Override(12); grade.saturation.Override(8); grade.colorFilter.Override(Color.white);
            bloom = profile.Add<Bloom>(); bloom.intensity.Override(1.1f); bloom.threshold.Override(1f); bloom.scatter.Override(.74f); bloom.tint.Override(new Color(1, .87f, .74f));
            vignette = profile.Add<Vignette>(); vignette.intensity.Override(.28f); vignette.smoothness.Override(.5f);
            split = profile.Add<SplitToning>(); split.shadows.Override(new Color(.32f, .55f, .58f)); split.highlights.Override(new Color(.95f, .74f, .5f)); split.balance.Override(-18);
            volumeObject = new GameObject("Tollhouse Nine volume"); volumeObject.transform.SetParent(parent, false);
            volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 60; volume.weight = 0; volume.sharedProfile = profile;
        }

        static Material Make(string shader, string name)
        {
            var s = Shader.Find(shader);
            return s == null ? null : new Material(s) { name = name, enableInstancing = true };
        }

        /// <summary>A lathe about y: `profile(t)` gives (radius, y) for t 0..1; uv.y is t (or `v` when v >= 0).</summary>
        static Mesh Lathe(string name, int rings, int segments, Func<float, float2> profile, float v, Bounds bounds)
        {
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                var p = profile(t); var d = profile(math.min(1, t + .01f)) - profile(math.max(0, t - .01f));
                var n = math.normalizesafe(new float2(d.y, -d.x), new float2(0, t < .5f ? -1 : 1));
                for (int j = 0; j <= segments; j++)
                {
                    float a = j / (float)segments * 2 * math.PI, c = math.cos(a), s = math.sin(a);
                    verts.Add(new Vector3(c * p.x, p.y, s * p.x)); normals.Add(new Vector3(c * n.x, n.y, s * n.x));
                    uvs.Add(new Vector2(j / (float)segments, v >= 0 ? v : t));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segments; j++)
                {
                    int a = i * (segments + 1) + j, b = a + segments + 1;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.bounds = bounds;
            return mesh;
        }

        internal void Searchlight(Light light, float3 at, float3 facing, float sweepRadians, float sweepSeconds, float phase, float range, float halfArcDeg, ushort district)
            => searchlights.Add(new Beam { Light = light, At = at, Facing = math.normalizesafe(facing, new float3(0, 0, 1)), Sweep = sweepRadians, Period = sweepSeconds,
                Phase = phase, Range = range, HalfArc = math.radians(halfArcDeg), District = district });

        internal void SetLit(ushort district, bool on) { if (district < lit.Length) lit[district] = on; }

        /// <summary>Once a frame. `simTime` is the watchers' clock (tick × the fixed step); `core` the core's render-space position, with
        /// `coreHome` when it hangs in the Cradle and `coreShown` when it is not stowed; `grateOpen` 0..1.</summary>
        internal void Tick(float dt, float simTime, Camera camera, Vector3 core, bool coreHome, bool coreShown, bool shrouded, float grateOpen, bool alarmOn)
        {
            var origin = parent.position;
            var eye = camera != null ? camera.transform.position : origin + Vector3.up * 1e5f;
            float t = Time.time;
            dt = math.clamp(dt, 0, .1f);

            // the beat: lub-dub, 0.8 Hz
            float phase = t * .8f % 1;
            float beat = math.exp(-math.pow((phase - .1f) / .05f, 2)) + .6f * math.exp(-math.pow((phase - .3f) / .06f, 2));
            Heartbeat = .75f + .6f * beat;

            // how dark the town is, and the line of lanterns going out
            int on = 0; for (ushort d = 1; d <= 6; d++) if (lit[d]) on++;
            dark = Approach(dark, 1 - on / 6f, dt * .8f);
            alarm = Approach(alarm, alarmOn ? 1 : 0, dt * 1.5f);
            if (!lit[2]) darkLine = math.min(RiseTop + 20, (float.IsNegativeInfinity(darkLine) ? RiseBottom - 10 : darkLine) + 24 * dt);
            else if (!float.IsNegativeInfinity(darkLine)) { darkLine = math.min(darkLine, RiseTop + 20) - 45 * dt; if (darkLine < RiseBottom - 12) darkLine = float.NegativeInfinity; }

            if (paper != null)
            {
                paper.SetFloat(DarkBelowId, float.IsNegativeInfinity(darkLine) ? -1e7f : origin.y + darkLine);
                paper.SetFloat(LitId, 1);
                var rp = new RenderParams(paper) { worldBounds = new Bounds(origin, Vector3.one * 1400), shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                int n = 0;
                for (int i = 0; i < Lanterns; i++)
                {
                    float y = RiseBottom + (start[i] + t * rate[i]) % 1 * (RiseTop - RiseBottom);
                    float grow = math.smoothstep(0, 8, y - RiseBottom) * (1 - math.smoothstep(RiseTop - 30, RiseTop, y));
                    if (grow < .02f) continue;
                    float r = radius[i] + 2.5f * math.sin(t * .23f + i) + math.max(0, y - 16) * .35f;
                    float a = angle[i] + t * swirl[i];
                    var p = origin + new Vector3(math.cos(a) * r, y + .8f * math.sin(t * .9f + i * 1.7f), math.sin(a) * r);
                    grow *= math.smoothstep(10, 34, Vector3.Distance(p, eye));   // they part around the lens and the ship it follows
                    if (grow < .02f) continue;
                    var rot = Quaternion.Euler(0, yaw[i], 0) * Quaternion.Euler(4 * math.sin(t * .7f + i), 0, 0);
                    batch[n++] = Matrix4x4.TRS(p, rot, new Vector3(.8f, 1, .8f) * size[i] * grow);
                }
                if (n > 0) Graphics.RenderMeshInstanced(rp, lantern, 0, batch, n);
                n = 0;
                var shift = Matrix4x4.Translate(origin);
                foreach (var m in beads) batch[n++] = shift * m;
                if (n > 0) Graphics.RenderMeshInstanced(rp, lantern, 0, batch, n);
            }

            // searchlights: the light and its beam follow the watcher's sweep
            {
                int n = 0;
                foreach (var s in searchlights)
                {
                    float yawNow = s.Period > 0 ? s.Sweep * math.sin(2 * math.PI * (simTime / s.Period + s.Phase)) : 0;
                    float3 dir = math.mul(quaternion.RotateY(yawNow), s.Facing);
                    if (s.Light != null) s.Light.transform.rotation = Quaternion.LookRotation((Vector3)dir);
                    if (!lit[s.District] || beam == null) continue;
                    float length = s.Range * .8f, width = math.tan(s.HalfArc) * length;
                    batch[n++] = Matrix4x4.TRS(origin + (Vector3)s.At, Quaternion.FromToRotation(Vector3.up, (Vector3)dir), new Vector3(width, length, width));
                }
                if (n > 0)
                {
                    beam.SetFloat(IntensityId, .16f * (1 + 1.5f * dark));
                    Graphics.RenderMeshInstanced(new RenderParams(beam) { worldBounds = new Bounds(origin, Vector3.one * 1400), shadowCastingMode = ShadowCastingMode.Off }, spotCone, 0, batch, n);
                }
            }

            // the Lantern: a beating halo, and the column up the Well when the Grate is open (a leak through its bars when shut)
            column01 = Approach(column01, !coreHome || shrouded ? 0 : math.lerp(.16f, 1, math.saturate(grateOpen)), dt * .9f);
            if (halo != null && coreShown)
            {
                halo.SetFloat(IntensityId, (shrouded ? .12f : .55f) * Heartbeat);
                batch[0] = Matrix4x4.TRS(core, Quaternion.identity, Vector3.one * CoreRadius * 2.8f);
                Graphics.RenderMeshInstanced(new RenderParams(halo) { worldBounds = new Bounds(core, Vector3.one * 200), shadowCastingMode = ShadowCastingMode.Off }, ball, 0, batch, 1);
            }
            if (column != null && column01 > .01f)
            {
                column.SetFloat(IntensityId, (.5f + .9f * dark) * column01 * (.85f + .15f * Heartbeat));
                float bottom = core.y - origin.y + CoreRadius, length = RiseTop + 40 - bottom;
                batch[0] = Matrix4x4.TRS(new Vector3(core.x, core.y + CoreRadius, core.z), Quaternion.identity, new Vector3(WellRadius * .7f, length, WellRadius * .7f));
                Graphics.RenderMeshInstanced(new RenderParams(column) { worldBounds = new Bounds(origin, Vector3.one * 1400), shadowCastingMode = ShadowCastingMode.Off }, wideCone, 0, batch, 1);
            }

            // the alarm's strobes: each turns like a beacon, out of step with the next
            for (int i = 0; i < strobes.Count; i++)
            {
                var s = strobes[i]; if (s == null) continue;
                s.enabled = alarm > .02f;
                s.intensity = 5200 * alarm * math.pow(math.abs(math.sin(t * math.PI * .9f + i * .8f)), 6);
            }

            // the grade, while the camera is in the town
            if (camera != null)
            {
                var local = camera.transform.position - origin;
                bool within = new Vector2(local.x, local.z).magnitude < TownRadius + 40 && local.y < 60 && local.y > UnderBottom - 30;
                inside = Approach(inside, within ? 1 : 0, dt * 1.2f);
            }
            volume.weight = inside;
            grade.postExposure.value = math.lerp(-.35f, -1.4f, dark);
            grade.saturation.value = math.lerp(8, -30, dark);
            grade.contrast.value = math.lerp(12, 24, dark);
            grade.colorFilter.value = Color.Lerp(Color.Lerp(new Color(1, .96f, .9f), new Color(.76f, .85f, 1), dark), new Color(1, .74f, .72f), alarm * .55f);
            bloom.intensity.value = math.lerp(.75f, 1.5f, dark);
            vignette.intensity.value = math.lerp(.28f, .44f, math.max(dark, alarm * .6f));
        }

        static float Approach(float from, float to, float step) => from < to ? math.min(to, from + step) : math.max(to, from - step);

        public void Dispose()
        {
            foreach (var o in new UnityEngine.Object[] { paper, beam, column, halo, lantern, spotCone, wideCone, ball, profile, volumeObject })
                if (o != null) UnityEngine.Object.Destroy(o);
        }
    }
}
#endif
