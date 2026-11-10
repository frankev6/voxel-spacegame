using System.Collections.Generic;
using Game.Core.Systems;
using Game.Simulation.Extraction;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Material = UnityEngine.Material;

namespace Game.Presentation.Extraction
{
    /// <summary>SPEC-93 EXT-002: the magnet cable, on every machine, from TowLink and the bodies' rendered poses. Slack, it hangs in a
    /// loose wave; taut, it is straight and goes from steel to amber, and red near its break force, so the pilot reads the load.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    public partial class TowCableVisualSystem : SystemBase
    {
        const int Points = 24;
        static readonly Color Slack = new Color(.55f, .6f, .66f), Taut = new Color(1f, .62f, .18f) * 1.6f, Breaking = new Color(1f, .16f, .1f) * 2.4f;
        readonly Dictionary<Entity, LineRenderer> lines = new();
        readonly List<Entity> stale = new();
        readonly Vector3[] buffer = new Vector3[Points];
        Material material;
        EntityQuery links;

        protected override void OnCreate()
        {
            links = GetEntityQuery(ComponentType.ReadOnly<TowLink>(), ComponentType.ReadOnly<LocalTransform>());
        }

        protected override void OnDestroy()
        {
            foreach (var l in lines.Values) if (l != null) Object.Destroy(l.gameObject);
            lines.Clear();
            if (material != null) Object.Destroy(material);
        }

        protected override void OnUpdate()
        {
            if (links.IsEmpty && lines.Count == 0) return;
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                if (shader == null) return;
                material = new Material(shader) { name = "Tow cable (runtime)" };
            }
            using var entities = links.ToEntityArray(Allocator.Temp);
            var seen = new HashSet<Entity>();
            float time = (float)SystemAPI.Time.ElapsedTime;
            foreach (var ship in entities)
            {
                var link = EntityManager.GetComponentData<TowLink>(ship);
                if (link.Load == Entity.Null || !EntityManager.Exists(link.Load)) continue;
                seen.Add(ship);
                if (!lines.TryGetValue(ship, out var line) || line == null)
                {
                    var go = new GameObject("Tow cable");
                    line = go.AddComponent<LineRenderer>();
                    line.sharedMaterial = material; line.positionCount = Points; line.useWorldSpace = true;
                    line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                    line.numCapVertices = 2; line.widthMultiplier = .45f;
                    lines[ship] = line;
                }
                float3 a = Pose(ship, link.ShipAnchor), b = Pose(link.Load, link.LoadAnchor);
                float d = math.distance(a, b);
                float slack = math.saturate(1 - d / math.max(1, link.Length));       // 0 taut .. 1 bunched up
                float3 dir = d > 1e-3f ? (b - a) / d : new float3(0, 0, 1);
                float3 side = math.normalizesafe(math.cross(dir, new float3(0, 1, 0)), new float3(1, 0, 0));
                float3 up = math.normalizesafe(math.cross(side, dir), new float3(0, 1, 0));
                float amplitude = math.sqrt(slack) * link.Length * .12f;
                for (int i = 0; i < Points; i++)
                {
                    float t = i / (float)(Points - 1);
                    float env = math.sin(t * math.PI);
                    float3 p = math.lerp(a, b, t) + (side * math.sin(t * math.PI * 3 + time * 1.3f) + up * .6f * math.cos(t * math.PI * 2 + time)) * amplitude * env;
                    buffer[i] = p;
                }
                line.SetPositions(buffer);
                float load = link.BreakForce > 0 ? math.saturate(link.Tension / link.BreakForce) : math.saturate(link.Tension / 4e6f);
                Color c = link.Tension <= 0 ? Slack : load < .6f ? Color.Lerp(Slack, Taut, math.saturate(load / .6f + .25f)) : Color.Lerp(Taut, Breaking, (load - .6f) / .4f);
                line.startColor = c; line.endColor = c;
                material.color = Color.white;
            }
            stale.Clear();
            foreach (var kv in lines) if (!seen.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (var e in stale) { if (lines[e] != null) Object.Destroy(lines[e].gameObject); lines.Remove(e); }
        }

        float3 Pose(Entity e, float3 local)
        {
            if (EntityManager.HasComponent<LocalToWorld>(e)) return math.transform(EntityManager.GetComponentData<LocalToWorld>(e).Value, local);
            return EntityManager.GetComponentData<LocalTransform>(e).TransformPoint(local);
        }
    }
}
