using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Narrative.ShipLog;
using Game.UI.Data;
using Game.UI.ViewModels;
using Game.UI.Views;
using Unity.Mathematics;

namespace Game.Presentation.Presenters
{
    /// <summary>HUD-016: canonical galaxy, ownership, narrative markers and travel route projected into retained map data.</summary>
    public sealed class GalaxyMapPresenter : IGalaxyMapCommands, IDisposable
    {
        readonly GalaxyMapViewModel model;
        readonly GalaxyService galaxy;
        readonly GalaxyMapModel map;
        readonly TravelService travel;
        readonly MapData data = new();
        readonly List<StarSystemId> ids = new();
        readonly Dictionary<uint, int> indices = new();
        readonly HashSet<(uint, uint)> route = new();
        GalaxyMapView view;
        ShipLogQuery query;
        StarSystemId selected;
        uint mapRevision, queryRevision, laneRevision, destination;
        bool bound, dirty = true, disposed;
        public GalaxyMapPresenter(GalaxyMapViewModel model, GalaxyService galaxy, GalaxyMapModel map, TravelService travel)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.travel = travel ?? throw new ArgumentNullException(nameof(travel)); model.Data = data;
        }
        public void Bind(GalaxyMapView view, ShipLogQuery query)
        {
            if (disposed) throw new ObjectDisposedException(nameof(GalaxyMapPresenter));
            if (view == null || query == null) throw new ArgumentNullException("The production map view and ship-log query are required.");
            Unbind(); this.view = view; this.query = query; bound = true;
            view.Pushed += Pushed; query.Changed += Changed;
            EventBus<StarSystemEntered>.Subscribe(Entered); EventBus<FactChanged>.Subscribe(FactChanged); dirty = true;
        }
        public void Unbind()
        {
            if (bound)
            {
                view.Pushed -= Pushed; query.Changed -= Changed;
                EventBus<StarSystemEntered>.Unsubscribe(Entered); EventBus<FactChanged>.Unsubscribe(FactChanged);
            }
            bound = false; view = null; query = null; selected = default;
            data.Points.Clear(); data.Lines.Clear(); data.Markers.Clear(); data.Rings.Clear(); ids.Clear(); indices.Clear();
            model.SelectedIndex = -1; model.SelectedName = model.SelectedInfo = ""; model.CanSetDestination = false;
            model.DestinationBlockedReason = "Select a star system."; model.DataVersion++;
        }
        public void Reset() { if (bound) { var currentView = view; var currentQuery = query; Bind(currentView, currentQuery); } }
        void Changed() => dirty = true;
        void Entered(StarSystemEntered value) => dirty = true;
        void FactChanged(FactChanged value) => dirty = true;
        void Pushed(object args)
        {
            selected = args is StarSystemId id && id.IsValid ? id : galaxy.State.CurrentStarSystem;
            Refresh();
            if (indices.TryGetValue(selected.Value, out int index)) view.FocusMap(data.Points[index].Pos);
            else FocusCurrent();
        }
        public void Tick()
        {
            if (!bound || !view.IsOpen) return;
            map.Refresh();
            if (dirty || map.Revision != mapRevision || query.Revision != queryRevision || travel.Revision != laneRevision ||
                travel.Destination.Value != destination) Refresh();
        }
        public void Refresh()
        {
            if (!bound) return;
            map.Refresh(); data.Points.Clear(); data.Lines.Clear(); data.Markers.Clear(); data.Rings.Clear(); ids.Clear(); indices.Clear(); route.Clear();
            foreach (var entry in map.StarSystems)
            {
                var flags = MapPointFlags.Known | MapPointFlags.Interactable;
                if (entry.Visited) flags |= MapPointFlags.Visited;
                if (entry.Current) flags |= MapPointFlags.Current;
                Add(entry.Id, new MapPoint { Pos = entry.PositionLy, Name = entry.Name, RadiusPx = entry.IsStory ? 5 : 3,
                    Flags = flags, ColorToken = entry.OwnerColorToken });
            }
            var generated = galaxy.Galaxy;
            if (generated != null)
                foreach (var marker in query.Markers)
                {
                    if (marker.Kind != NarrativeMarkerKind.Rumor || marker.StarSystem.IsNone) continue;
                    var target = StarSystemId.FromAuthored(marker.StarSystem.Hash);
                    if (!generated.TryGetIndex(target, out int at)) continue;
                    var position = generated.StarSystems[at].PositionLy;
                    if (!indices.ContainsKey(target.Value))
                        Add(target, new MapPoint { Pos = position, Name = "Uncharted", RadiusPx = 4, Flags = MapPointFlags.Interactable });
                    data.Markers.Add(new MapMarker { Pos = position, Kind = MapMarkerKind.Rumor, Label = marker.Label });
                }
            var previous = galaxy.State.CurrentStarSystem;
            foreach (var next in travel.Route) { route.Add(Edge(previous, next)); previous = next; }
            foreach (var lane in map.Lanes)
                if (indices.TryGetValue(lane.A.Value, out int a) && indices.TryGetValue(lane.B.Value, out int b))
                    data.Lines.Add(new MapLine { A = a, B = b, Known = true, Gated = !lane.Open, Route = route.Contains(Edge(lane.A, lane.B)) });
            if (!selected.IsValid || !indices.ContainsKey(selected.Value)) selected = galaxy.State.CurrentStarSystem;
            for (int i = 0; i < ids.Count; i++)
            {
                var point = data.Points[i];
                if (ids[i].Equals(selected)) point.Flags |= MapPointFlags.Selected;
                if (ids[i].Equals(travel.Destination)) point.Flags |= MapPointFlags.Destination;
                data.Points[i] = point;
                if ((point.Flags & MapPointFlags.Current) != 0) data.Markers.Add(new MapMarker { Pos = point.Pos, Kind = MapMarkerKind.Player, Label = "Current system" });
                if ((point.Flags & MapPointFlags.Destination) != 0) data.Markers.Add(new MapMarker { Pos = point.Pos, Kind = MapMarkerKind.Destination, Label = "Destination" });
            }
            Bounds(); UpdateSelection(); model.DataVersion++;
            mapRevision = map.Revision; queryRevision = query.Revision; laneRevision = travel.Revision; destination = travel.Destination.Value; dirty = false;
        }
        void Add(StarSystemId id, MapPoint point) { indices.Add(id.Value, ids.Count); ids.Add(id); data.Points.Add(point); }
        static (uint, uint) Edge(StarSystemId a, StarSystemId b) => a.Value < b.Value ? (a.Value, b.Value) : (b.Value, a.Value);
        void Bounds()
        {
            double2 min = new(-1), max = new(1);
            if (data.Points.Count > 0)
            {
                min = max = data.Points[0].Pos;
                foreach (var point in data.Points) { min = math.min(min, point.Pos); max = math.max(max, point.Pos); }
                if (math.cmax(max - min) < 1) { min -= 1; max += 1; }
            }
            data.BoundsMin = min; data.BoundsMax = max;
        }
        void UpdateSelection()
        {
            model.SelectedIndex = indices.TryGetValue(selected.Value, out int index) ? index : -1;
            if (model.SelectedIndex < 0)
            { model.SelectedName = "No star system"; model.SelectedInfo = "Start a campaign to chart a route."; model.CanSetDestination = false; model.DestinationBlockedReason = "No galaxy is loaded."; return; }
            var point = data.Points[index]; model.SelectedName = point.Name;
            model.CanSetDestination = travel.CanSetDestination(selected, out string reason); model.DestinationBlockedReason = reason;
            model.SelectedInfo = (point.Flags & MapPointFlags.Current) != 0 ? "Current star system" :
                (point.Flags & MapPointFlags.Visited) != 0 ? "Visited star system" : (point.Flags & MapPointFlags.Known) != 0 ? "Known star system" : "Rumored location";
            if (travel.Destination.IsValid) model.SelectedInfo += travel.RouteStatus == Game.Galaxy.Travel.RouteStatus.Ok
                ? "\nRoute: " + travel.Route.Count + " jumps, " + travel.RouteFuelCost + " fuel" : "\n" + travel.RouteBlockedReason;
        }
        public void Select(int pointIndex)
        { if (!bound || (uint)pointIndex >= (uint)ids.Count) return; selected = ids[pointIndex]; Refresh(); }
        public void SetDestination()
        {
            if (!bound || !selected.IsValid) return;
            if (!travel.SetDestination(selected, out string reason)) { model.CanSetDestination = false; model.DestinationBlockedReason = reason; return; }
            Refresh();
        }
        public void ClearDestination() { if (bound) { travel.ClearDestination(); Refresh(); } }
        public void FocusCurrent()
        {
            if (!bound || !view.IsBuilt) return;
            if (indices.TryGetValue(galaxy.State.CurrentStarSystem.Value, out int index)) view.FocusMap(data.Points[index].Pos, false);
        }
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; }
    }
}
