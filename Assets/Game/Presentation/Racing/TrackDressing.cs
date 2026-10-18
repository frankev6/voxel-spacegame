using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Hazards;
using UnityEngine;
using UnityEngine.Rendering;
using Builder = Game.Presentation.Racing.HoloMesh;

namespace Game.Presentation.Racing
{
    /// <summary>RACE-016: what a race track looks like, built from its TrackDefinition on every machine. Nothing here is simulated.
    /// Under Root (placed at the track origin each frame by the caller): the lane surface along each route, an energy fence with
    /// emitter posts along every field wall, gate pylons with an energy curtain (the next gate lit), boost pads, corner boards on the
    /// outside of bends, the start grid and the wrecks' lights. The solid parts (emitter posts, gate pylons, debris) are
    /// voxel grids (TrackStructureBuilder); this draws only their light and the energy between them. In world space: mine halos, the guide arrow and space dust round the
    /// camera. Materials come from TrackDefinition.Look (Game/Race/Holo, one per mode, and a lit metal).</summary>
    public sealed class TrackDressing : IDisposable
    {
        public readonly GameObject Root;
        readonly GameObject world;
        readonly TrackLook look;
        readonly List<Mesh> meshes = new();
        readonly List<(MeshRenderer curtain, MeshRenderer lights, MeshRenderer glows, bool required, bool finish)> gates = new();
        readonly List<(MeshRenderer halo, MeshRenderer glow)> mines = new();
        readonly MaterialPropertyBlock block = new();
        MeshRenderer guide;
        Mesh haloMesh, glowMesh;
        ulong shownMask = ulong.MaxValue;

        static readonly Color NextGate = new Color(.5f, .92f, 1f) * 2.2f, RequiredGate = new Color(1f, .62f, .2f) * 1.1f, OtherGate = new Color(1f, .55f, .25f) * .6f;
        static readonly Color FenceBlue = new Color(.35f, .75f, 1f), PostGlow = new Color(.5f, .85f, 1f), BoostGreen = new Color(.4f, 1f, .55f);

