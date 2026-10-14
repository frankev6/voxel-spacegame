using System;
using System.Collections.Generic;
using Game.App.Session;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Galaxy.Data;
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
        public static IReadOnlyList<string> UnwiredMembers { get; } = Array.AsReadOnly(new[] { nameof(GetRelation), nameof(AdjustRelation),
            nameof(CurrentStarSystemOwnerFactionIndex), nameof(SpawnEncounter), nameof(StartCombat),
            nameof(RequestLaneJump), nameof(OpenMerchant) });
        readonly ContentCatalog content;
        readonly GameSession session;
        readonly GalaxyService galaxy;
        readonly BlueprintLibrary blueprints;
        readonly IAudioService audio;
        readonly NotificationService notifications;
        readonly World world;
        readonly EntityQuery clock;
        readonly NarrativePlayerDamage hull;
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
        public int CurrentStarSystemOwnerFactionIndex { get { Missing(nameof(CurrentStarSystemOwnerFactionIndex), "ENC-005/009"); return 0; } }
        public int GetRelation(int factionIndex) { Missing(nameof(GetRelation), "ENC-004/009"); return 0; }
        public void AdjustRelation(int factionIndex, int delta) => Missing(nameof(AdjustRelation), "ENC-004/009");
        public void SpawnEncounter(int encounterIndex, string anchorTag, int count, in LogicScope scope)
            => Missing(nameof(SpawnEncounter), "ENC-007/009");
        public void StartCombat(int encounterIndex, in LogicScope scope) => Missing(nameof(StartCombat), "ENC-007/009");
        public void RequestLaneJump(int starSystemIndex) => Missing(nameof(RequestLaneJump), "TRV-006/011");
        public void OpenMerchant(int merchantIndex, in LogicScope scope, Action onClosed)
        {
            Missing(nameof(OpenMerchant), "ECO-008"); onClosed?.Invoke();
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
        public void OpenShipEditor() => _ = EnterEditor();
        async Awaitable EnterEditor()
        {
            try
            {
                var flagship = session.Fleet.Flagship;
                if (flagship == null) { GameLog.ErrorOnce("Narrative.Editor.Flagship", "Narrative", "OpenShipEditor requires a flagship."); return; }
                var result = await session.EnterBlueprintLabAsync(new BlueprintLabArgs(BlueprintLabMode.FleetShip, flagship.PersistentId, ContentId.None));
                if (result != FlowResult.Ok) GameLog.WarnOnce("Narrative.Editor.Rejected", "Narrative", "OpenShipEditor was rejected: " + result);
            }
            catch (Exception error) { GameLog.Exception("Narrative", error, "OpenShipEditor"); }
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
