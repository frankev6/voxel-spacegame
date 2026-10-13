using System;
using System.Collections.Generic;
using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>Read-only galaxy map contract for the UI (SPEC-30 §4.8, §5.12).</summary>
    public sealed class GalaxyMapModel
    {
        const byte KnownFlag = 1, VisitedFlag = 2;
        readonly GalaxyService galaxy;
        readonly ILaneAccess lanes;
        readonly IStarSystemOwnership ownership;
        readonly List<StarSystemMapEntry> starSystems = new();
        readonly List<LaneMapEntry> laneEntries = new();
        byte[] visibility = Array.Empty<byte>();
        GalaxyDef lastGalaxy;
        uint lastStateRevision, lastLaneRevision;
        int lastGeneration;
        StarSystemId lastCurrent;
        bool hasStamp;

        public GalaxyMapModel(GalaxyService galaxy, ILaneAccess lanes, IStarSystemOwnership ownership)
        {
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.lanes = lanes;
            this.ownership = ownership;
            StarSystems = starSystems.AsReadOnly();
            Lanes = laneEntries.AsReadOnly();
            Refresh();
        }
        public uint Revision { get; private set; }
        public IReadOnlyList<StarSystemMapEntry> StarSystems { get; }
        public IReadOnlyList<LaneMapEntry> Lanes { get; }

        public void Refresh()
        {
            var definition = galaxy.Galaxy;
            var state = galaxy.State;
            uint laneRevision = lanes?.Revision ?? 0;
            if (hasStamp && ReferenceEquals(definition, lastGalaxy) && galaxy.GenerationCount == lastGeneration &&
                state.Revision == lastStateRevision && laneRevision == lastLaneRevision && state.CurrentStarSystem.Equals(lastCurrent)) return;

            bool changed = false;
            int systemCount = 0, laneCount = 0;
            if (definition != null)
            {
                if (visibility.Length < definition.StarSystems.Length) visibility = new byte[definition.StarSystems.Length];
                int knownIndex = 0, visitedIndex = 0;
                // All three ID lists are sorted. Merge once rather than searching state per system/lane.
                for (int i = 0; i < definition.StarSystems.Length; i++)
                {
                    var system = definition.StarSystems[i];
                    uint id = system.Id.Value;
                    while (knownIndex < state.Known.Count && state.Known[knownIndex] < id) knownIndex++;
                    while (visitedIndex < state.Visited.Count && state.Visited[visitedIndex] < id) visitedIndex++;
                    bool known = knownIndex < state.Known.Count && state.Known[knownIndex] == id;
                    bool visited = visitedIndex < state.Visited.Count && state.Visited[visitedIndex] == id;
                    visibility[i] = (byte)((known ? KnownFlag : 0) | (visited ? VisitedFlag : 0));
                    if (!known) continue;
                    var entry = new StarSystemMapEntry(system.Id, system.PositionLy, galaxy.GetStarSystemName(system.Id), true,
                        visited, system.Id.Equals(state.CurrentStarSystem), (system.Flags & StarSystemFlags.Story) != 0,
                        system.Id.Equals(state.RouteDestination), system.Danger, ownership?.GetOwnerColorToken(system.Id) ?? "");
                    if (systemCount == starSystems.Count) { starSystems.Add(entry); changed = true; }
                    else if (!Same(starSystems[systemCount], entry)) { starSystems[systemCount] = entry; changed = true; }
                    systemCount++;
                }
                for (int i = 0; i < definition.Lanes.Length; i++)
                {
                    var lane = definition.Lanes[i];
                    if (!definition.TryGetIndex(lane.A, out int a) || !definition.TryGetIndex(lane.B, out int b)) continue;
                    bool known = (visibility[a] & visibility[b] & KnownFlag) != 0;
                    bool visited = (visibility[a] & visibility[b] & VisitedFlag) != 0;
                    if (!(known && (lane.Flags & LaneFlags.HiddenUntilKnown) == 0) && !visited) continue;
                    bool open = lanes?.IsLaneOpen(i) ?? true;
                    var entry = new LaneMapEntry(i, lane.A, lane.B, open,
                        open ? "" : lanes.GetLockedMessage(i), lanes?.GetFuelCost(i) ?? 0);
                    if (laneCount == laneEntries.Count) { laneEntries.Add(entry); changed = true; }
                    else if (!Same(laneEntries[laneCount], entry)) { laneEntries[laneCount] = entry; changed = true; }
                    laneCount++;
                }
            }
            if (systemCount < starSystems.Count) { starSystems.RemoveRange(systemCount, starSystems.Count - systemCount); changed = true; }
            if (laneCount < laneEntries.Count) { laneEntries.RemoveRange(laneCount, laneEntries.Count - laneCount); changed = true; }
            if (changed) Revision++;
            lastGalaxy = definition;
            lastGeneration = galaxy.GenerationCount;
            lastStateRevision = state.Revision;
            lastLaneRevision = laneRevision;
            lastCurrent = state.CurrentStarSystem;
            hasStamp = true;
        }

        static bool Same(in StarSystemMapEntry a, in StarSystemMapEntry b) =>
            a.Id.Equals(b.Id) && a.PositionLy.Equals(b.PositionLy) && string.Equals(a.Name, b.Name, StringComparison.Ordinal) &&
            a.Known == b.Known && a.Visited == b.Visited && a.Current == b.Current && a.IsStory == b.IsStory &&
            a.IsRouteTarget == b.IsRouteTarget && a.Danger == b.Danger && string.Equals(a.OwnerColorToken, b.OwnerColorToken, StringComparison.Ordinal);

        static bool Same(in LaneMapEntry a, in LaneMapEntry b) =>
            a.LaneIndex == b.LaneIndex && a.A.Equals(b.A) && a.B.Equals(b.B) && a.Open == b.Open && a.FuelCost == b.FuelCost &&
            string.Equals(a.LockedMessage, b.LockedMessage, StringComparison.Ordinal);
    }
}
