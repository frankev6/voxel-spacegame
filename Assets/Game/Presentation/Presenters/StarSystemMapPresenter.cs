using System;
using System.Collections.Generic;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Narrative.ShipLog;
using Game.Simulation;
using Game.UI.Data;
using Game.UI.ViewModels;
using Game.UI.Views;
using Game.Voxel.Damage;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Presentation.Presenters
{
    /// <summary>HUD-016: double-precision star-system positions/orbits and objective markers, with M3 cruise hidden.</summary>
    public sealed class StarSystemMapPresenter : IStarSystemMapCommands, IDisposable
    {
        readonly StarSystemMapViewModel model;
        readonly GalaxyService galaxy;
        readonly StarSystemLoader loader;
        readonly World world;
        readonly EntityQuery players, origins, clocks;
        readonly MapData data = new();
        readonly List<BodySpec> bodies = new();
        StarSystemMapView view;
        ShipLogQuery query;
        ulong selectedKey;
        double2 playerPosition;
        bool bound, dirty, disposed;
        public StarSystemMapPresenter(StarSystemMapViewModel model, GalaxyService galaxy, StarSystemLoader loader, World world)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            var em = world.EntityManager;
            players = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalTransform>() }, None = new[] { ComponentType.ReadOnly<Hulk>() } });
            origins = em.CreateEntityQuery(ComponentType.ReadOnly<FrameOrigin>()); clocks = em.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
            model.Data = data;
        }
        public void Bind(StarSystemMapView view, ShipLogQuery query)
        {
            if (disposed) throw new ObjectDisposedException(nameof(StarSystemMapPresenter));
            if (view == null || query == null) throw new ArgumentNullException("The production map view and ship-log query are required.");
            Unbind(); this.view = view; this.query = query; bound = true;
            view.Pushed += Pushed; query.Changed += Changed; EventBus<StarSystemEntered>.Subscribe(Entered); dirty = true;
        }
        public void Unbind()
        {
            if (bound) { view.Pushed -= Pushed; query.Changed -= Changed; EventBus<StarSystemEntered>.Unsubscribe(Entered); }
            view = null; query = null; bound = false; selectedKey = 0; bodies.Clear();
            data.Points.Clear(); data.Lines.Clear(); data.Rings.Clear(); data.Markers.Clear();
            model.SelectedIndex = -1; model.SelectedName = model.SelectedInfo = ""; model.CanSetWaypoint = false; model.DataVersion++;
        }
        public void Reset() { if (bound) { var currentView = view; var currentQuery = query; Bind(currentView, currentQuery); } }
        void Changed() => dirty = true;
        void Entered(StarSystemEntered value) => dirty = true;
        void Pushed(object args) { Refresh(); view.FocusMap(playerPosition); }
        public void Tick() { if (bound && view.IsOpen && dirty) Refresh(); }
        public void Refresh()
        {
            if (!bound) return;
            data.Points.Clear(); data.Lines.Clear(); data.Rings.Clear(); data.Markers.Clear(); bodies.Clear();
            var em = world.EntityManager; em.CompleteDependencyBeforeRO<LocalTransform>(); em.CompleteDependencyBeforeRO<FrameOrigin>(); em.CompleteDependencyBeforeRO<SimClock>();
            double time = clocks.IsEmptyIgnoreFilter ? 0 : clocks.GetSingleton<SimClock>().UniversalTime;
            playerPosition = default;
            if (players.CalculateEntityCount() == 1)
            {
                var transform = em.GetComponentData<LocalTransform>(players.GetSingletonEntity());
                double3 origin = origins.IsEmptyIgnoreFilter ? default : origins.GetSingleton<FrameOrigin>().OriginInStarSystem;
                playerPosition = (origin + (double3)transform.Position).xz;
                float3 forward = math.mul(transform.Rotation, new float3(0, 0, 1));
                data.Markers.Add(new MapMarker { Pos = playerPosition, Kind = MapMarkerKind.Player, Label = "Player", HeadingDeg = math.degrees(math.atan2(forward.x, forward.z)) });
            }
            double2 min = playerPosition - 1, max = playerPosition + 1;
            if (galaxy.Galaxy != null && loader.Loaded.IsValid)
            {
                var content = galaxy.GetContent(loader.Loaded);
                foreach (var body in content.Bodies)
                {
                    if ((body.State & BodyStateFlags.Destroyed) != 0) continue;
                    double2 position = OrbitMath.Position(body, time).xz;
                    bodies.Add(body); data.Points.Add(new MapPoint { Pos = position, Name = galaxy.GetBodyName(body.Id), RadiusPx = body.Id.Role == BodyRole.Star ? 7 : 4,
                        Flags = MapPointFlags.Known | MapPointFlags.Interactable, ColorIndex = body.Id.Role == BodyRole.LaneGate ? (byte)5 : (byte)0 });
                    min = math.min(min, position - body.RadiusM); max = math.max(max, position + body.RadiusM);
                    double2 center = OrbitMath.LocalPosition(body.ParentOrbit, time).xz;
                    if (body.Orbit.Mode != OrbitMode.Fixed && body.Orbit.RadiusM > 0)
                    {
                        data.Rings.Add(new MapRing { Center = center, Radius = body.Orbit.RadiusM });
                        min = math.min(min, center - body.Orbit.RadiusM); max = math.max(max, center + body.Orbit.RadiusM);
                    }
                    if (body.Id.Role == BodyRole.Belt)
                    {
                        if (body.BeltInnerRadiusM > 0) data.Rings.Add(new MapRing { Center = default, Radius = body.BeltInnerRadiusM });
                        if (body.BeltOuterRadiusM > 0)
                        { data.Rings.Add(new MapRing { Center = default, Radius = body.BeltOuterRadiusM }); min = math.min(min, new double2(-body.BeltOuterRadiusM)); max = math.max(max, new double2(body.BeltOuterRadiusM)); }
                    }
                }
                foreach (var marker in query.Markers)
                {
                    if (marker.Kind != NarrativeMarkerKind.Objective || marker.StarSystem.IsNone ||
                        !StarSystemId.FromAuthored(marker.StarSystem.Hash).Equals(loader.Loaded)) continue;
                    if (string.IsNullOrEmpty(marker.BodyTag))
                        data.Markers.Add(new MapMarker { Pos = default, Kind = MapMarkerKind.Waypoint, Label = marker.Label });
                    else
                        for (int i = 0; i < bodies.Count; i++)
                            if (string.Equals(galaxy.GetBodyTemplate(bodies[i].TemplateIndex).StoryTag, marker.BodyTag, StringComparison.Ordinal))
                                data.Markers.Add(new MapMarker { Pos = data.Points[i].Pos, Kind = MapMarkerKind.Waypoint, Label = marker.Label });
                }
            }
            data.BoundsMin = min; data.BoundsMax = max;
            int selected = -1;
            for (int i = 0; i < bodies.Count; i++) if (bodies[i].Id.LocalKey == selectedKey) { selected = i; break; }
            if (selected < 0 && bodies.Count > 0) selected = 0;
            SelectCore(selected); model.DataVersion++; dirty = false;
        }
        void SelectCore(int index)
        {
            model.SelectedIndex = index; model.CanSetWaypoint = false;
            if (index < 0) { selectedKey = 0; model.SelectedName = "No star system"; model.SelectedInfo = "Enter space to view local bodies."; return; }
            selectedKey = bodies[index].Id.LocalKey;
            for (int i = 0; i < data.Points.Count; i++)
            { var point = data.Points[i]; point.Flags &= ~MapPointFlags.Selected; if (i == index) point.Flags |= MapPointFlags.Selected; data.Points[i] = point; }
            model.SelectedName = data.Points[index].Name;
            model.SelectedInfo = bodies[index].Id.Role + "\n" + (math.distance(playerPosition, data.Points[index].Pos) / 1000).ToString("N1", System.Globalization.CultureInfo.InvariantCulture) + " km from player";
        }
        public void Select(int pointIndex)
        { if (!bound || (uint)pointIndex >= (uint)bodies.Count) return; SelectCore(pointIndex); model.DataVersion++; }
        public void SetWaypoint() { /* SPEC-61 M3: hidden until the real M4 cruise owner exists. */ }
        public void FocusPlayer() { if (bound && view.IsBuilt) view.FocusMap(playerPosition, false); }
        public void Dispose()
        { if (disposed) return; Unbind(); disposed = true; if (world.IsCreated) { players.Dispose(); origins.Dispose(); clocks.Dispose(); } }
    }
}
