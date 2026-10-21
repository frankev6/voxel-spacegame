using System;
using System.Collections.Generic;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Galaxy.Interaction;
using Game.Presentation.Components;
using Game.Presentation.Presenters;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.UI.Data;
using Game.UI.Views;
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
    /// <summary>World-anchored HUD (QA-001 tags, reworked by the HUD redesign of 2026-10-21). A ship, station or wreck gets a corner
    /// bracket and a name/distance label only when it matters: under the cursor or auto-aim, locked, the interaction target (then the tag
    /// carries the prompt, "[F] Dock · The Hollow"), a salvageable wreck nearby, or just revealed by a sensor ping. A hovered or locked
    /// ship's bracket carries its shield (outer) and hull (inner) arcs. Contacts off screen get chevrons at the screen edge in relation
    /// colour with their distance (hostiles close together collapse into one, the next gate and points of interest are gold). The
    /// sensor ping (V, and on arrival in a system) sweeps a ring out from the ship and tags what it crosses for a few seconds. Pooled
    /// UI Toolkit elements on the World layer; no element is created after Bind.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    [UpdateAfter(typeof(CameraTargetSyncSystem))]
    public partial class WorldOverlaySystem : SystemBase
    {
        const int MaxTags = 24, MaxEdges = 12, MinWreckCells = 24, RingPoints = 96;
        const float WreckRangeM = 400, MinSizePx = 28, PadPx = 6, EdgeMarginPx = 36, GroupPx = 64;
        /// <summary>Sensor ping: the ring's speed and reach, and how long what it crossed stays tagged.</summary>
        public const float PingSpeedMps = 1800, PingRangeM = 3000, PingHoldSeconds = 4;
        static readonly ProfilerMarker Marker = new("WorldOverlaySystem.Update");
        static readonly Color Enemy = new(1f, .48f, .27f), Ally = new(.36f, .55f, 1f), Neutral = new(.73f, .76f, .82f), Station = new(.224f, .835f, 1f),
            Objective = new(1f, .88f, .54f), Hull = new(.93f, .9f, .855f), ShieldColor = new(.247f, .847f, 1f);
        [Flags] enum Why { None = 0, Hover = 1, Locked = 2, Interaction = 4, Salvage = 8, Pinged = 16 }
        sealed class Tag
        {
            internal VisualElement Root; internal Label Label, Detail;
            internal string Text, DetailText; internal int Classes = -1;
        }
        sealed class Edge { internal Label Label; internal string Text; }
        struct Candidate : IComparable<Candidate>
        {
            public float Distance; public Entity Entity; public float3 Min, Max; public Why Why;
            public int CompareTo(Candidate other) { int c = ((int)other.Why & 7).CompareTo((int)Why & 7); if (c != 0) return c; c = Distance.CompareTo(other.Distance); return c != 0 ? c : Entity.Index.CompareTo(other.Entity.Index); }
        }
        struct ArcMark { public Vector2 Centre; public float Radius, Hull, Shield; public bool HasShield; }
        struct Chevron { public Vector2 Point, Direction; public Color Color; public int Count; public float Distance; public byte Group; }
        readonly Tag[] tags = new Tag[MaxTags];
        readonly Edge[] edges = new Edge[MaxEdges];
        readonly List<Candidate> candidates = new(64);
        readonly List<ArcMark> arcs = new(4);
        readonly List<Chevron> chevrons = new(MaxEdges);
        readonly Vector2[] ring = new Vector2[RingPoints + 1];
        readonly bool[] ringVisible = new bool[RingPoints + 1];
        readonly Dictionary<Entity, double> pinged = new();
        readonly List<Entity> expired = new();
        readonly System.Text.StringBuilder text = new(64);
        VisualElement root, canvas;
        IHudNames names;
        Camera camera;
        Func<string, string> keys;
        Func<bool> pingPressed;
        EntityQuery grids, players;
        double pingAt = double.NegativeInfinity;
        float ringAlpha, pulseAlpha, pulseRadius;
        Vector2 pulseCentre;
        uint lastSystem;

        protected override void OnCreate()
        {
            // scenery (SpatialScenery: the Hollow's rock tiles) gets no tag: no floating name over every piece of rock
            grids = GetEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.Exclude<PlayerShip>(),
                ComponentType.Exclude<Game.Simulation.Spatial.SpatialScenery>());
            players = GetEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalToWorld>());
        }
        /// <param name="keys">Binding display ("Flight/Interact" → "F") for the interaction tag.</param>
        /// <param name="pingPressed">True on the frame the sensor ping key is pressed.</param>
        public void Bind(VisualElement worldLayer, IHudNames names, Camera camera, Func<string, string> keys = null, Func<bool> pingPressed = null)
        {
            Unbind();
            if (worldLayer == null || names == null || camera == null) return;
            this.names = names; this.camera = camera; this.keys = keys; this.pingPressed = pingPressed;
            root = new VisualElement { name = "world-overlay", pickingMode = PickingMode.Ignore };
            root.AddToClassList("world-overlay");
            canvas = new VisualElement { name = "world-overlay-canvas", pickingMode = PickingMode.Ignore };
            canvas.AddToClassList("world-overlay__canvas"); canvas.generateVisualContent += Draw; root.Add(canvas);
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
                tag.Detail = new Label { pickingMode = PickingMode.Ignore }; tag.Detail.AddToClassList("world-tag__label"); tag.Detail.AddToClassList("world-tag__detail");
                tag.Detail.AddToClassList(UIStyleClasses.IsHidden); tag.Root.Add(tag.Detail);
                root.Add(tag.Root);
            }
            for (int i = 0; i < MaxEdges; i++)
            {
                var edge = edges[i] = new Edge { Label = new Label { name = "world-edge-" + i, pickingMode = PickingMode.Ignore } };
                edge.Label.AddToClassList("world-edge"); edge.Label.AddToClassList(UIStyleClasses.IsHidden); root.Add(edge.Label);
            }
            worldLayer.Add(root);
        }
        public void Unbind()
        {
            root?.RemoveFromHierarchy(); root = null; canvas = null; names = null; camera = null; keys = null; pingPressed = null;
            for (int i = 0; i < MaxTags; i++) tags[i] = null;
            for (int i = 0; i < MaxEdges; i++) edges[i] = null;
            pinged.Clear(); pingAt = double.NegativeInfinity; lastSystem = 0;
        }
        protected override void OnDestroy() => Unbind();
        /// <summary>Starts a sensor ping from the player's ship (the V key, arrival in a system, or the dev console).</summary>
        public void Ping() => pingAt = UnityEngine.Time.unscaledTimeAsDouble;
        /// <summary>True while a ping ring is still sweeping outward.</summary>
        public bool PingActive => UnityEngine.Time.unscaledTimeAsDouble - pingAt < PingRangeM / PingSpeedMps;

        protected override void OnUpdate()
        {
            if (root == null) return;
            using var scope = Marker.Auto();
            int used = 0, edgeCount = 0;
            arcs.Clear(); chevrons.Clear(); ringAlpha = 0; pulseAlpha = 0;
            if (camera != null && root.panel != null && players.CalculateEntityCount() == 1)
            {
                if (pingPressed != null && pingPressed()) Ping();
                if (SystemAPI.TryGetSingleton<HudSnapshot>(out var snapshot) && snapshot.StarSystemIdValue != 0 && snapshot.StarSystemIdValue != lastSystem)
                { lastSystem = snapshot.StarSystemIdValue; Ping(); }   // arriving in a system: the world answers
                used = Gather(); edgeCount = Edges();
            }
            for (int i = used; i < MaxTags; i++) Hide(tags[i]);
            for (int i = edgeCount; i < MaxEdges; i++) Hide(edges[i].Label);
            canvas.MarkDirtyRepaint();
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
            var sampler = World.GetExistingSystemManaged<Game.Presentation.Input.PlayerInputSampleSystem>();
            Entity hover = sampler != null ? sampler.HoverEntity : Entity.Null;
            Entity locked = em.HasComponent<Game.Simulation.Weapons.ShipTarget>(player) ? em.GetComponentData<Game.Simulation.Weapons.ShipTarget>(player).Value : Entity.Null;
            double now = UnityEngine.Time.unscaledTimeAsDouble;
            float sweep = (float)(now - pingAt) * PingSpeedMps;
            bool sweeping = sweep >= 0 && sweep <= PingRangeM;
            if (sweeping)
            {
                // the world ring only reads from a steep camera; a low one sees it edge-on as a line across the screen
                if (camera.transform.forward.y < -.55f) Ring(origin, sweep, 1 - sweep / PingRangeM);
                // the pulse also leaves the ship on screen, so a low camera that sees the ring edge-on still sees the ping
                Vector3 ship = origin;
                pulseAlpha = camera.WorldToViewportPoint(ship).z > camera.nearClipPlane ? 1 - sweep / PingRangeM : 0;
                if (pulseAlpha > 0)
                {
                    var bounds = root.panel.visualTree.layout;
                    pulseCentre = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, ship, camera);
                    pulseRadius = sweep / PingRangeM * math.length(new float2(bounds.width, bounds.height)) * .6f;
                }
            }
            expired.Clear();
            foreach (var pair in pinged) if (pair.Value < now) expired.Add(pair.Key);
            foreach (var entity in expired) pinged.Remove(entity);
            candidates.Clear();
            using (var entities = grids.ToEntityArray(Allocator.Temp))
                foreach (var entity in entities)
                {
                    var grid = em.GetComponentData<VoxelGrid>(entity);
                    if (grid.Kind == GridKind.Asteroid) continue;
                    if (grid.Kind == GridKind.Station && em.HasComponent<GridSource>(entity) && names.IsTrackPart(em.GetComponentData<GridSource>(entity).Blueprint)) continue;
                    var ltw = em.GetComponentData<LocalToWorld>(entity).Value;
                    float3 min = (float3)grid.CellMin * grid.CellSize, max = ((float3)grid.CellMax + 1) * grid.CellSize;
                    float distance = math.distance(origin, math.transform(ltw, (min + max) * .5f));
                    var why = Why.None;
                    if (entity == hover) why |= Why.Hover;
                    if (entity == locked) why |= Why.Locked;
                    if (entity == interaction.Target) why |= Why.Interaction;
                    if (grid.Kind == GridKind.Hulk)
                    {
                        // Debris islands become small hulks; only salvageable or substantial wrecks are worth a tag.
                        bool salvage = em.HasComponent<Interactable>(entity) && em.GetComponentData<Interactable>(entity).Kind == InteractionKind.Salvage;
                        if (salvage && distance <= WreckRangeM) why |= Why.Salvage;
                        if (!salvage && (!em.HasComponent<GridMass>(entity) || em.GetComponentData<GridMass>(entity).Data.CellCount < MinWreckCells)) { if ((why & (Why.Hover | Why.Locked)) == 0) continue; }
                    }
                    if (sweeping && distance <= sweep) pinged[entity] = now + PingHoldSeconds;
                    if (pinged.ContainsKey(entity)) why |= Why.Pinged;
                    if (why == Why.None) continue;
                    candidates.Add(new Candidate { Distance = distance, Entity = entity, Min = min, Max = max, Why = why });
                }
            candidates.Sort();
            int used = 0; bool interactionTagged = false;
            for (int c = 0; c < candidates.Count && used < MaxTags; c++)
            {
                var candidate = candidates[c]; var entity = candidate.Entity;
                var ltw = em.GetComponentData<LocalToWorld>(entity).Value;
                if (!Project(ltw, candidate.Min, candidate.Max, out var rect)) continue;
                var grid = em.GetComponentData<VoxelGrid>(entity);
                bool hulk = grid.Kind == GridKind.Hulk, salvage = (candidate.Why & Why.Salvage) != 0 || hulk && em.HasComponent<Interactable>(entity) && em.GetComponentData<Interactable>(entity).Kind == InteractionKind.Salvage;
                int relation = 0;
                if (!hulk && hasRelations && em.HasComponent<Allegiance>(entity))
                {
                    var stance = relations.Get(faction, em.GetComponentData<Allegiance>(entity).Faction);
                    relation = stance == FactionStance.Friendly ? 1 : stance == FactionStance.Hostile ? 2 : 0;
                }
                bool focus = (candidate.Why & (Why.Hover | Why.Locked)) != 0;
                bool isInteraction = (candidate.Why & Why.Interaction) != 0;
                var blueprint = em.HasComponent<GridSource>(entity) ? em.GetComponentData<GridSource>(entity).Blueprint : default;
                string name = hulk ? (salvage ? "Wreck  ·  Salvage" : "Wreck")
                    : em.HasComponent<Game.Core.Net.PilotName>(entity) ? PilotText(em.GetComponentData<Game.Core.Net.PilotName>(entity).Value)
                    : blueprint.Value == 0 ? (grid.Kind == GridKind.Station ? "Station" : "Contact")
                    : names.TargetName(blueprint, em.HasComponent<Allegiance>(entity) ? em.GetComponentData<Allegiance>(entity).Faction : (byte)255);
                string detail = null; bool blocked = false, inRange = true;
                if (!(isInteraction && Prompt(name, out detail, out blocked, out inRange))) text.Clear().Append(name).Append("  ·  ").Append(Distance(candidate.Distance));
                else interactionTagged = true;
                Show(tags[used++], rect, relation, salvage, focus || isInteraction && inRange, isInteraction, !inRange, (candidate.Why & ~Why.Pinged) == 0, detail, blocked);
                if (focus && !hulk) Bracket(entity, rect);
            }
            // a gate, planet or beacon has no grid: its tag sits on its position
            var point = interaction.Target;
            if (!interactionTagged && used < MaxTags && point != Entity.Null && em.Exists(point) && em.HasComponent<LocalToWorld>(point) && !em.HasComponent<VoxelGrid>(point))
            {
                Vector3 world = em.GetComponentData<LocalToWorld>(point).Position;
                if (camera.WorldToViewportPoint(world).z > camera.nearClipPlane && Prompt("", out var detail, out bool blocked, out bool inRange))
                {
                    Vector2 at = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, camera);
                    Show(tags[used++], new Rect(at.x - 20, at.y - 20, 40, 40), 0, false, inRange, true, !inRange, false, detail, blocked);
                }
            }
            return used;
        }
        /// <summary>K: the interaction prompt on the object it is about ("[F]  Dock  ·  The Hollow"), into <see cref="text"/>.</summary>
        bool Prompt(string fallback, out string detail, out bool blocked, out bool inRange)
        {
            detail = null; blocked = false; inRange = true;
            if (!SystemAPI.TryGetSingleton<HudSnapshot>(out var snapshot) || (snapshot.Flags & HudFlags.HasInteraction) == 0) return false;
            var prompt = names.Interaction(snapshot);
            string verb = snapshot.InteractionKind switch { 1 => "Dock", 2 => "Inspect", 3 => "Jump", 4 => "Hail", 5 => "Salvage", _ => "" };
            text.Clear().Append('[').Append(keys?.Invoke("Flight/Interact") ?? "F").Append("]  ").Append(verb).Append("  ·  ").Append(string.IsNullOrEmpty(prompt.Name) ? fallback : prompt.Name);
            detail = string.IsNullOrEmpty(prompt.Detail) ? null : prompt.Detail; blocked = prompt.Blocked; inRange = snapshot.InteractionInRange != 0;
            return true;
        }
        /// <summary>C: the shield (outer) and hull (inner) arcs over a hovered or locked ship's bracket.</summary>
        void Bracket(Entity entity, Rect rect)
        {
            var em = EntityManager;
            float hull = em.HasComponent<ShipStats>(entity) ? em.GetComponentData<ShipStats>(entity).MassFraction : 1;
            bool hasShield = em.HasComponent<ShieldState>(entity) && em.GetComponentData<ShieldState>(entity).MaxHp > 0;
            float shield = hasShield ? em.GetComponentData<ShieldState>(entity).Hp / em.GetComponentData<ShieldState>(entity).MaxHp : 0;
            arcs.Add(new ArcMark { Centre = rect.center, Radius = Mathf.Max(rect.width, rect.height) * .5f + 6, Hull = hull, Shield = shield, HasShield = hasShield });
        }
        /// <summary>I: chevrons at the screen edge for contacts outside the view (HudSyncSystem's contact list, true offsets).</summary>
        int Edges()
        {
            var hud = World.GetExistingSystemManaged<HudSyncSystem>();
            if (hud == null) return 0;
            var contacts = hud.Blips;
            var player = players.GetSingletonEntity();
            float3 origin = EntityManager.GetComponentData<LocalToWorld>(player).Position;
            var bounds = root.panel.visualTree.layout;
            if (!(bounds.width > 0 && bounds.height > 0)) return 0;
            int stations = 0;
            for (int i = 0; i < contacts.Length; i++)
            {
                var contact = contacts[i];
                bool waypoint = (contact.Flags & 4) != 0;
                // hostiles and allies, the next gate and points of interest, and the two nearest stations; neutral traffic, bodies
                // and wrecks get no chevron (in the Hollow they would ring the screen)
                if (!waypoint && contact.Kind != 3 && !(contact.Kind == 0 && contact.Relation != 0) && contact.Kind != 1) continue;
                var offset = contact.OffsetXZ;
                if (!float.IsFinite(offset.x) || !float.IsFinite(offset.y)) continue;
                Vector3 world = new(origin.x + offset.x, origin.y, origin.z + offset.y);
                var viewport = camera.WorldToViewportPoint(world);
                bool ahead = viewport.z > camera.nearClipPlane;
                Vector2 point = new(viewport.x * bounds.width, (1 - viewport.y) * bounds.height);
                if (ahead && point.x > EdgeMarginPx && point.x < bounds.width - EdgeMarginPx && point.y > EdgeMarginPx && point.y < bounds.height - EdgeMarginPx) continue;
                Vector2 centre = new(bounds.width * .5f, bounds.height * .5f), direction = point - centre;
                if (!ahead) direction = -direction;
                if (direction.sqrMagnitude < 1e-4f) direction = Vector2.down;
                float scale = Mathf.Min((centre.x - EdgeMarginPx) / Mathf.Max(1e-3f, Mathf.Abs(direction.x)), (centre.y - EdgeMarginPx) / Mathf.Max(1e-3f, Mathf.Abs(direction.y)));
                Vector2 at = centre + direction * scale;
                byte group = waypoint || contact.Kind == 3 ? (byte)3 : contact.Kind == 1 ? (byte)4 : contact.Relation;
                float distance = offset.magnitude;
                int merged = -1;
                if (group == 4 && stations >= 2) continue;
                if (group != 3 && group != 4)
                    for (int c = 0; c < chevrons.Count; c++)
                        if (chevrons[c].Group == group && (chevrons[c].Point - at).sqrMagnitude < GroupPx * GroupPx) { merged = c; break; }
                if (merged >= 0)
                {
                    var chevron = chevrons[merged]; chevron.Count++; chevron.Distance = Mathf.Min(chevron.Distance, distance); chevrons[merged] = chevron;
                    continue;
                }
                if (chevrons.Count >= MaxEdges) continue;
                if (group == 4) stations++;
                var color = group == 3 ? Objective : group == 4 ? Station : group == 2 ? Enemy : group == 1 ? Ally : Neutral;
                chevrons.Add(new Chevron { Point = at, Direction = direction.normalized, Color = color, Count = 1, Distance = distance, Group = group });
            }
            for (int i = 0; i < chevrons.Count; i++)
            {
                var chevron = chevrons[i]; var edge = edges[i];
                text.Clear();
                if (chevron.Count > 1) text.Append(chevron.Count).Append(chevron.Group == 2 ? " hostiles" : chevron.Group == 1 ? " allies" : " contacts").Append("  ·  ");
                else if (chevron.Group == 4) text.Append("Station  ·  ");
                text.Append(Distance(chevron.Distance));
                if (edge.Text == null || !Same(text, edge.Text)) { edge.Text = text.ToString(); edge.Label.text = edge.Text; }
                Vector2 label = chevron.Point - chevron.Direction * 22;
                edge.Label.style.left = label.x; edge.Label.style.top = label.y; edge.Label.style.color = chevron.Color;
                edge.Label.RemoveFromClassList(UIStyleClasses.IsHidden);
            }
            return chevrons.Count;
        }
        /// <summary>J: the ping ring, a world circle on the flight plane projected point by point.</summary>
        void Ring(float3 origin, float radius, float alpha)
        {
            ringAlpha = math.saturate(alpha);
            for (int i = 0; i <= RingPoints; i++)
            {
                float angle = i * math.PI * 2 / RingPoints;
                Vector3 world = new(origin.x + math.sin(angle) * radius, origin.y, origin.z + math.cos(angle) * radius);
                ringVisible[i] = camera.WorldToViewportPoint(world).z > camera.nearClipPlane;
                if (ringVisible[i]) ring[i] = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, world, camera);
            }
        }
        void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.lineCap = LineCap.Round; painter.lineJoin = LineJoin.Round;
            if (ringAlpha > .01f)
            {
                painter.lineWidth = 2; painter.strokeColor = new Color(Station.r, Station.g, Station.b, .4f * ringAlpha);
                bool open = false;
                for (int i = 0; i <= RingPoints; i++)
                {
                    if (!ringVisible[i]) { if (open) painter.Stroke(); open = false; continue; }
                    if (!open) { painter.BeginPath(); painter.MoveTo(ring[i]); open = true; } else painter.LineTo(ring[i]);
                }
                if (open) painter.Stroke();
            }
            if (pulseAlpha > .01f)
            {
                painter.lineWidth = 3; painter.strokeColor = new Color(Station.r, Station.g, Station.b, .5f * pulseAlpha);
                painter.BeginPath(); painter.Arc(pulseCentre, pulseRadius, Angle.Degrees(0), Angle.Degrees(360)); painter.Stroke();
                painter.lineWidth = 1; painter.strokeColor = new Color(Station.r, Station.g, Station.b, .25f * pulseAlpha);
                painter.BeginPath(); painter.Arc(pulseCentre, pulseRadius * .82f, Angle.Degrees(0), Angle.Degrees(360)); painter.Stroke();
            }
            foreach (var arc in arcs)
            {
                // over the top of the bracket, filling from the left: shield outside, hull inside
                if (arc.HasShield) Arc(painter, arc.Centre, arc.Radius + 5, arc.Shield, ShieldColor);
                Arc(painter, arc.Centre, arc.Radius, arc.Hull, arc.Hull < .3f ? Enemy : Hull);
            }
            foreach (var chevron in chevrons)
            {
                Vector2 tip = chevron.Point + chevron.Direction * 9, side = new Vector2(-chevron.Direction.y, chevron.Direction.x) * 7, back = chevron.Point - chevron.Direction * 3;
                painter.fillColor = chevron.Color;
                painter.BeginPath(); painter.MoveTo(tip); painter.LineTo(back + side); painter.LineTo(chevron.Point); painter.LineTo(back - side); painter.ClosePath(); painter.Fill();
            }
        }
        static void Arc(Painter2D painter, Vector2 centre, float radius, float fraction, Color color)
        {
            const float start = 215, span = 110;
            painter.lineWidth = 2.5f; painter.strokeColor = new Color(1, 1, 1, .18f);
            painter.BeginPath(); painter.Arc(centre, radius, Angle.Degrees(start), Angle.Degrees(start + span)); painter.Stroke();
            fraction = Mathf.Clamp01(fraction);
            if (fraction <= .001f) return;
            painter.strokeColor = color;
            painter.BeginPath(); painter.Arc(centre, radius, Angle.Degrees(start), Angle.Degrees(start + span * fraction)); painter.Stroke();
        }
        static string Distance(float metres) => metres < 1000 ? NumberStringCache.Get((int)math.round(metres / 10) * 10) + " m" : (metres / 1000).ToString("0.0") + " km";
        static bool Same(System.Text.StringBuilder builder, string value)
        {
            if (builder.Length != value.Length) return false;
            for (int i = 0; i < value.Length; i++) if (builder[i] != value[i]) return false;
            return true;
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
            if (!pilotTexts.TryGetValue(name, out var value)) pilotTexts[name] = value = name.ToString();
            return value;
        }
        void Show(Tag tag, Rect rect, int relation, bool salvage, bool active, bool interaction, bool dim, bool faint, string detail, bool blocked)
        {
            var style = tag.Root.style;
            style.left = rect.x; style.top = rect.y; style.width = rect.width; style.height = rect.height;
            int classes = relation | (salvage ? 4 : 0) | (active ? 8 : 0) | (interaction ? 16 : 0) | (dim ? 32 : 0) | (faint ? 64 : 0) | (blocked ? 128 : 0) | (detail != null ? 256 : 0);
            if (classes != tag.Classes)
            {
                tag.Classes = classes;
                tag.Root.EnableInClassList("world-tag--ally", relation == 1); tag.Root.EnableInClassList("world-tag--enemy", relation == 2);
                tag.Root.EnableInClassList("world-tag--salvage", salvage); tag.Root.EnableInClassList("world-tag--active", active);
                tag.Root.EnableInClassList("world-tag--interaction", interaction); tag.Root.EnableInClassList("world-tag--dim", dim);
                tag.Root.EnableInClassList("world-tag--pinged", faint); tag.Detail.EnableInClassList("is-warning", blocked);
                tag.Detail.EnableInClassList(UIStyleClasses.IsHidden, detail == null);
            }
            if (tag.Text == null || !Same(text, tag.Text)) { tag.Text = text.ToString(); tag.Label.text = tag.Text; }
            if (detail != null && !ReferenceEquals(detail, tag.DetailText)) { tag.DetailText = detail; tag.Detail.text = detail; }
            tag.Root.RemoveFromClassList(UIStyleClasses.IsHidden);
        }
        static void Hide(Tag tag)
        {
            if (tag == null || tag.Root.ClassListContains(UIStyleClasses.IsHidden)) return;
            tag.Root.AddToClassList(UIStyleClasses.IsHidden);
        }
        static void Hide(VisualElement element)
        {
            if (element == null || element.ClassListContains(UIStyleClasses.IsHidden)) return;
            element.AddToClassList(UIStyleClasses.IsHidden);
        }
    }
}
