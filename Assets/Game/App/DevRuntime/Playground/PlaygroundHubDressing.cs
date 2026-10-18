#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Presentation.Racing;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Game.App.DevRuntime.Playground.PlaygroundHub;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>DEV-022: what the ARK Proving Grounds look like, in the race track's hologram language (Game/Race/Holo, the track's
    /// materials): dot-matrix floor signs, a dashed ring round every F pad, each district's perimeter and beacons, lanes from the
    /// plaza's portals out to the districts, the traffic loops, light on the hub station and the district pylons, and the flight
    /// school's gates (the next one lit). Nothing here is simulated; Root is placed at the playground origin each frame.</summary>
    internal sealed class PlaygroundHubDressing : IDisposable
    {
        internal readonly GameObject Root;
        readonly TrackLook look;
        readonly List<Mesh> meshes = new();
        readonly MaterialPropertyBlock block = new();
        readonly List<MeshRenderer> gates = new();
        int litGate = -2;

        internal PlaygroundHubDressing(TrackLook look, Transform parent)
        {
            this.look = look;
            Root = new GameObject("Playground hub dressing"); Root.transform.SetParent(parent, false);
            if (look.Solid == null || look.Glow == null) { Game.Core.GameLog.Warn(Game.Core.LogCategories.Dev, "Playground hub: the race look materials are missing"); return; }
            Signs(); Pads(); Districts(); Lanes(); Lights(); CoreWall(); CourseGates();
        }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root);
            foreach (var m in meshes) if (m != null) UnityEngine.Object.Destroy(m);
            meshes.Clear(); gates.Clear();
        }

        /// <summary>Lights the flight school gate `index` (−1: none, all dim).</summary>
        internal void SetCourseGate(int index)
        {
            if (index == litGate) return;
            litGate = index;
            for (int i = 0; i < gates.Count; i++) Tint(gates[i], i == index ? Flight.Colour * 3f : Flight.Colour * .35f);
        }

        // ------------------------------------------------------------------------------------------------ building
        static Vector3 V(float2 p, float y = -.8f) => new Vector3(p.x, y, p.y);
        static Vector3 Forward(float yaw) => new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        static Vector3 RightOf(float yaw) => new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));

        MeshRenderer Add(string name, HoloMesh b, Material material, Color tint)
        {
            if (b.Count == 0 || material == null) return null;
            var mesh = b.ToMesh(name); meshes.Add(mesh);
            var go = new GameObject(name); go.transform.SetParent(Root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Tint(r, tint);
            return r;
        }
        void Tint(MeshRenderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(block); block.SetColor("_Color", c); r.SetPropertyBlock(block);
        }

        static void Floor(HoloMesh b, string text, float2 at, float yaw, float pixel, Color colour, HoloAlign align = HoloAlign.Centre)
            => b.Text(text, V(at, -.7f), RightOf(yaw), -Forward(yaw), pixel, colour, (HoloMesh.Align)align);

        void Signs()
        {
            var b = new HoloMesh();
            foreach (var s in PlaygroundHub.Signs()) Floor(b, s.Text, s.Position, s.Yaw, s.Pixel, s.Colour, s.Align);
            Add("hub signs", b, look.Solid, Color.white * 1.6f);
        }

        /// Every F pad: a dashed ring of dots at its trigger radius and an inner ring; reset pads in red.
        void Pads()
        {
            var b = new HoloMesh();
            foreach (var st in Stations)
            {
                bool reset = st.Kind == StationKind.ResetDistrict || st.Kind == StationKind.ResetAll;
                var c = reset ? ResetColour : st.District?.Colour ?? PlazaColour;
                b.DotRing(V(st.Position), StationRadius - 1, 40, 1.1f, c, 4);
                b.DotRing(V(st.Position), StationRadius - 4, 28, .6f, c * .55f);
            }
            Add("hub pads", b, look.Solid, Color.white * 1.8f);
        }

        /// Each district: a dashed perimeter, a blinking beacon every 45° on it, its brake box and boost strip outlines.
        void Districts()
        {
            var ring = new HoloMesh(); var glows = new HoloMesh();
            foreach (var d in PlaygroundHub.Districts)
            {
                int dots = Mathf.RoundToInt(d.Radius * 2 * Mathf.PI / 7);
                ring.DotRing(V(d.Centre), d.Radius, dots, 2, d.Colour * .6f, 5);
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4;
                    glows.Glow(V(d.Centre + new float2(Mathf.Sin(a), Mathf.Cos(a)) * d.Radius, 2), 6, d.Colour, 1, k * .125f);
                }
                if (d == Combat) ring.DotRing(V(d.Centre), 220, 160, 1.4f, d.Colour * .5f, 3);
            }
            // flight school: the brake box and the boost strip's edge
            Box(ring, Flight.At(-175, 120), Flight.Yaw, 44, 44, Flight.Colour * .9f);
            Box(ring, Flight.At(175, -40), Flight.Yaw, 44, 180, new Color(.4f, 1f, .55f) * .7f);
            Add("district rings", ring, look.Solid, Color.white * 1.5f);
            Add("district beacons", glows, look.Glow, Color.white * 2.2f);
        }

        static void Box(HoloMesh b, float2 c, float yaw, float width, float length, Color colour)
        {
            Vector3 f = Forward(yaw), r = RightOf(yaw), o = V(c);
            Vector3 p0 = o - r * width * .5f - f * length * .5f, p1 = o + r * width * .5f - f * length * .5f,
                p2 = o + r * width * .5f + f * length * .5f, p3 = o - r * width * .5f + f * length * .5f;
            foreach (var (a, e) in new[] { (p0, p1), (p1, p2), (p2, p3), (p3, p0) })
            {
                int n = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, e) / 4));
                for (int i = 0; i < n; i++) { var p = Vector3.Lerp(a, e, (i + .5f) / n); b.Dot(p - (r + f) * .75f, r * 1.5f, f * 1.5f, colour); }
            }
        }

        /// The lanes: a surface ribbon from each plaza portal out to its district, and the inner traffic loop.
        void Lanes()
        {
            var lanes = new HoloMesh();
            foreach (var (a, e, width, _) in PlaygroundHub.Lanes()) Ribbon(lanes, new[] { a, e }, width, false);
            Add("hub lanes", lanes, look.Surface, PlazaColour * 2.2f);
            // running lights along each lane's edges, chasing out toward the district
            var chase = new HoloMesh();
            foreach (var (a, e, width, colour) in PlaygroundHub.Lanes())
            {
                float2 dir = math.normalize(e - a), side = new float2(dir.y, -dir.x) * (width * .5f + 3);
                float length = math.distance(a, e);
                for (float t = 0; t <= length; t += 30)
                    foreach (float k in new[] { -1f, 1f }) chase.Glow(V(a + dir * t + side * k, .5f), 2.6f, colour, 1, -t / 300f);
            }
            Add("lane lights", chase, look.Glow, Color.white * 2.2f);
            var traffic = new HoloMesh();
            Ribbon(traffic, Loop(InnerLoop, 96), 36, true);
            Ribbon(traffic, Loop(OuterLoop, 192), 46, true);
            Add("traffic lanes", traffic, look.Surface, new Color(1f, .7f, .3f) * .8f);
        }

        static void Ribbon(HoloMesh b, float2[] pts, float width, bool closed)
        {
            int n = pts.Length, prev = -1; float along = 0, half = width * .5f;
            for (int i = 0; i <= (closed ? n : n - 1); i++)
            {
                float2 p = pts[i % n];
                if (i > 0) along += math.distance(pts[(i - 1) % n], p);
                float2 prevP = closed ? pts[(i - 1 + n) % n] : pts[Mathf.Max(0, i - 1)], nextP = closed ? pts[(i + 1) % n] : pts[Mathf.Min(n - 1, i + 1)];
                float2 d = math.normalize(nextP - prevP), normal = new float2(-d.y, d.x) * half;
                int k = b.Count;
                b.Vertex(V(p - normal, -.9f), Color.white, new Vector2(0, along), new Vector2(width, 0));
                b.Vertex(V(p + normal, -.9f), Color.white, new Vector2(1, along), new Vector2(width, 0));
                if (prev >= 0) b.Quad(prev, prev + 1, k + 1, k);
                prev = k;
            }
        }

        /// Light: a ring of lamps round the hub station, its spire, the district pylons, the portals' posts and the traffic loop's edge.
        void Lights()
        {
            var g = new HoloMesh(); var solid = new HoloMesh();
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                g.Glow(V(Plaza + new float2(Mathf.Sin(a), Mathf.Cos(a)) * (CoreRadius + 2), 5), i % 4 == 0 ? 6 : 3.5f, PlazaColour, i % 4 == 0 ? 1 : 0, i / 48f);
            }
            g.Glow(V(Plaza, 18), 16, new Color(1f, .85f, .5f), 1, 0);
            g.Glow(V(Plaza, 9), 9, PlazaColour, 2, .3f);
            foreach (var d in PlaygroundHub.Districts)
                foreach (float side in new[] { -70f, 70f })
                {
                    float2 p = d.At(side, -(d.Radius - 40));
                    g.Glow(V(p, 18), 8, d.Colour, 0, 0);
                    solid.Ring(V(p, 0), 8, 4.3f, 1, 2.2f, d.Colour);
                    solid.Ring(V(p, 0), 8, 4.3f, 9, 10, d.Colour * .7f);
                }
            foreach (var p in Loop(InnerLoop + 20, 48)) g.Glow(V(p, 1), 2.5f, new Color(1f, .65f, .25f), 1, (p.x + p.y) * .001f);
            foreach (var p in Loop(InnerLoop - 20, 48)) g.Glow(V(p, 1), 2.5f, new Color(1f, .65f, .25f), 1, (p.x + p.y) * .001f + .5f);
            Add("hub lights", g, look.Glow, Color.white * 2.4f);
            Add("hub light bands", solid, look.Solid, Color.white * 2f);
        }

        /// "ARK PROVING GROUNDS" round the hub station's outer wall, on the side facing the spawn, and lamps on each gate's posts.
        void CoreWall()
        {
            var b = new HoloMesh();
            Arc(b, "ARK PROVING GROUNDS", Mathf.PI, CoreRadius + .6f, .9f, -1f, PlazaColour);
            Arc(b, "PORTALS ALL ROUND · FLY OUT THROUGH ONE", 0, CoreRadius + .6f, .6f, -1f, PlazaColour * .8f);
            Arc(b, "SHIPYARD · MARKET · SALVAGE", Mathf.PI / 2, CoreRadius + .6f, .6f, -1f, PlazaColour * .8f);
            Arc(b, "HAZARDS · MECHANISMS · COMBAT", -Mathf.PI / 2, CoreRadius + .6f, .6f, -1f, PlazaColour * .8f);
            Add("hub wall text", b, look.Solid, Color.white * 1.8f);
        }

        /// Vertical text along the outside of a circle round the plaza, centred on bearing `centre`, its middle row at height y.
        static void Arc(HoloMesh b, string text, float centre, float radius, float pixel, float y, Color colour)
        {
            float step = PixelFont.Advance * pixel / radius, start = centre + (text.Length - 1) * step * .5f;
            for (int i = 0; i < text.Length; i++)
            {
                float a = start - i * step;   // seen from outside, reading runs to decreasing bearing
                var at = V(Plaza + new float2(Mathf.Sin(a), Mathf.Cos(a)) * radius, y);
                var right = new Vector3(-Mathf.Cos(a), 0, Mathf.Sin(a));
                b.Text(text[i].ToString(), at, right, Vector3.down, pixel, colour);
            }
        }

        /// The flight school's gates: a dot ring 18 m across each with its number; SetCourseGate lights the next.
        void CourseGates()
        {
            for (int i = 0; i < Course.Length; i++)
            {
                var b = new HoloMesh();
                b.DotRing(V(Course[i]), 18, 40, 1.5f, Color.white);
                b.Text((i + 1).ToString(), V(Course[i], -.7f), RightOf(Flight.Yaw), -Forward(Flight.Yaw), 1f, Color.white);
                gates.Add(Add("course gate " + (i + 1), b, look.Solid, Flight.Colour * .35f));
            }
            SetCourseGate(-1);
        }
    }
}
#endif
