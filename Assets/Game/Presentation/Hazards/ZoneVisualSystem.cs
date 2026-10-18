using System.Collections.Generic;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Hazards;
using Game.Simulation.Hazards;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Material = UnityEngine.Material;

namespace Game.Presentation.Hazards
{
    /// <summary>HAZ-016: how a Pulse zone looks, on every machine (from the replicated schedule and SimClock, as PulseMath decides).
    /// A reactor (C = 0) shows a dim ring at its reach that brightens over the last 0.75 s before each pulse, then a bright ring that
    /// sweeps out from the core in 0.3 s; the ground it lights fills in faintly with the warning and flashes with the pulse. A flare (C = 1) does the same with a line along the box's back edge sweeping forward.
    /// Shadows show as gaps: each segment is cut where a ray from the source meets a grid (the same rays the authority casts).
    /// Drawn with the race hologram shader (Game/Race/Holo, solid mode) on the flight plane.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    public partial class ZoneVisualSystem : SystemBase
    {
        const int Segments = 96, WarnTicks = 45, SweepTicks = 18;
        const float EdgeWidth = 1.5f, WaveWidth = 3.5f, Plane = -1.2f;
        static readonly Color Reactor = new Color(1f, .5f, .18f) * 3.2f, Flare = new Color(1f, .88f, .5f) * 3.2f;

        sealed class Visual { public GameObject Go; public Mesh Mesh; public readonly float[] Shadow = new float[Segments]; }
        readonly Dictionary<Entity, Visual> visuals = new();
        readonly List<Vector3> v = new(); readonly List<Color> c = new(); readonly List<Vector2> uv = new(), uv1 = new(); readonly List<int> tri = new();
        readonly List<Entity> stale = new();
        Material material;
        EntityQuery zones, physics;

        protected override void OnCreate()
        {
            zones = GetEntityQuery(ComponentType.ReadOnly<WorldZone>(), ComponentType.ReadOnly<ZoneEffect>(), ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<ZoneSchedule>());
            physics = GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            RequireForUpdate(zones);
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
                material = new Material(shader) { name = "Pulse zone (runtime)" };
                material.SetFloat("_Mode", 6); material.SetColor("_Color", Color.white); material.SetFloat("_Pixel", 1); material.SetFloat("_Brightness", 1);
            }
            uint tick = SystemAPI.TryGetSingleton<SimClock>(out var clock) ? clock.Tick : 0;
            bool canCast = !physics.IsEmptyIgnoreFilter;
            CollisionWorld world = default;
            if (canCast) { CompleteDependency(); EntityManager.CompleteDependencyBeforeRO<PhysicsWorldSingleton>(); world = physics.GetSingleton<PhysicsWorldSingleton>().CollisionWorld; }
            var hits = new NativeList<Unity.Physics.RaycastHit>(8, Allocator.Temp);
            using var entities = zones.ToEntityArray(Allocator.Temp);
            var seen = new HashSet<Entity>();
            foreach (var e in entities)
            {
                ZoneEffect pulse = default; bool found = false;
                foreach (var effect in EntityManager.GetBuffer<ZoneEffect>(e, true)) if (effect.Kind == ZoneEffectKind.Pulse) { pulse = effect; found = true; break; }
                var zone = EntityManager.GetComponentData<WorldZone>(e);
                if (!found || zone.Shape == ZoneShape.Path) continue;
                seen.Add(e);
                if (!visuals.TryGetValue(e, out var visual))
                {
                    visual = new Visual { Mesh = new Mesh { name = "Pulse zone" } };
                    visual.Mesh.MarkDynamic();
                    visual.Go = new GameObject("Pulse zone " + zone.PersistentId);
                    visual.Go.AddComponent<MeshFilter>().sharedMesh = visual.Mesh;
                    var r = visual.Go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
                    r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    visuals.Add(e, visual);
                }
                var schedule = EntityManager.GetComponentData<ZoneSchedule>(e);
                float2 centre = EntityManager.GetComponentData<LocalTransform>(e).Position.xz;
                bool flare = pulse.C >= .5f;
                // Ticks since the last pulse and until the next one.
                uint since = schedule.PeriodTicks > 0 ? (tick + schedule.PhaseTicks) % schedule.PeriodTicks : uint.MaxValue;
                uint until = schedule.PeriodTicks > 0 ? schedule.PeriodTicks - since : uint.MaxValue;
                bool live = schedule.Enabled != 0 && schedule.PeriodTicks > 0;
                float warn = live ? Mathf.Clamp01(1 - (until - 1) / (float)WarnTicks) : 0;
                float sweep = live && since < SweepTicks ? (since + .5f) / SweepTicks : -1;
                Shadows(visual, zone, centre, pulse.C, canCast, world, hits);
                Build(visual, zone, centre, flare, warn, sweep, flare ? Flare : Reactor);
            }
            hits.Dispose();
            stale.Clear();
            foreach (var kv in visuals) if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var e in stale) { Release(visuals[e]); visuals.Remove(e); }
        }

        /// <summary>Per segment, how far the light gets before a grid stops it (the full reach when nothing does).</summary>
        static void Shadows(Visual visual, in WorldZone zone, float2 centre, float c, bool canCast, in CollisionWorld world, NativeList<Unity.Physics.RaycastHit> hits)
        {
            bool flare = c >= .5f;
            float reach = flare ? 2 * zone.HalfExtents.y : zone.HalfExtents.x;
            for (int i = 0; i < Segments; i++)
            {
                Ray(zone, centre, flare, i, out float3 from, out float3 dir);
                float d = reach;
                if (canCast)
                {
                    hits.Clear();
                    if (world.CastRay(new RaycastInput { Start = from, End = from + dir * reach, Filter = PulseSystem.ShadowQuery }, ref hits))
                        foreach (var h in hits) d = math.min(d, h.Fraction * reach);
                }
                visual.Shadow[i] = d;
            }
        }

        /// <summary>Segment i's ray: from the core outward (reactor), or from the box's back edge forward (flare).</summary>
        static void Ray(in WorldZone zone, float2 centre, bool flare, int i, out float3 from, out float3 dir)
        {
            if (!flare)
            {
                float a = (i + .5f) * math.PI * 2 / Segments;
                from = new float3(centre.x, 0, centre.y); dir = new float3(math.cos(a), 0, math.sin(a));
                return;
            }
            float2 forward = new float2(math.sin(zone.Yaw), math.cos(zone.Yaw)), right = new float2(forward.y, -forward.x);
            float across = ((i + .5f) / Segments * 2 - 1) * zone.HalfExtents.x;
            float2 p = centre - forward * zone.HalfExtents.y + right * across;
            from = new float3(p.x, 0, p.y); dir = new float3(forward.x, 0, forward.y);
        }

        void Build(Visual visual, in WorldZone zone, float2 centre, bool flare, float warn, float sweep, Color colour)
        {
            v.Clear(); c.Clear(); uv.Clear(); uv1.Clear(); tri.Clear();
            float reach = flare ? 2 * zone.HalfExtents.y : zone.HalfExtents.x;
            // The reach outline (the back edge for a flare), dim, brightening before a pulse; dark where the light cannot get there.
            float edgeAlpha = .18f + .82f * warn * warn;
            for (int i = 0; i < Segments; i++)
            {
                float at = flare ? 0 : reach;
                bool shadowed = !flare && visual.Shadow[i] < reach - 1;
                Segment(zone, centre, flare, i, at, EdgeWidth, colour, shadowed ? edgeAlpha * .15f : edgeAlpha);
            }
            // The lit ground, faint, filling in before a pulse and flashing with it: a shadow reads as the dark wedge behind a grid.
            float fill = .04f + .2f * warn * warn + (sweep >= 0 ? .3f * (1 - sweep) : 0);
            for (int i = 0; i < Segments; i++)
            {
                float d = visual.Shadow[i];
                if (d > .5f) Segment(zone, centre, flare, i, d * .5f, d, colour, fill);
            }
            if (sweep >= 0)
            {
                float at = sweep * reach, fade = 1 - sweep * sweep;
                for (int i = 0; i < Segments; i++)
                    if (at <= visual.Shadow[i]) Segment(zone, centre, flare, i, at, WaveWidth, colour, fade);
            }
            var mesh = visual.Mesh; mesh.Clear();
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv1); mesh.SetTriangles(tri, 0);
            mesh.bounds = new Bounds(new Vector3(centre.x, Plane, centre.y), new Vector3(reach * 4 + 10, 4, reach * 4 + 10));
        }

        /// <summary>One flat quad of segment i at distance `at` along its ray, `width` thick.</summary>
        void Segment(in WorldZone zone, float2 centre, bool flare, int i, float at, float width, Color colour, float alpha)
        {
            Vector3 a0, a1, b0, b1;
            if (!flare)
            {
                float t0 = i * math.PI * 2 / Segments, t1 = (i + 1) * math.PI * 2 / Segments;
                float r0 = math.max(0, at - width * .5f), r1 = at + width * .5f;
                Vector3 d0 = new Vector3(math.cos(t0), 0, math.sin(t0)), d1 = new Vector3(math.cos(t1), 0, math.sin(t1)), o = new Vector3(centre.x, Plane, centre.y);
                a0 = o + d0 * r0; a1 = o + d1 * r0; b0 = o + d0 * r1; b1 = o + d1 * r1;
            }
            else
            {
                float2 forward = new float2(math.sin(zone.Yaw), math.cos(zone.Yaw)), right = new float2(forward.y, -forward.x);
                float x0 = (i / (float)Segments * 2 - 1) * zone.HalfExtents.x, x1 = ((i + 1) / (float)Segments * 2 - 1) * zone.HalfExtents.x;
                float2 back = centre - forward * zone.HalfExtents.y;
                float2 p00 = back + right * x0 + forward * (at - width * .5f), p01 = back + right * x1 + forward * (at - width * .5f);
                float2 p10 = back + right * x0 + forward * (at + width * .5f), p11 = back + right * x1 + forward * (at + width * .5f);
                a0 = new Vector3(p00.x, Plane, p00.y); a1 = new Vector3(p01.x, Plane, p01.y); b0 = new Vector3(p10.x, Plane, p10.y); b1 = new Vector3(p11.x, Plane, p11.y);
            }
            int k = v.Count; var tint = new Color(colour.r, colour.g, colour.b, alpha);
            var size = new Vector2(Vector3.Distance(a0, a1), width);
            v.Add(a0); v.Add(a1); v.Add(b1); v.Add(b0);
            for (int n = 0; n < 4; n++) { c.Add(tint); uv1.Add(size); }
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            tri.Add(k); tri.Add(k + 1); tri.Add(k + 2); tri.Add(k); tri.Add(k + 2); tri.Add(k + 3);
        }
    }
}
