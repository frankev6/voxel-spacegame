using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Nodes;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Narrative;

namespace Game.App.Services
{
    /// <summary>SPEC-40 §5.6: ordered world notifications to facts and beat triggers.</summary>
    public sealed class NarrativeWorldEventBridge : IDisposable
    {
        readonly NarrativeService narrative;
        readonly FactCatalog catalog;
        readonly AddFactEffect[] counters;
        readonly HashSet<ulong> warned = new();
        readonly Func<uint, ulong, LogicBodyHandle?> resolveBody;
        readonly Func<uint, ContentHash> resolveAnchor;
        readonly Func<ulong, ContentHash?> resolveEncounter;
        readonly Func<bool> travelIdle;
        readonly Func<ContentHash, int> factionIndex;
        readonly Func<LogicBodyHandle, bool, bool> interact;
        readonly Action<ContentHash, ulong, bool> hail;
        readonly Action<uint, bool> enter;
        readonly int systemsEntered, territoriesEntered;
        bool disposed;

        // Composition supplies canonical owner lookups; no service locator or alternate world state.
        public NarrativeWorldEventBridge(NarrativeService narrative,
            Func<uint, ulong, LogicBodyHandle?> resolveBody, Func<uint, ContentHash> resolveAnchor,
            Func<ulong, ContentHash?> resolveEncounter, Func<bool> travelIdle,
            Func<ContentHash, int> factionIndex, Func<LogicBodyHandle, bool, bool> interact,
            Action<ContentHash, ulong, bool> hail, Action<uint, bool> enter)
        {
            this.narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
            this.resolveBody = resolveBody ?? throw new ArgumentNullException(nameof(resolveBody));
            this.resolveAnchor = resolveAnchor ?? throw new ArgumentNullException(nameof(resolveAnchor));
            this.resolveEncounter = resolveEncounter ?? throw new ArgumentNullException(nameof(resolveEncounter));
            this.travelIdle = travelIdle ?? throw new ArgumentNullException(nameof(travelIdle));
            this.factionIndex = factionIndex ?? throw new ArgumentNullException(nameof(factionIndex));
            this.interact = interact ?? throw new ArgumentNullException(nameof(interact));
            this.hail = hail ?? throw new ArgumentNullException(nameof(hail));
            this.enter = enter ?? throw new ArgumentNullException(nameof(enter));
            catalog = narrative.Facts.Catalog;
            counters = new AddFactEffect[catalog.Count];
            systemsEntered = catalog.SlotOf("sys_star_systems_entered");
            territoriesEntered = catalog.SlotOf("sys_territories_entered");
            EventBus<BodyInteracted>.Subscribe(OnInteraction);
            EventBus<StarSystemEntered>.Subscribe(OnEntry);
            EventBus<LaneJumped>.Subscribe(OnJump);
            EventBus<BodyDestroyed>.Subscribe(OnBodyDestroyed);
            EventBus<ShipDestroyed>.Subscribe(OnShipDestroyed);
            EventBus<EncounterDefeated>.Subscribe(OnDefeated);
            EventBus<FactionRelationChanged>.Subscribe(OnRelation);
            EventBus<TerritoryEntered>.Subscribe(OnTerritory);
            EventBus<ItemAcquired>.Subscribe(OnAcquired);
        }

        /// <summary>QST-008 composition entry: canonical lookups plus implemented beat/travel/encounter owners.</summary>
        public static NarrativeWorldEventBridge CreateForWorld(NarrativeService narrative,
            ContentCatalog content, GalaxyService galaxy, Func<ulong, ContentHash?> encounterOfMember,
            Func<bool> travelIdle, Func<LogicBodyHandle, bool, bool> interact,
            Action<ContentHash, ulong, bool> hail, Action<uint, bool> enter)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (galaxy == null) throw new ArgumentNullException(nameof(galaxy));
            var anchors = new Dictionary<uint, ContentHash>();
            foreach (var definition in content.All<AuthoredStarSystemDefinition>())
                anchors.Add(StarSystemId.FromAuthored(definition.Id.Hash).Value, definition.Id.Hash);
            LogicBodyHandle? ResolveBody(uint system, ulong key)
            {
                var id = new StarSystemId(system);
                if (galaxy.Galaxy == null || !galaxy.Galaxy.TryGetIndex(id, out _)) return null;
                var bodies = galaxy.GetContent(id);
                if (!bodies.TryFind(key, out int index)) return null;
                var body = bodies.Bodies[index];
                var template = galaxy.GetBodyTemplate(body.TemplateIndex);
                return new LogicBodyHandle(system, (byte)body.Id.Role, body.Id.Slot, body.Id.SubSlot, template.Id.Hash);
            }
            ContentHash ResolveAnchor(uint system) => anchors.TryGetValue(system, out var hash) ? hash : default;
            int ResolveFaction(ContentHash hash) => content.TryGet<FactionDefinition>(hash, out var faction) ? content.IndexOf(faction) : 0;
            return new NarrativeWorldEventBridge(narrative, ResolveBody, ResolveAnchor,
                encounterOfMember, travelIdle, ResolveFaction, interact, hail, enter);
        }