        public TrackDressing(TrackDefinition track, Transform parent)
        {
            look = track.Look;
            Root = new GameObject("Race track dressing"); Root.transform.SetParent(parent, false);
            world = new GameObject("Race dressing (world)"); world.transform.SetParent(parent, false);
            if (look.Glow == null) { Game.Core.GameLog.Warn("Race", "TrackDressing: track " + track.IdString + " has no Look materials (Game/Content/Build Race Track)"); return; }
            Lanes(track); Walls(track); Gates(track); Pads(track); Corners(track); Grid(track); WreckLights(track); Dust(); Guide();
            haloMesh = Keep(Disc(1)); glowMesh = Keep(Sprite(1));
        }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root);
            if (world != null) UnityEngine.Object.Destroy(world);
            foreach (var m in meshes) if (m != null) UnityEngine.Object.Destroy(m);
            meshes.Clear(); gates.Clear(); mines.Clear();
        }

        // ------------------------------------------------------------------------------------------------ per frame
        /// <summary>Lights the gates in `next` (a bit per gate id) for this pilot; the rest stay dim gold (required) or amber.</summary>
        public void SetNextGates(ulong next)
        {
            if (next == shownMask) return;
            shownMask = next;
            for (int i = 0; i < gates.Count; i++)
            {
                var g = gates[i]; bool lit = (next & (1UL << i)) != 0;
                var colour = lit ? NextGate : g.required ? RequiredGate : OtherGate;
                Tint(g.curtain, colour, g.finish ? 1 : 0); Tint(g.lights, lit ? NextGate * .8f : colour * 1.4f, 0); Tint(g.glows, lit ? NextGate * .9f : colour * 1.2f, 0);
            }
        }
        void Tint(MeshRenderer r, Color c, float checker)
        {
            if (r == null) return;
            r.GetPropertyBlock(block); block.SetColor("_Color", c); block.SetFloat("_Checker", checker); r.SetPropertyBlock(block);
        }

        public enum MineLook : byte { Arming, Armed, Triggered }
        /// <summary>One halo (trigger radius) and one glow per mine, in world space; pooled.</summary>
        public void SetMines(IReadOnlyList<(Vector3 position, float radius, MineLook state)> list, float time)
        {
            if (haloMesh == null) return;
            while (mines.Count < list.Count)
            {
                var halo = Renderer(world, "mine halo", haloMesh, look.Halo, false); var glow = Renderer(world, "mine light", glowMesh, look.Glow, false);
                mines.Add((halo, glow));
            }
            for (int i = 0; i < mines.Count; i++)
            {
                var (halo, glow) = mines[i]; bool on = i < list.Count;
                halo.gameObject.SetActive(on); glow.gameObject.SetActive(on);
                if (!on) continue;
                var (p, r, state) = list[i];
                halo.transform.localPosition = new Vector3(p.x, -.8f, p.z); halo.transform.localScale = new Vector3(r, 1, r);
                glow.transform.localPosition = p + Vector3.up * 2; glow.transform.localScale = Vector3.one * 5;
                Color c; float pulse = .5f + .5f * Mathf.Sin(time * 3.2f + i);
                switch (state)
                {
                    case MineLook.Arming: c = new Color(.55f, .6f, .75f) * .6f; break;
                    case MineLook.Triggered: c = Mathf.Sin(time * 30) > 0 ? Color.white * 3 : new Color(1f, .2f, .1f) * 3; break;
                    default: c = new Color(1f, .18f, .12f) * (.45f + .45f * pulse); break;   // owner: the red was too bright
                }
                Tint(halo, c, 0); Tint(glow, c * .9f, 0);
            }
        }

        /// <summary>A chevron on the flight plane 28 m from the pilot's ship toward its next gate; hidden when `show` is false.</summary>
        public void SetGuide(bool show, Vector3 ship, Vector3 target)
        {
            if (guide == null) return;
            Vector3 d = target - ship; d.y = 0;
            bool visible = show && d.sqrMagnitude > 60 * 60;
            guide.gameObject.SetActive(visible);
            if (!visible) return;
            d.Normalize();
            guide.transform.SetPositionAndRotation(new Vector3(ship.x, -.5f, ship.z) + d * 28, Quaternion.LookRotation(d, Vector3.up));
        }

        // ------------------------------------------------------------------------------------------------ building
        Mesh Keep(Mesh m) { meshes.Add(m); return m; }
        MeshRenderer Renderer(GameObject under, string name, Mesh mesh, Material material, bool shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(under.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = shadows;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }
        MeshRenderer Add(string name, Builder b, Material material, bool shadows = false) =>
            b.Count == 0 || material == null ? null : Renderer(Root, name, Keep(b.ToMesh(name)), material, shadows);

        /// The lane surface: each route's ribbon on the flight plane, as wide as the route. Where a route runs inside one drawn before it
        /// (a branch leaving the main line) its ribbon stops, so junctions show one surface.
        void Lanes(TrackDefinition track)
        {
            var b = new Builder();
            for (int pi = 0; pi < track.Paths.Length; pi++)
            {
                var path = track.Paths[pi];
                if (path.Points == null || path.Points.Length < 2) continue;
                var pts = path.Points; int n = pts.Length; bool closed = path.Closed;
                float along = 0, half = path.Width * .5f;
                int prev = -1;
                for (int i = 0; i <= (closed ? n : n - 1); i++)
                {
                    Vector2 p = pts[i % n];
                    if (i > 0) along += Vector2.Distance(pts[(i - 1) % n], p);
                    bool covered = false;
                    for (int o = 0; o < pi && !covered; o++) covered = Distance(track.Paths[o], p) < track.Paths[o].Width * .5f - 2;
                    if (covered) { prev = -1; continue; }
                    Vector2 normal = Normal(pts, i % n, closed) * half;
                    int k = b.Count;
                    b.Vertex(new Vector3(p.x - normal.x, -.6f, p.y - normal.y), Color.white, new Vector2(0, along), new Vector2(path.Width, 0));
                    b.Vertex(new Vector3(p.x + normal.x, -.6f, p.y + normal.y), Color.white, new Vector2(1, along), new Vector2(path.Width, 0));
                    if (prev >= 0) b.Quad(prev, prev + 1, k + 1, k);
                    prev = k;
                }
            }
            Add("lanes", b, look.Surface);
        }

        /// +1 when a wall is drawn the way the race runs past it (the nearest route's direction), −1 when against it: the fence's
        /// pulse and the footprint's dashes then travel with the racers.
        static float RaceWay(TrackDefinition track, Vector2[] wall)
        {
            int mid = wall.Length / 2; Vector2 at = wall[mid], dir = wall[Mathf.Min(wall.Length - 1, mid + 1)] - wall[Mathf.Max(0, mid - 1)];
            float best = float.MaxValue, way = 1;
            foreach (var path in track.Paths)
            {
                if (path.Points == null || path.Points.Length < 2) continue;
                var pts = path.Points; int n = pts.Length;
                for (int i = 0; i < n - 1; i++)
                {
                    float d = (pts[i] - at).sqrMagnitude;
                    if (d < best) { best = d; way = Vector2.Dot(pts[i + 1] - pts[i], dir) >= 0 ? 1 : -1; }
                }
            }
            return way;
        }

        static float Distance(in TrackPath path, Vector2 p)
        {
            var pts = path.Points; float best = float.MaxValue; int n = pts.Length;
            for (int i = 0; i < (path.Closed ? n : n - 1); i++)
            {
                Vector2 a = pts[i], ab = pts[(i + 1) % n] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + t * ab));
            }
            return best;
        }

        /// Field walls: a ribbon footprint and a 12 m fence per span, and on each voxel emitter post (not the gate pylons, which carry
        /// their own lights) a light band and a glow on its head. A span goes dark when its emitter is shot away (SetSpans).
        readonly Dictionary<int, (int f0, int f1, int r0, int r1)> spanRanges = new();
        readonly Dictionary<(int zone, int end), (int b0, int b1, int g0, int g1)> postRanges = new();
        Builder fenceB, ribbonB, bandsB, glowB; MeshRenderer fenceR, ribbonR, bandsR, glowR;
        void Walls(TrackDefinition track)
        {
            fenceB = new Builder(); ribbonB = new Builder(); bandsB = new Builder(); glowB = new Builder();
            var pylons = new List<Vector2>();
            foreach (var g in track.Gates)
            {
                if (g.Interior) continue;
                var right = new Vector2(Mathf.Cos(g.Yaw), -Mathf.Sin(g.Yaw));
                pylons.Add(g.Position - right * (g.Width * .5f + 4.5f)); pylons.Add(g.Position + right * (g.Width * .5f + 4.5f));
            }
            bool OnPylon(Vector2 q) { foreach (var p in pylons) if ((p - q).sqrMagnitude < .25f) return true; return false; }
            for (int zi = 0; zi < track.Zones.Length; zi++)
            {
                var z = track.Zones[zi];
                if (z.Zone == null || z.Zone.Shape != ZoneShape.Path || z.Path == null || z.Path.Length < 2) continue;
                float radius = z.PathRadius > 0 ? z.PathRadius : z.Zone.PathRadius;
                var pts = z.Path; float way = RaceWay(track, pts), along = 0;
                // keep the pulse continuous from span to span: start where the wall's previous span ended
                if (z.Emitters && zi > 0 && track.Zones[zi - 1].Emitters && track.Zones[zi - 1].EmitterB == z.EmitterA) along = lastAlong;
                int f0 = fenceB.Count, r0 = ribbonB.Count;
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 p = pts[i];
                    if (i > 0) along += Vector2.Distance(pts[i - 1], p);
                    Vector2 normal = Normal(pts, i, false) * (radius + 1.5f);
                    fenceB.Vertex(new Vector3(p.x, -3, p.y), Color.white, new Vector2(along * way, 0), new Vector2(0, 12));
                    fenceB.Vertex(new Vector3(p.x, 9, p.y), Color.white, new Vector2(along * way, 1), new Vector2(0, 12));
                    ribbonB.Vertex(new Vector3(p.x - normal.x, -.4f, p.y - normal.y), Color.white, new Vector2(along * way, 0), new Vector2(0, (radius + 1.5f) * 2));
                    ribbonB.Vertex(new Vector3(p.x + normal.x, -.4f, p.y + normal.y), Color.white, new Vector2(along * way, 1), new Vector2(0, (radius + 1.5f) * 2));
                    if (i > 0) { fenceB.Quad(f0 + (i - 1) * 2, f0 + i * 2, f0 + i * 2 + 1, f0 + (i - 1) * 2 + 1); ribbonB.Quad(r0 + (i - 1) * 2, r0 + (i - 1) * 2 + 1, r0 + i * 2 + 1, r0 + i * 2); }
                }
                lastAlong = along;
                spanRanges[zi] = (f0, fenceB.Count, r0, ribbonB.Count);
                if (!z.Emitters) continue;
                bool last = zi + 1 >= track.Zones.Length || !track.Zones[zi + 1].Emitters || track.Zones[zi + 1].EmitterA != z.EmitterB;
                for (int end = 0; end < (last ? 2 : 1); end++)
                {
                    Vector2 q = end == 0 ? z.EmitterA : z.EmitterB;
                    if (OnPylon(q)) continue;
                    var c = new Vector3(q.x, 0, q.y);
                    int b0 = bandsB.Count, g0 = glowB.Count;
                    bandsB.Ring(c, 8, 2.3f, 8.2f, 9.6f, FenceBlue);
                    bandsB.Ring(c, 8, 2.3f, -1.4f, -.8f, FenceBlue * .6f);
                    glowB.Glow(c + Vector3.up * 11.6f, 3.8f, PostGlow, 0, 0);
                    postRanges[(zi, end)] = (b0, bandsB.Count, g0, glowB.Count);
                }
            }
            fenceR = Add("wall fence", fenceB, look.Fence); ribbonR = Add("wall footprint", ribbonB, look.Ribbon);
            bandsR = Add("wall post bands", bandsB, look.Solid); glowR = Add("wall post lights", glowB, look.Glow);
        }
        float lastAlong;

        /// <summary>Darkens the spans that are down (zone index → which emitters are lost: bit 0 A, bit 1 B). Idempotent.</summary>
        public void SetSpans(IReadOnlyList<(int zone, byte lost)> down)
        {
            if (fenceB == null || down.Count == shownDown) return;
            shownDown = down.Count;
            foreach (var (zone, lost) in down)
            {
                if (spanRanges.TryGetValue(zone, out var r)) { fenceB.Fade(r.f0, r.f1); ribbonB.Fade(r.r0, r.r1); }
                for (int end = 0; end < 2; end++)
                {
                    if ((lost & (1 << end)) == 0) continue;
                    // a post is drawn by the span it starts (end 0), or by the wall's last span (end 1)
                    if (postRanges.TryGetValue((zone, end), out var p) || (end == 1 && postRanges.TryGetValue((zone + 1, 0), out p))) { bandsB.Fade(p.b0, p.b1); glowB.Fade(p.g0, p.g1); }
                }
            }
            fenceB.Upload(fenceR); ribbonB.Upload(ribbonR); bandsB.Upload(bandsR); glowB.Upload(glowR);
        }
        int shownDown;

        /// Gates: two pylons, a light strip on each inner face, bands, a glow on top, and an energy curtain between them. Gate 0 gets a
        /// checkered curtain (no gantry over it: the camera collided with one).
        void Gates(TrackDefinition track)
        {
            for (int id = 0; id < track.Gates.Length; id++)
            {
                var g = track.Gates[id];
                Vector3 c = new Vector3(g.Position.x, 0, g.Position.y), right = new Vector3(Mathf.Cos(g.Yaw), 0, -Mathf.Sin(g.Yaw));
                float half = g.Width * .5f, h = 32, bottom = -h * .3f;
                bool finish = id == 0;
                var curtain = new Builder(); var lights = new Builder(); var glows = new Builder();
                Vector3 a = c - right * half, b = c + right * half;
                int k = curtain.Count;
                curtain.Vertex(a + Vector3.up * bottom, Color.white, new Vector2(0, 0), new Vector2(g.Width, h));
                curtain.Vertex(b + Vector3.up * bottom, Color.white, new Vector2(1, 0), new Vector2(g.Width, h));
                curtain.Vertex(b + Vector3.up * (bottom + h), Color.white, new Vector2(1, 1), new Vector2(g.Width, h));
                curtain.Vertex(a + Vector3.up * (bottom + h), Color.white, new Vector2(0, 1), new Vector2(g.Width, h));
                curtain.Quad(k, k + 1, k + 2, k + 3);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 foot = c + right * side * (half + 4.5f);
                    if (g.Interior) continue;   // inside a wreck: no pylons, the curtain spans the hull's trench
                    // the voxel pylon (ship_track_pylon) stands here; these are its lights
                    lights.Ring(foot, 8, 4.7f, 3, 5, Color.white);
                    lights.Ring(foot, 8, 3.3f, 17, 19, Color.white);
                    lights.Strip(foot - right * side * 3.7f, Vector3.up, -right * side, 0.9f, -12, 12, Color.white);
                    glows.Glow(foot + Vector3.up * 30.5f, 10, Color.white, 0, 0);
                    glows.Glow(foot - right * side * 4f, 6, Color.white, 0, 0);
                }
                gates.Add((Add("gate " + id + " curtain", curtain, look.Curtain), Add("gate " + id + " lights", lights, look.Solid),
                    Add("gate " + id + " glows", glows, look.Glow), g.Required, finish));
            }
            SetNextGates(1);
        }

        /// Boost strips: a chevron pad, glowing rails along both edges and a light at each corner.
        void Pads(TrackDefinition track)
        {
            var pad = new Builder(); var rails = new Builder(); var glows = new Builder();
            foreach (var z in track.Zones)
            {
                if (z.Zone == null || z.Zone.Shape != ZoneShape.Box) continue;
                Vector2 he = z.HalfExtents.x > 0 ? z.HalfExtents : z.Zone.HalfExtents;
                Vector3 c = new Vector3(z.Position.x, -.4f, z.Position.y), f = new Vector3(Mathf.Sin(z.Yaw), 0, Mathf.Cos(z.Yaw)), r = new Vector3(f.z, 0, -f.x);
                float len = he.y * 2;
                int k = pad.Count;
                pad.Vertex(c - r * he.x - f * he.y, Color.white, new Vector2(0, 0), new Vector2(len, he.x * 2));
                pad.Vertex(c + r * he.x - f * he.y, Color.white, new Vector2(1, 0), new Vector2(len, he.x * 2));
                pad.Vertex(c + r * he.x + f * he.y, Color.white, new Vector2(1, len), new Vector2(len, he.x * 2));
                pad.Vertex(c - r * he.x + f * he.y, Color.white, new Vector2(0, len), new Vector2(len, he.x * 2));
                pad.Quad(k, k + 1, k + 2, k + 3);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 edge = c + r * side * (he.x + 1.5f);
                    rails.Beam(edge - f * he.y + Vector3.up * .8f, edge + f * he.y + Vector3.up * .8f, 1.2f, .8f, BoostGreen);
                    glows.Glow(edge - f * he.y + Vector3.up * 2, 7, BoostGreen, 0, 0); glows.Glow(edge + f * he.y + Vector3.up * 2, 7, BoostGreen, 0, 0);
                    for (float s = -he.y + 30; s < he.y - 20; s += 45) glows.Glow(edge + f * s + Vector3.up * 1.5f, 3.5f, BoostGreen, 1, s * .004f + (side > 0 ? .5f : 0));
                }
            }
            Add("boost pads", pad, look.Pad); Add("boost rails", rails, look.Solid); Add("boost lights", glows, look.Glow);
        }

        /// Corner boards: on the outside of every bend tighter than ~700 m radius, a holographic chevron board every 70 m.
        void Corners(TrackDefinition track)
        {
            var b = new Builder();
            foreach (var path in track.Paths)
            {
                if (path.Interior || path.Points == null || path.Points.Length < 8) continue;
                var pts = path.Points; int n = pts.Length; bool closed = path.Closed;
                float along = 0, lastBoard = -1000;
                for (int i = 1; i < n; i++)
                {
                    along += Vector2.Distance(pts[i - 1], pts[i]);
                    // heading change over ±30 m
                    int i0 = i, i1 = i; float back = 0, ahead = 0;
                    while (back < 30 && (closed || i0 > 0)) { int j = (i0 - 1 + n) % n; back += Vector2.Distance(pts[j], pts[i0]); i0 = j; if (i0 == i) break; }
                    while (ahead < 30 && (closed || i1 < n - 1)) { int j = (i1 + 1) % n; ahead += Vector2.Distance(pts[i1], pts[j]); i1 = j; if (i1 == i) break; }
                    Vector2 d0 = (pts[i] - pts[i0]).normalized, d1 = (pts[i1] - pts[i]).normalized;
                    float turn = d0.x * d1.y - d0.y * d1.x;   // + left, − right
                    if (Mathf.Abs(turn) < 60f / 700f || along - lastBoard < 70) continue;
                    lastBoard = along;
                    Vector2 normal = Normal(pts, i, closed), forward = (d0 + d1).normalized;
                    Vector2 outside = normal * (turn > 0 ? -1 : 1);   // Normal is to the left of travel; a left bend's outside is the right
                    Vector2 at = pts[i] + outside * (path.Width * .5f - 11);
                    Vector3 c = new Vector3(at.x, 0, at.y), f = new Vector3(forward.x, 0, forward.y);
                    const float W = 24, H = 9;
                    int k = b.Count;
                    var size = new Vector2(W, H);
                    b.Vertex(c - f * W * .5f + Vector3.up * -1, Color.white, new Vector2(0, 0), size);
                    b.Vertex(c + f * W * .5f + Vector3.up * -1, Color.white, new Vector2(1, 0), size);
                    b.Vertex(c + f * W * .5f + Vector3.up * (H - 1), Color.white, new Vector2(1, 1), size);
                    b.Vertex(c - f * W * .5f + Vector3.up * (H - 1), Color.white, new Vector2(0, 1), size);
                    b.Quad(k, k + 1, k + 2, k + 3);
                }
            }
            Add("corner boards", b, look.Sign);
        }

        /// The start grid: a light frame round each slot and a glow at its nose.
        void Grid(TrackDefinition track)
        {
            var frames = new Builder(); var glows = new Builder();
            foreach (var s in track.StartGrid)
            {
                Vector3 c = new Vector3(s.Position.x, -.5f, s.Position.y), f = new Vector3(Mathf.Sin(s.Yaw), 0, Mathf.Cos(s.Yaw)), r = new Vector3(f.z, 0, -f.x);
                float hw = 9, hl = 15;
                Vector3 p0 = c - r * hw - f * hl, p1 = c + r * hw - f * hl, p2 = c + r * hw + f * hl, p3 = c - r * hw + f * hl;
                frames.Beam(p0, p1, 1, .2f, Color.white); frames.Beam(p1, p2, 1, .2f, Color.white);
                frames.Beam(p2, p3, 1, .2f, Color.white); frames.Beam(p3, p0, 1, .2f, Color.white);
                glows.Glow(p2 + Vector3.up, 4, Color.white, 1, 0); glows.Glow(p3 + Vector3.up, 4, Color.white, 1, .5f);
            }
            var lights = Add("grid frames", frames, look.Solid); var spots = Add("grid lights", glows, look.Glow);
            Tint(lights, new Color(1f, .75f, .35f) * 2.2f, 0); Tint(spots, new Color(1f, .75f, .35f) * 2f, 0);
        }

        void WreckLights(TrackDefinition track)
        {
            var glows = new Builder();
            for (int i = 0; i < track.Lights.Length; i++)
            {
                var l = track.Lights[i];
                glows.Glow(l.Position, l.Size, l.Color, (float)l.Kind, (i * .6180339f) % 1);
            }
            Add("wreck lights", glows, look.Glow);
        }

        /// Space dust round the camera: the shader wraps 600 motes through a 240 m box, so speed reads against them.
        void Dust()
        {
            if (look.Dust == null) return;
            var b = new Builder(); var rng = new Unity.Mathematics.Random(16);
            for (int i = 0; i < 600; i++)
            {
                var p = new Vector3(rng.NextFloat(), rng.NextFloat(), rng.NextFloat());
                float size = .25f + rng.NextFloat() * .3f; var c = Color.white * (.25f + rng.NextFloat() * .4f);
                int k = b.Count;
                b.Vertex(p, c, new Vector2(-1, -1), new Vector2(-size, -size)); b.Vertex(p, c, new Vector2(1, -1), new Vector2(size, -size));
                b.Vertex(p, c, new Vector2(1, 1), new Vector2(size, size)); b.Vertex(p, c, new Vector2(-1, 1), new Vector2(-size, size));
                b.Quad(k, k + 1, k + 2, k + 3);
            }
            var mesh = Keep(b.ToMesh("dust")); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);   // wraps round the camera
            Renderer(world, "space dust", mesh, look.Dust, false);
        }

        void Guide()
        {
            var b = new Builder();
            // a flat chevron pointing +z, 16 m wide
            b.Beam(new Vector3(-8, 0, -5), new Vector3(0, 0, 3), 2.2f, .1f, Color.white);
            b.Beam(new Vector3(8, 0, -5), new Vector3(0, 0, 3), 2.2f, .1f, Color.white);
            guide = Renderer(world, "guide arrow", Keep(b.ToMesh("guide")), look.Solid, false);
            Tint(guide, NextGate * .7f, 0);
            guide.gameObject.SetActive(false);
        }

        static Mesh Disc(float radius)
        {
            var b = new Builder(); int k = b.Count;
            b.Vertex(new Vector3(-radius, 0, -radius), Color.white, new Vector2(-1, -1), default); b.Vertex(new Vector3(radius, 0, -radius), Color.white, new Vector2(1, -1), default);
            b.Vertex(new Vector3(radius, 0, radius), Color.white, new Vector2(1, 1), default); b.Vertex(new Vector3(-radius, 0, radius), Color.white, new Vector2(-1, 1), default);
            b.Quad(k, k + 1, k + 2, k + 3); return b.ToMesh("halo");
        }
        static Mesh Sprite(float size) { var b = new Builder(); b.Glow(Vector3.zero, size, Color.white, 0, 0); return b.ToMesh("glow"); }

        /// The unit normal to the left of travel at point i of a polyline.
        static Vector2 Normal(Vector2[] pts, int i, bool closed)
        {
            int n = pts.Length;
            Vector2 prev = closed ? pts[(i - 1 + n) % n] : pts[Mathf.Max(0, i - 1)], next = closed ? pts[(i + 1) % n] : pts[Mathf.Min(n - 1, i + 1)];
            Vector2 d = (next - prev).normalized;
            return new Vector2(-d.y, d.x);
        }

    }
}
