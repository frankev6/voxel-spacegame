using System;
using System.Collections.Generic;
using Game.App.Session;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;
using Game.UI.Data;
using Game.UI.Services;
using Game.Voxel.Grid;
using Unity.Entities;
using UnityEngine;

namespace Game.App.Services
{
    /// <summary>SPEC-40 §5.5: narrative effects call their state owners, never write simulation state.</summary>
    public sealed class LogicWorldAdapter : ILogicWorld, IDisposable
    {
        // Updated by the corresponding owner wiring tasks. Callers cannot mutate this set.
        [field: StaticStateOk("Immutable read-only view of task-owned missing routes; never changed at runtime") ]
        public static IReadOnlyList<string> UnwiredMembers { get; } = Array.AsReadOnly(Array.Empty<string>());
        readonly ContentCatalog content;
        readonly GameSession session;
        readonly GalaxyService galaxy;
        readonly BlueprintLibrary blueprints;
        readonly IAudioService audio;
        readonly NotificationService notifications;
        readonly World world;
        readonly EntityQuery clock;
        readonly NarrativePlayerDamage hull;
        readonly FactionService factions;
        readonly TerritoryService territory;
        readonly StarSystemLoader loader;
        readonly EncounterDirectorService encounters;
        public LogicWorldAdapter(ContentCatalog content, GameSession session, GalaxyService galaxy,
            BlueprintLibrary blueprints, IAudioService audio, NotificationService notifications,
            World world, NarrativeConfigDefinition config)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.blueprints = blueprints ?? throw new ArgumentNullException(nameof(blueprints));
            this.audio = audio; this.notifications = notifications;
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            // These canonical owners are registered before narrative boot (ENC-007/009).
            // Keep the NAR-011 constructor contract; smaller contexts may omit world owners.
            Game.Core.Services.TryGet(out factions);
            Game.Core.Services.TryGet(out territory);
            Game.Core.Services.TryGet(out loader);
            Game.Core.Services.TryGet(out encounters);
            hull = new NarrativePlayerDamage(world, config);
            clock = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
        }
        public int CurrentStarSystemIndex
        {
            get
            {
                var generated = galaxy.Galaxy;
                if (generated == null || !generated.TryGetIndex(galaxy.State.CurrentStarSystem, out int index)) return 0;
                int authored = generated.StarSystems[index].AuthoredIndex;
                return authored == 0 ? 0 : content.IndexOf(generated.Authored[authored - 1]);
            }
        }
        public double UniversalTime => world.IsCreated && !clock.IsEmptyIgnoreFilter ? clock.GetSingleton<SimClock>().UniversalTime : 0;
        FactionDefinition CurrentOwner => territory != null && loader != null ? territory.GetOwner(loader.Loaded) : null;
        public int CurrentStarSystemOwnerFactionIndex => content.IndexOf(CurrentOwner);
        public int GetRelation(int factionIndex)
            => Require(factions, nameof(GetRelation)) ? factions.GetRelation(Faction(factionIndex)) : 0;
        public void AdjustRelation(int factionIndex, int delta)
        {
            if (Require(factions, nameof(AdjustRelation))) factions.AdjustRelation(Faction(factionIndex), delta);
        }
        FactionDefinition Faction(int index) => index == -1 ? CurrentOwner : Definition<FactionDefinition>(index);
        public void SpawnEncounter(int encounterIndex, string anchorTag, int count, in LogicScope scope)
        {
            if (Require(encounters, nameof(SpawnEncounter)))
                encounters.SpawnEncounter(Definition<EncounterDefinition>(encounterIndex), Anchor(anchorTag, scope), count, EncounterSource.Narrative);
        }
        public void StartCombat(int encounterIndex, in LogicScope scope)
        {
            if (Require(encounters, nameof(StartCombat)))
                encounters.StartCombat(Definition<EncounterDefinition>(encounterIndex), Anchor("here", scope));
        }
        static EncounterAnchor Anchor(string tag, in LogicScope scope)
        {
            if (tag == "here") return scope.HasBody ? new EncounterAnchor { Kind = EncounterAnchorKind.Body, Body = Body(scope.Body) } : EncounterAnchor.Player();
            return tag == "player" ? EncounterAnchor.Player() : new EncounterAnchor { Kind = EncounterAnchorKind.Tag, Tag = tag };
        }
        static bool Require<T>(T owner, string member) where T : class
        {
            if (owner != null) return true;
            GameLog.ErrorOnce("Narrative.Unavailable." + member, "Narrative", member + " requires " + typeof(T).Name + ".");
            return false;
        }
        public void RequestLaneJump(int starSystemIndex)
        {
            var definition = Definition<AuthoredStarSystemDefinition>(starSystemIndex);
            if (definition == null)
            { GameLog.ErrorOnce("Narrative.RequestLaneJump.Definition", "Narrative", "request_lane_jump requires an authored star system."); return; }
            if (!galaxy.TryResolveStarSystem(definition.Id, out var target))
            { GameLog.ErrorOnce("Narrative.RequestLaneJump.System", "Narrative", "request_lane_jump target is absent from the generated galaxy."); return; }
            if (!Game.Core.Services.TryGet<TravelService>(out var travel))
            { GameLog.ErrorOnce("Narrative.RequestLaneJump.Travel", "Narrative", "request_lane_jump requires TravelService."); return; }
            var result = travel.RequestJump(target, JumpSource.Narrative);
            if (result.Accepted) return;
            GameLog.Warn("Narrative", "request_lane_jump refused: " + result.Reason);
            EventBus<TravelNotice>.Publish(new TravelNotice(TravelNoticeKind.JumpRefused, target.Value, result.Reason));
        }
        public void OpenMerchant(int merchantIndex, in LogicScope scope, Action onClosed)
        {
            var merchant = Definition<MerchantDefinition>(merchantIndex);
            if (merchant == null)
            {
                GameLog.ErrorOnce("Narrative.OpenMerchant.Definition", "Narrative", "open_merchant requires a known merchant.");
                onClosed?.Invoke(); return;
            }
            if (!Game.Core.Services.TryGet<Game.App.Economy.MerchantLauncher>(out var launcher))
            {
                Require(launcher, nameof(OpenMerchant)); onClosed?.Invoke(); return;
            }
            launcher.Open(merchant, onClosed);
        }
        public void GiveShip(int blueprintIndex)
        {
            var definition = Definition<ShipBlueprintDefinition>(blueprintIndex);
            if (definition == null || !blueprints.TryGet(definition.Id.Hash, out var source))
            { GameLog.ErrorOnce("Narrative.GiveShip.Blueprint", "Narrative", "GiveShip requires a decoded blueprint."); return; }
            string name = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.IdString : definition.DisplayName;
            var record = new FleetShipRecord { PersistentId = session.Allocate(), SourceBlueprint = definition.Id,
                Name = name.Length > 32 ? name.Substring(0, 32) : name, Structure = source.Clone() };
            try { session.Fleet.Add(record, false); }
            catch { record.Dispose(); throw; }
        }
        public void ReplaceBody(in LogicScope scope, int bodyTemplateIndex)
        {
            var definition = Definition<BodyTemplateDefinition>(bodyTemplateIndex);
            if (!scope.HasBody || definition == null)
            { GameLog.ErrorOnce("Narrative.ReplaceBody.Target", "Narrative", "ReplaceBody requires a body scope and template."); return; }
            galaxy.ReplaceBody(Body(scope.Body), definition.Id);
        }
        public void RemoveBody(in LogicScope scope)
        {
            if (!scope.HasBody) { GameLog.ErrorOnce("Narrative.RemoveBody.Target", "Narrative", "RemoveBody requires a body scope."); return; }
            galaxy.RemoveBody(Body(scope.Body));
        }
        public void OpenShipEditor()
        {
            if (!Game.Core.Services.TryGet<Game.App.EditorHost.ShipEditorLauncher>(out var launcher))
            { GameLog.WarnOnce("Narrative.Editor.Unavailable", "Narrative", "Ship editor launcher is unavailable."); return; }
            if (!launcher.EditPlayerShip(out var reason)) GameLog.Warn("Narrative", reason);
        }
        public void RepairPlayerShip(int percent) => hull.Repair(percent);
        public void DamagePlayerShip(int amount, in LogicScope scope) => hull.Damage(amount, session.CampaignSeed, UniversalTime);
        public void PlaySfx(int audioCueIndex)
        {
            var definition = Definition<AudioCueDefinition>(audioCueIndex);
            if (definition != null) audio?.PlayCue(definition.Id);
        }
        public void Notify(string text)
        {
            if (notifications == null) GameLog.Info("Narrative", text);
            else notifications.Post(new Notification { Kind = NotificationKind.Info, Label = text });
        }
        T Definition<T>(int index) where T : ContentDefinition
            => index > 0 && index <= ushort.MaxValue ? content.GetByIndex<T>((ushort)index) : null;
        static BodyId Body(in LogicBodyHandle body) => new(new StarSystemId(body.StarSystem), (BodyRole)body.Role, body.Slot, body.SubSlot);
        static void Missing(string member, string owner) => GameLog.ErrorOnce("Narrative.Unwired." + member, "Narrative", member + " not wired: " + owner);
        public void Dispose() { hull.Dispose(); if (world.IsCreated) clock.Dispose(); }
    }
}