        void Add(int slot, int amount)
        {
            if (slot < 0) return;
            var effect = counters[slot] ??= new AddFactEffect(slot, allowReserved: true);
            effect.Apply(narrative.Context, amount, FactWriteSource.WorldEvent);
        }
        void Add(FactFamily family, ContentHash hash, int amount)
        {
            if (hash.IsNone) return;
            int slot = catalog.GeneratedSlot(family, hash.Value);
            if (slot < 0) { Warn(hash.Value); return; }
            Add(slot, amount);
        }
        void Warn(ulong hash)
        {
            if (warned.Add(hash))
                GameLog.WarnOnce("NAR.WorldEvent.Unknown." + hash, "Narrative", "World event references unknown narrative ID " + hash + "; ignored.");
        }
        void Location() => narrative.NotifyDependency(new LogicDependency(LogicDependencyKind.Location, 0));
        void OnInteraction(BodyInteracted value)
        {
            if (disposed) return;
            var kind = (InteractionKind)value.Kind;
            if (kind == InteractionKind.Salvage) return;
            if (kind == InteractionKind.LaneGate && !travelIdle()) return;
            if (kind == InteractionKind.Hail)
            {
                var encounter = resolveEncounter(value.GridPersistentId);
                if (encounter.HasValue) hail(encounter.Value, value.GridPersistentId, narrative.PlayerCanTalk);
                return;
            }
            if (value.BodyKey == 0) return;
            var body = resolveBody(value.StarSystemValue, value.BodyKey);
            if (!body.HasValue) return;
            Add(FactFamily.Interacted, body.Value.Template, 1);
            bool handled = interact(body.Value, narrative.PlayerCanTalk);
            if (!handled && kind == InteractionKind.Inspect)
                EventBus<InteractionUnhandled>.Publish(new InteractionUnhandled(body.Value));
        }
        void OnEntry(StarSystemEntered value)
        {
            if (disposed) return;
            Add(systemsEntered, 1);
            Add(FactFamily.Visited, resolveAnchor(value.StarSystemValue), 1);
            Location();
            enter(value.StarSystemValue, value.VisitCount == 1);
        }
        void OnJump(LaneJumped value) { if (!disposed) Location(); }
        void OnBodyDestroyed(BodyDestroyed value)
        {
            if (disposed) return;
            var body = resolveBody(value.StarSystemValue, value.BodyKey);
            if (body.HasValue) Add(FactFamily.Destroyed, body.Value.Template, 1);
        }
        void OnShipDestroyed(ShipDestroyed value)
        {
            if (disposed || value.IsPlayer == 1) return;
            Add(FactFamily.Destroyed, value.Blueprint, 1);
            Add(FactFamily.Destroyed, value.Encounter, 1);
        }
        void OnDefeated(EncounterDefeated value)
        {
            // ENC-009 uses the generated defeated_<encounter> slot and the shared WorldEvent writer.
            if (!disposed) Add(FactFamily.Defeated, value.Encounter, 1);
        }
        void OnRelation(FactionRelationChanged value)
        {
            if (disposed) return;
            // ENC-009 dependencies use the content catalogue index, never the ECS faction index.
            int index = factionIndex(value.Faction);
            if (index <= 0) { Warn(value.Faction.Value); return; }
            narrative.NotifyDependency(new LogicDependency(LogicDependencyKind.Relation, index));
        }
        void OnTerritory(TerritoryEntered value)
        { if (!disposed) { Add(territoriesEntered, 1); Location(); } }
        void OnAcquired(ItemAcquired value)
        { if (!disposed) Add(FactFamily.Acquired, value.Item, value.Count); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EventBus<BodyInteracted>.Unsubscribe(OnInteraction);
            EventBus<StarSystemEntered>.Unsubscribe(OnEntry);
            EventBus<LaneJumped>.Unsubscribe(OnJump);
            EventBus<BodyDestroyed>.Unsubscribe(OnBodyDestroyed);
            EventBus<ShipDestroyed>.Unsubscribe(OnShipDestroyed);
            EventBus<EncounterDefeated>.Unsubscribe(OnDefeated);
            EventBus<FactionRelationChanged>.Unsubscribe(OnRelation);
            EventBus<TerritoryEntered>.Unsubscribe(OnTerritory);
            EventBus<ItemAcquired>.Unsubscribe(OnAcquired);
        }
    }
}
