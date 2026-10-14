using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Presentation.Components;
using Game.Simulation.Factions;

namespace Game.Presentation.Presenters
{
    /// <summary>Strings are cached by their real owner keys; campaign replacement invalidates geographic caches.</summary>
    public sealed class HudNames : IHudNames
    {
        readonly ContentCatalog content;
        readonly FactionIndexMap factions;
        readonly GalaxyService galaxy;
        readonly TravelService travel;
        readonly EncounterDirectorService encounters;
        readonly Dictionary<(ulong, byte), string> targets = new();
        readonly Dictionary<uint, string> systems = new();
        readonly Dictionary<BodyId, string> bodies = new();
        readonly Dictionary<int, string> fuel = new();
        GalaxyDef generation;
        RouteStatus lastStatus;
        uint lastDestination, lastRevision;
        int lastCount = -1, lastFuel;
        string lastBlocked;
        HudRouteText route;
        ulong gateKey;
        uint gateRevision, gateStateRevision;
        int gateFuel;
        bool gateCached;
        HudInteractionText gateText;
        public HudNames(ContentCatalog content, FactionIndexMap factions, GalaxyService galaxy,
            TravelService travel, EncounterDirectorService encounters = null)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.factions = factions ?? throw new ArgumentNullException(nameof(factions));
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.travel = travel ?? throw new ArgumentNullException(nameof(travel));
            this.encounters = encounters;
        }
        void CheckGeneration()
        {
            if (ReferenceEquals(generation, galaxy.Galaxy)) return;
            generation = galaxy.Galaxy; systems.Clear(); bodies.Clear(); lastCount = -1; gateCached = false;
        }
        public string TargetName(ContentHash blueprint, byte faction)
        {
            var key = (blueprint.Value, faction);
            if (targets.TryGetValue(key, out var name)) return name;
            name = content.TryGet<ShipBlueprintDefinition>(blueprint, out var definition) ? definition.DisplayName : "Unknown ship";
            var prefix = factions.ByIndex(faction)?.ShipNamePrefix;
            if (!string.IsNullOrEmpty(prefix)) name = prefix + " " + name;
            targets.Add(key, name); return name;
        }
        public string SystemName(uint system)
        {
            CheckGeneration();
            if (system == 0 || generation == null || !generation.IndexById.ContainsKey(system)) return "";
            if (!systems.TryGetValue(system, out var name)) systems.Add(system, name = galaxy.GetStarSystemName(new StarSystemId(system)));
            return name;
        }
        public AbilityDefinition Ability(ContentHash ability) => content.TryGet<AbilityDefinition>(ability, out var value) ? value : null;
        public HudInteractionText Interaction(in HudSnapshot value)
        {
            CheckGeneration();
            if ((value.Flags & HudFlags.HasInteraction) == 0) return new HudInteractionText("", "", false);
            if ((InteractionKind)value.InteractionKind == InteractionKind.LaneGate)
            {
                uint revision = travel.Revision, stateRevision = galaxy.State.Revision;
                int item = content.IndexOf(travel.Config.FuelItem), held = -1;
                if (item != 0 && Game.Core.Services.TryGet<Game.Data.Logic.ILogicContext>(out var context))
                    held = context.Economy?.GetItemCount(item) ?? -1;
                if (gateCached && gateKey == value.InteractionBodyKey && gateRevision == revision && gateStateRevision == stateRevision && gateFuel == held) return gateText;
                gateKey = value.InteractionBodyKey; gateRevision = revision; gateStateRevision = stateRevision; gateFuel = held; gateCached = true;
                gateText = new HudInteractionText("", "Gate unavailable", true);
                if (travel.TryGetGateInfo(gateKey, out var gate))
                {
                    if (!fuel.TryGetValue(gate.FuelCost, out var detail)) fuel.Add(gate.FuelCost, detail = "Fuel " + gate.FuelCost);
                    gateText = new HudInteractionText(gate.TargetName, gate.Open ? detail : gate.LockedMessage, !gate.Open || !gate.CanAfford);
                }
                return gateText;
            }
            if ((InteractionKind)value.InteractionKind == InteractionKind.Hail)
            {
                // TODO(ENC-007): composition supplies the real director when its runtime implementation exists.
                return new HudInteractionText(encounters != null && encounters.TryGetInstanceByMember(value.InteractionGridPersistentId, out var info)
                    ? info.Definition.DisplayName : "Unknown contact", "", false);
            }
            if (generation == null || value.StarSystemIdValue == 0 || value.InteractionBodyKey == 0) return new HudInteractionText("", "", false);
            var body = BodyId.FromLocalKey(new StarSystemId(value.StarSystemIdValue), value.InteractionBodyKey);
            if (!bodies.TryGetValue(body, out var name))
            {
                if (!generation.IndexById.ContainsKey(body.System.Value)) return new HudInteractionText("", "", false);
                var system = galaxy.GetContent(body.System);
                if (!system.TryFind(body.LocalKey, out _)) return new HudInteractionText("", "", false);
                bodies.Add(body, name = galaxy.GetBodyName(body));
            }
            return new HudInteractionText(name, "", false);
        }
        public HudRouteText Route
        {
            get
            {
                CheckGeneration();
                var status = travel.RouteStatus; uint destination = travel.Destination.Value, revision = travel.Revision;
                int count = travel.Route.Count, cost = travel.RouteFuelCost; var blocked = travel.RouteBlockedReason;
                if (lastCount != count || lastFuel != cost || lastStatus != status || lastDestination != destination || lastRevision != revision || lastBlocked != blocked)
                {
                    lastCount = count; lastFuel = cost; lastStatus = status; lastDestination = destination; lastRevision = revision; lastBlocked = blocked;
                    route = new HudRouteText(status != RouteStatus.None, status == RouteStatus.Blocked || status == RouteStatus.Unreachable,
                        status == RouteStatus.Ok ? "Route: " + count + " jumps, " + cost + " fuel" : blocked ?? "");
                }
                return route;
            }
        }
        public bool TryGetNextHop(out NextHop hop) => travel.TryGetNextHop(out hop);
    }
}
