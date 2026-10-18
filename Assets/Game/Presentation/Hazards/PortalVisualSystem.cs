using System.Collections.Generic;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Material = UnityEngine.Material;

namespace Game.Presentation.Hazards
{
    /// <summary>HAZ-021: how a lane-gate portal looks, on every machine (from its PortalLink and SimClock). Both gates show as a bar
    /// across the track with a post at each end, in three glow widths like the track plan. Open: blue, breathing. Jammed: red and
    /// broken into dashes that shrink toward the posts as the jam runs out, so a pilot can read when it opens again.
    /// Drawn with the race hologram shader (Game/Race/Holo, solid mode) on the flight plane, like the pulse zones.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    public partial class PortalVisualSystem : SystemBase
    {
        const float Plane = -1.2f, Post = 7;
        const int Dashes = 14;
        static readonly Color Open = new Color(.22f, .42f, 1f) * 2.2f, Jammed = new Color(1f, .22f, .16f) * 3.2f;

        sealed class Visual { public GameObject Go; public Mesh Mesh; }
        readonly Dictionary<Entity, Visual> visuals = new();
        readonly List<Vector3> v = new(); readonly List<Color> c = new(); readonly List<Vector2> uv = new(), uv1 = new(); readonly List<int> tri = new();
        readonly List<Entity> stale = new();
        Material material;
        EntityQuery portals, zones;

        protected override void OnCreate()
        {
            portals = GetEntityQuery(ComponentType.ReadOnly<PortalLink>(), ComponentType.ReadOnly<WorldZone>(), ComponentType.ReadOnly<LocalTransform>());
            zones = GetEntityQuery(ComponentType.ReadOnly<WorldZone>(), ComponentType.ReadOnly<LocalTransform>());
            RequireForUpdate(portals);
        }

        protected override void OnDestroy()
        {
            foreach (var visual in visuals.Values) Release(visual);
            visuals.Clear();
            if (material != null) Object.Destroy(material);
        }

        static void Release(Visual visual) { if (visual.Go != null) Object.Destroy(visual.Go); if (visual.Mesh != null) Object.Destroy(visual.Mesh); }

        protected override void OnUpdate()
        {
            if (material == null)
            {
                var shader = Shader.Find("Game/Race/Holo");
                if (shader == null) return;
                material = new Material(shader) { name = "Portal (runtime)" };
                material.SetFloat("_Mode", 6); material.SetColor("_Color", Color.white); material.SetFloat("_Pixel", 1); material.SetFloat("_Brightness", 1);
            }
            uint tick = SystemAPI.TryGetSingleton<SimClock>(out var clock) ? clock.Tick : 0;
            using var entries = portals.ToEntityArray(Allocator.Temp);
            using var all = zones.ToEntityArray(Allocator.Temp);
            var seen = new HashSet<Entity>();
            foreach (var e in entries)
            {
                var link = EntityManager.GetComponentData<PortalLink>(e);
                var entry = EntityManager.GetComponentData<WorldZone>(e);
                WorldZone exit = default; float2 exitAt = default; bool hasExit = false;
                foreach (var z in all)
                {
                    var w = EntityManager.GetComponentData<WorldZone>(z);
                    if (w.PersistentId != link.Other) continue;
                    exit = w; exitAt = EntityManager.GetComponentData<LocalTransform>(z).Position.xz; hasExit = true; break;
                }
                seen.Add(e);
                if (!visuals.TryGetValue(e, out var visual))
                {
                    visual = new Visual { Mesh = new Mesh { name = "Portal" } };
                    visual.Mesh.MarkDynamic();
                    visual.Go = new GameObject("Portal " + entry.PersistentId);
                    visual.Go.AddComponent<MeshFilter>().sharedMesh = visual.Mesh;
                    var r = visual.Go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    visuals.Add(e, visual);
                }
                // 1 when the jam starts, 0 when the portal opens again
                float left = link.JammedUntilTick > tick ? math.saturate((link.JammedUntilTick - tick) / math.max(1f, PortalDecideSystem.JamTicks(link.JamSeconds))) : 0;
                v.Clear(); c.Clear(); uv.Clear(); uv1.Clear(); tri.Clear();
                float2 entryAt = EntityManager.GetComponentData<LocalTransform>(e).Position.xz;
                Gate(entryAt, entry, left, tick, true);
                if (hasExit) Gate(exitAt, exit, left, tick, false);
                var mesh = visual.Mesh; mesh.Clear();
                mesh.SetVertices(v); mesh.SetColors(c); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv1); mesh.SetTriangles(tri, 0);
                float2 mid = hasExit ? (entryAt + exitAt) * .5f : entryAt;
                float span = (hasExit ? math.distance(entryAt, exitAt) : 0) + entry.HalfExtents.x * 2 + 50;
                mesh.bounds = new Bounds(new Vector3(mid.x, Plane, mid.y), new Vector3(span, 4, span));
            }
            stale.Clear();
            foreach (var kv in visuals) if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var e in stale) { Release(visuals[e]); visuals.Remove(e); }
        }

        /// <summary>One gate: glow bars across its width and a post at each end; the entry glows a little brighter than the exit.</summary>
        void Gate(float2 at, in WorldZone zone, float jamLeft, uint tick, bool entry)
        {
            float2 forward = new float2(math.sin(zone.Yaw), math.cos(zone.Yaw)), right = new float2(forward.y, -forward.x);
            float half = zone.HalfExtents.x;
            bool jammed = jamLeft > 0;
            var colour = jammed ? Jammed : Open;
            float breathe = jammed ? .75f + .25f * math.sin(tick * .5f) : .8f + .2f * math.sin(tick * .08f);
            float scale = entry ? 1 : .8f;
            foreach (var (width, alpha) in new[] { (22f, .12f), (12f, .3f), (5f, 1f) })
            {
                if (!jammed) { Quad(at - right * half, at + right * half, forward, width, colour, alpha * breathe * scale); continue; }
                // dashes from each post inward, covering jamLeft of the half width: they shrink toward the posts as the jam runs out
                for (int k = 0; k < Dashes; k++)
                {
                    float s0 = k / (float)Dashes, s1 = (k + .55f) / Dashes;
                    if (s0 >= jamLeft) break;
                    s1 = math.min(s1, jamLeft);
                    Quad(at - right * half * (1 - s0), at - right * half * (1 - s1), forward, width, colour, alpha * breathe * scale);
                    Quad(at + right * half * (1 - s1), at + right * half * (1 - s0), forward, width, colour, alpha * breathe * scale);
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float2 p = at + right * half * side;
                Quad(p - right * Post * .5f, p + right * Post * .5f, forward, Post, colour, scale);
            }
        }

        /// <summary>A flat quad along a→b on the flight plane, `width` thick across `forward`.</summary>
        void Quad(float2 a, float2 b, float2 forward, float width, Color colour, float alpha)
        {
            float2 o = forward * width * .5f;
            var a0 = new Vector3(a.x - o.x, Plane, a.y - o.y); var a1 = new Vector3(b.x - o.x, Plane, b.y - o.y);
            var b0 = new Vector3(a.x + o.x, Plane, a.y + o.y); var b1 = new Vector3(b.x + o.x, Plane, b.y + o.y);
            int k = v.Count; var tint = new Color(colour.r, colour.g, colour.b, alpha);
            var size = new Vector2(math.distance(a, b), width);
            v.Add(a0); v.Add(a1); v.Add(b1); v.Add(b0);
            for (int n = 0; n < 4; n++) { c.Add(tint); uv1.Add(size); }
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            tri.Add(k); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k); tri.Add(k + 2); tri.Add(k + 3);
        }
    }
}
