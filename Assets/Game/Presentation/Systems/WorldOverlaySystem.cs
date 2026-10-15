using System.Collections.Generic;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Galaxy.Interaction;
using Game.Presentation.Presenters;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.UI.Data;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.Systems
{
    /// <summary>QA-001 in-world tags: corner brackets and a name/distance label on nearby ships, stations and wrecks,
    /// coloured by relation. Salvageable wrecks get a subtle salvage highlight; the current interaction target is
    /// emphasised. Pooled UI Toolkit elements on the World layer; no element is created after Bind.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    [UpdateAfter(typeof(CameraTargetSyncSystem))]
    public partial class WorldOverlaySystem : SystemBase
    {
        const int MaxTags = 24, MinWreckCells = 24;
        const float RangeM = 1500, WreckRangeM = 400, MinSizePx = 28, PadPx = 6;
        static readonly ProfilerMarker Marker = new("WorldOverlaySystem.Update");
        sealed class Tag
        {
            internal VisualElement Root; internal Label Label;
            internal string Name; internal int Distance = -1; internal int Classes = -1;
        }
        struct Candidate : System.IComparable<Candidate>
        {
            public float Distance; public Entity Entity; public float3 Min, Max;
            public int CompareTo(Candidate other) { int c = Distance.CompareTo(other.Distance); return c != 0 ? c : Entity.Index.CompareTo(other.Entity.Index); }
        }
        readonly Tag[] tags = new Tag[MaxTags];
        readonly List<Candidate> candidates = new(64);
        readonly System.Text.StringBuilder text = new(64);
        VisualElement root;
        IHudNames names;
        Camera camera;
        EntityQuery grids, players;

        protected override void OnCreate()
        {
            grids = GetEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.Exclude<PlayerShip>());
            players = GetEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalToWorld>());
        }
        public void Bind(VisualElement worldLayer, IHudNames names, Camera camera)
        {
            Unbind();
            if (worldLayer == null || names == null || camera == null) return;
            this.names = names; this.camera = camera;
            root = new VisualElement { name = "world-overlay", pickingMode = PickingMode.Ignore };
            root.AddToClassList("world-overlay");
            for (int i = 0; i < MaxTags; i++)
            {
                var tag = tags[i] = new Tag { Root = new VisualElement { name = "world-tag-" + i, pickingMode = PickingMode.Ignore } };
                tag.Root.AddToClassList("world-tag"); tag.Root.AddToClassList(UIStyleClasses.IsHidden);
                foreach (var corner in new[] { "tl", "tr", "bl", "br" })
                {
                    var piece = new VisualElement { pickingMode = PickingMode.Ignore };
                    piece.AddToClassList("world-tag__corner"); piece.AddToClassList("world-tag__corner--" + corner); tag.Root.Add(piece);
                }
                tag.Label = new Label { pickingMode = PickingMode.Ignore }; tag.Label.AddToClassList("world-tag__label"); tag.Root.Add(tag.Label);
                root.Add(tag.Root);
            }
            worldLayer.Add(root);
        }
        public void Unbind()
        {
            root?.RemoveFromHierarchy(); root = null; names = null; camera = null;
            for (int i = 0; i < MaxTags; i++) tags[i] = null;
        }
        protected override void OnDestroy() => Unbind();

        protected override void OnUpdate()
        {
            if (root == null) return;
            using var scope = Marker.Auto();
            int used = 0;
            if (camera != null && root.panel != null && players.CalculateEntityCount() == 1) used = Gather();
            for (int i = used; i < MaxTags; i++) Hide(tags[i]);
        }
        int Gather()
        {
            var em = EntityManager;
            CompleteDependency();
            var player = players.GetSingletonEntity();
            float3 origin = em.GetComponentData<LocalToWorld>(player).Position;
            byte faction = em.HasComponent<Allegiance>(player) ? em.GetComponentData<Allegiance>(player).Faction : (byte)255;
            bool hasRelations = SystemAPI.TryGetSingleton<FactionRelations>(out var relations);
            var interaction = SystemAPI.TryGetSingleton<InteractionTarget>(out var found) ? found : default;
            candidates.Clear();
            using (var entities = grids.ToEntityArray(Allocator.Temp))
                foreach (var entity in entities)
                {
                    var grid = em.GetComponentData<VoxelGrid>(entity);
                    if (grid.Kind == GridKind.Asteroid) continue;
                    var ltw = em.GetComponentData<LocalToWorld>(entity).Value;
                    float3 min = (float3)grid.CellMin * grid.CellSize, max = ((float3)grid.CellMax + 1) * grid.CellSize;
                    float distance = math.distance(origin, math.transform(ltw, (min + max) * .5f));
                    if (grid.Kind == GridKind.Hulk)
                    {
                        // Debris islands become small hulks; only salvageable or substantial wrecks get a tag.
                        bool salvage = em.HasComponent<Interactable>(entity) && em.GetComponentData<Interactable>(entity).Kind == InteractionKind.Salvage;
                        if (distance > WreckRangeM || !salvage && (!em.HasComponent<GridMass>(entity) || em.GetComponentData<GridMass>(entity).Data.CellCount < MinWreckCells)) continue;
                    }
                    else if (distance > RangeM) continue;
                    candidates.Add(new Candidate { Distance = distance, Entity = entity, Min = min, Max = max });
                }
            candidates.Sort();
            int used = 0;
            for (int c = 0; c < candidates.Count && used < MaxTags; c++)
            {
                var candidate = candidates[c]; var entity = candidate.Entity;
                var ltw = em.GetComponentData<LocalToWorld>(entity).Value;
                if (!Project(ltw, candidate.Min, candidate.Max, out var rect)) continue;
                var grid = em.GetComponentData<VoxelGrid>(entity);
                bool hulk = grid.Kind == GridKind.Hulk;
                bool salvage = hulk && em.HasComponent<Interactable>(entity) && em.GetComponentData<Interactable>(entity).Kind == InteractionKind.Salvage;
                int relation = 0;
                if (!hulk && hasRelations && em.HasComponent<Allegiance>(entity))
                {
                    var stance = relations.Get(faction, em.GetComponentData<Allegiance>(entity).Faction);
                    relation = stance == FactionStance.Friendly ? 1 : stance == FactionStance.Hostile ? 2 : 0;
                }
                bool active = interaction.Target == entity && interaction.InRange != 0;
                var blueprint = em.HasComponent<GridSource>(entity) ? em.GetComponentData<GridSource>(entity).Blueprint : default;
                string name = hulk ? (salvage ? "Wreck  ·  Salvage" : "Wreck")
                    : em.HasComponent<Game.Core.Net.PilotName>(entity) ? PilotText(em.GetComponentData<Game.Core.Net.PilotName>(entity).Value)
                    : blueprint.Value == 0 ? (grid.Kind == GridKind.Station ? "Station" : "Contact")
                    : names.TargetName(blueprint, em.HasComponent<Allegiance>(entity) ? em.GetComponentData<Allegiance>(entity).Faction : (byte)255);
                Show(tags[used++], rect, name, (int)math.round(candidate.Distance / 10) * 10, relation, salvage, active);
            }
            return used;
        }
        bool Project(float4x4 ltw, float3 min, float3 max, out Rect rect)
        {
            rect = default;
            float2 lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                float3 corner = new float3((i & 1) != 0 ? max.x : min.x, (i & 2) != 0 ? max.y : min.y, (i & 4) != 0 ? max.z : min.z);
                Vector3 world = math.transform(ltw, corner);
                if (camera.WorldToViewportPoint(world).z <= camera.nearClipPlane) return false;
                Vector2 point = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, camera);
                lo = math.min(lo, point); hi = math.max(hi, point);
            }
            float2 size = math.max(hi - lo + 2 * PadPx, MinSizePx), centre = (lo + hi) * .5f;
            var bounds = root.panel.visualTree.layout;
            if (centre.x + size.x * .5f < 0 || centre.y + size.y * .5f < 0 || centre.x - size.x * .5f > bounds.width || centre.y - size.y * .5f > bounds.height) return false;
            rect = new Rect(centre.x - size.x * .5f, centre.y - size.y * .5f, size.x, size.y);
            return true;
        }
        // Co-op pilot names: cached so the HUD does not allocate a string every frame.
        readonly Dictionary<FixedString32Bytes, string> pilotTexts = new();
        string PilotText(in FixedString32Bytes name)
        {
            if (!pilotTexts.TryGetValue(name, out var text)) pilotTexts[name] = text = name.ToString();
            return text;
        }
        void Show(Tag tag, Rect rect, string name, int distance, int relation, bool salvage, bool active)
        {
            var style = tag.Root.style;
            style.left = rect.x; style.top = rect.y; style.width = rect.width; style.height = rect.height;
            int classes = relation | (salvage ? 4 : 0) | (active ? 8 : 0);
            if (classes != tag.Classes)
            {
                tag.Classes = classes;
                tag.Root.EnableInClassList("world-tag--ally", relation == 1); tag.Root.EnableInClassList("world-tag--enemy", relation == 2);
                tag.Root.EnableInClassList("world-tag--salvage", salvage); tag.Root.EnableInClassList("world-tag--active", active);
            }
            if (!ReferenceEquals(name, tag.Name) || distance != tag.Distance)
            {
                tag.Name = name; tag.Distance = distance;
                text.Clear().Append(name).Append("  ·  ").Append(distance).Append(" m");
                tag.Label.text = text.ToString();
            }
            tag.Root.RemoveFromClassList(UIStyleClasses.IsHidden);
        }
        static void Hide(Tag tag)
        {
            if (tag == null || tag.Root.ClassListContains(UIStyleClasses.IsHidden)) return;
            tag.Root.AddToClassList(UIStyleClasses.IsHidden);
        }
    }
}
