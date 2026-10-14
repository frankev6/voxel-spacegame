using System.Collections;
using System.Collections.Generic;
using Game.App.Presenters;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Narrative.Clues;
using Game.Narrative.Quests;
using Game.Narrative.ShipLog;
using Game.Tests.PlayMode.UI;
using Game.UI.Data;
using Game.App.DevRuntime;
using Game.App.Session;
using Game.Core;
using Game.Economy;
using Game.Simulation;
using Game.Simulation.Ai;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Game.UI.Services;
using Game.UI.Views;
using UnityEngine.UIElements;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    /// <summary>One integrated owner-feature case: quiet entry, real interaction, spawn/reset and model cleanup.</summary>
    public sealed class PlaygroundTests : SessionCommandFixture
    {
        [UnityTest] public IEnumerator Enter_Interact_Reset()
        {
            yield return RunCommand("playground");
            Assert.IsTrue(Game.Session.IsSandbox);
            Assert.IsFalse(Services.Get<Game.Narrative.Dialog.DialogService>().IsActive, "Sandbox entry must not launch the campaign intro");
            StringAssert.Contains("Playground active", Host.Commands.Execute("playground status", Host.Context).Text);
            var ui = Services.Get<UIRoot>();
            Assert.NotNull(ui.RootElement.Q<Button>("btn-open-console"), "Console access cannot require F1");
            Assert.NotNull(ui.RootElement.Q<Button>("btn-enter-playground"));
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground activate", Host.Context).Kind);
            StringAssert.Contains("gate 1 / 4", Host.Commands.Execute("playground status", Host.Context).Text);
            var originalPlayer = Root;
            Assert.IsTrue(Services.TryGet<FiringRangeService>(out var range));
            var originalStation = range.Target;
            Assert.IsTrue(EM.Exists(originalStation));
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=cargo", Host.Context).Kind);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("key keys=F hold=0.02", Host.Context).Kind);
            yield return Until(() => Host.Commands.Execute("inv", Host.Context).Text.Contains("credits 1000"), "F activates real cargo/wallet pad", 5);
            Assert.IsTrue(ui.Navigator.Get<InventoryView>().IsOpen, "Cargo must open the real inventory screen");
            ui.Navigator.Pop(ui.Navigator.Get<InventoryView>());
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=settings", Host.Context).Kind);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground activate", Host.Context).Kind);
            Assert.IsTrue(ui.Navigator.Get<SettingsView>().IsOpen);
            ui.Navigator.Pop(ui.Navigator.Get<SettingsView>());
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=merchant", Host.Context).Kind);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground activate", Host.Context).Kind);
            Assert.IsTrue(ui.Navigator.Get<MerchantView>().IsOpen, "Trade must open the real merchant screen");
            Assert.NotNull(Services.Get<EconomyService>().Merchants.Current);
            ui.Navigator.Pop(ui.Navigator.Get<MerchantView>());
            Assert.IsNull(Services.Get<EconomyService>().Merchants.Current);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=fleet", Host.Context).Kind);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground activate", Host.Context).Kind);
            Assert.IsTrue(ui.Navigator.Get<FleetView>().IsOpen, "Fleet trial must open the real fleet screen");
            Assert.Greater(Services.Get<Game.App.Session.GameSession>().Fleet.Ships.Count, 0);
            ui.Navigator.Pop(ui.Navigator.Get<FleetView>());
            // The explicit story area uses the registered owners; quiet entry above still has no campaign intro.
            var catalog = Services.Get<ContentService>().Catalog; var quests = Services.Get<QuestService>();
            var clues = Services.Get<ClueService>(); var query = Services.Get<ShipLogQuery>(); var screens = Services.Get<UiComposition>();
            var economy = Services.Get<EconomyService>(); var galaxy = Services.Get<GalaxyService>();
            var quest = catalog.Get<QuestDefinition>(new ContentId("quest_survey_local_materials")); int questIndex = catalog.IndexOf(quest);
            int clueIndex = catalog.IndexOf<ClueDefinition>(catalog.Get<ClueDefinition>(new ContentId("clue_fragmented_warning")));
            var rumor = catalog.Get<RumorDefinition>(new ContentId("rumor_hidden_research_station")); int rumorIndex = catalog.IndexOf<ClueDefinition>(rumor);
            var palladium = new ContentId("resource_palladium"); var objectives = new List<QuestObjectiveView>();
            Assert.AreEqual(QuestStatus.NotStarted, quests.GetStatus(questIndex)); Assert.IsFalse(clues.IsKnown(clueIndex)); Assert.IsFalse(clues.IsKnown(rumorIndex));
            Assert.AreEqual(0, query.Markers.Count, "A fresh quiet sandbox must not retain campaign markers.");
            var sceneBeforeStory = Game.Session.CurrentScene; var locationBeforeStory = galaxy.State.PlayerLocation;
            bool hasLocationBeforeStory = galaxy.State.HasPlayerLocation; ulong seedBeforeStory = Game.Session.CampaignSeed;
            int generationsBeforeStory = galaxy.GenerationCount;
            // Release the earlier cargo injection before another actual F press; screen changes can finish in one frame.
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("key keys=F release=true", Host.Context).Kind);
            yield return null;
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=story", Host.Context).Kind);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("key keys=F hold=0.02", Host.Context).Kind);
            yield return Until(() => ui.Navigator.IsOpen<ShipLogView>(), "F opens the canonical story log", 5);
            var log = ui.Navigator.Get<ShipLogView>(); Assert.AreSame(log, ui.Navigator.Top);
            Assert.AreEqual(QuestStatus.Active, quests.GetStatus(questIndex)); Assert.AreEqual(questIndex, quests.TrackedQuestIndex);
            Assert.AreEqual(1, quests.GetInstanceCount(questIndex)); Assert.IsTrue(clues.IsKnown(clueIndex)); Assert.IsTrue(clues.IsKnown(rumorIndex));
            int clueOrdinal = clues.OrdinalOf(clueIndex), rumorOrdinal = clues.OrdinalOf(rumorIndex);
            Assert.Greater(rumorOrdinal, clueOrdinal); Assert.AreEqual(quest.Title, screens.ShipLogModel.QuestTitle);
            quests.GetObjectives(questIndex, objectives); Assert.AreEqual(0, objectives[0].Progress); Assert.IsFalse(objectives[0].Done);
            bool objectiveMarker = false, rumorMarker = false;
            foreach (var marker in query.Markers)
            {
                objectiveMarker |= marker.Kind == NarrativeMarkerKind.Objective && marker.QuestIndex == questIndex;
                rumorMarker |= marker.Kind == NarrativeMarkerKind.Rumor && marker.ClueIndex == rumorIndex && marker.StarSystem == rumor.Target.Id;
            }
            Assert.IsTrue(objectiveMarker); Assert.IsTrue(rumorMarker); Assert.IsNotNull(galaxy.Galaxy);
            Assert.AreEqual(generationsBeforeStory + 1, galaxy.GenerationCount);
            Assert.AreEqual(sceneBeforeStory, Game.Session.CurrentScene); Assert.AreEqual(seedBeforeStory, Game.Session.CampaignSeed);
            Assert.AreEqual(hasLocationBeforeStory, galaxy.State.HasPlayerLocation); Assert.AreEqual(locationBeforeStory, galaxy.State.PlayerLocation);
            Assert.AreEqual(galaxy.Galaxy.StartStarSystem, galaxy.State.CurrentStarSystem, "NewGame initializes chart metadata at its start system; the sandbox has no campaign player location.");
            DialogTestContext.Activate(log.Root.Q<Button>("ship-log-tab-clues"));
            Assert.IsTrue(clues.IsSeen(clueIndex), "Selecting the real clue tab records the canonical seen state.");
            DialogTestContext.Activate(log.Root.Q<Button>("ship-log-close")); Assert.IsFalse(log.IsOpen);
            for (int collected = 1; collected <= 3; collected++)
            {
                Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=survey", Host.Context).Kind);
                var logic = Services.Get<ILogicContext>(); var callerScope = logic.Scope;
                Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground activate", Host.Context).Kind);
                Assert.AreEqual(callerScope, logic.Scope, "Quest and rumor owners restore the caller scope after evaluating the authored quest_completed condition.");
                Assert.AreSame(log, ui.Navigator.Top); Assert.AreEqual(collected, economy.Inventory.CountOf(palladium));
                quests.GetObjectives(questIndex, objectives); Assert.AreEqual(collected, objectives[0].Progress);
                Assert.AreEqual(collected == 3, objectives[0].Done); Assert.IsFalse(objectives[1].Done, "This supplies trial does not fake ARK hand-in.");
                Assert.IsTrue(screens.ShipLogModel.Objectives[0].Text.Contains(collected + "/3"));
                Assert.AreEqual(collected == 3, screens.ShipLogModel.Objectives[0].IsDone);
                DialogTestContext.Activate(log.Root.Q<Button>("ship-log-close"));
            }
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground activate", Host.Context).Kind);
            Assert.AreEqual(3, economy.Inventory.CountOf(palladium), "Completed collection cannot farm more survey grants.");
            Assert.AreEqual(1, quests.GetInstanceCount(questIndex)); Assert.AreEqual(clueOrdinal, clues.OrdinalOf(clueIndex)); Assert.AreEqual(rumorOrdinal, clues.OrdinalOf(rumorIndex));
            DialogTestContext.Activate(log.Root.Q<Button>("ship-log-tab-rumors"));
            yield return Until(() => log.Root.Q<Button>("ship-log-rumor-map-0") != null, "The actual rumor row is realized", 2);
            var loadedBeforeMap = Services.Get<StarSystemLoader>().Loaded;
            DialogTestContext.Activate(log.Root.Q<Button>("ship-log-rumor-map-0"));
            var map = ui.Navigator.Get<GalaxyMapView>(); Assert.AreSame(map, ui.Navigator.Top);
            Assert.IsTrue(galaxy.Galaxy.TryGetIndex(StarSystemId.FromAuthored(rumor.Target.Id.Hash), out int rumorSystem));
            Assert.GreaterOrEqual(screens.GalaxyMapModel.SelectedIndex, 0);
            var rumorPosition = galaxy.Galaxy.StarSystems[rumorSystem].PositionLy;
            Assert.AreEqual(rumorPosition, screens.GalaxyMapModel.Data.Points[screens.GalaxyMapModel.SelectedIndex].Pos,
                "The real rumor route selects the canonical target, even while its name is uncharted.");
            bool renderedRumor = false;
            foreach (var marker in screens.GalaxyMapModel.Data.Markers)
                renderedRumor |= marker.Kind == MapMarkerKind.Rumor && marker.Label == rumor.Title && marker.Pos.Equals(rumorPosition);
            Assert.IsTrue(renderedRumor, "The actual map data contains the revealed rumor marker.");
            DialogTestContext.Activate(map.Root.Q<Button>("galaxy-map-close")); Assert.AreSame(log, ui.Navigator.Top);
            DialogTestContext.Activate(log.Root.Q<Button>("ship-log-close"));
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=map", Host.Context).Kind);
            var poseBeforeMap = EM.GetComponentData<LocalTransform>(Root);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground activate", Host.Context).Kind);
            Assert.AreEqual(poseBeforeMap.Position, EM.GetComponentData<LocalTransform>(Root).Position);
            Assert.AreEqual(generationsBeforeStory + 1, galaxy.GenerationCount, "Repeated beacons reuse the generated metadata, including a valid seed-zero galaxy.");
            Assert.AreSame(map, ui.Navigator.Top); Assert.GreaterOrEqual(screens.GalaxyMapModel.SelectedIndex, 0);
            Assert.AreEqual(rumorPosition, screens.GalaxyMapModel.Data.Points[screens.GalaxyMapModel.SelectedIndex].Pos);
            DialogTestContext.Activate(map.Root.Q<Button>("galaxy-map-close"));
            Assert.AreEqual(loadedBeforeMap, Services.Get<StarSystemLoader>().Loaded, "Map inspection cannot load campaign bodies into the sandbox.");
            Assert.IsTrue(Game.Session.IsSandbox); Assert.IsTrue(EM.Exists(originalStation));
            Assert.AreEqual(sceneBeforeStory, Game.Session.CurrentScene); Assert.AreEqual(locationBeforeStory, galaxy.State.PlayerLocation);
            Assert.AreEqual(hasLocationBeforeStory, galaxy.State.HasPlayerLocation);
            Assert.IsFalse(Services.Get<Game.Narrative.Dialog.DialogService>().IsActive, "Explicit quest/clue/map actions must not launch automatic campaign dialogue.");
            // Console shortcuts must remove TextEntry before dispatching actual ability input.
            DialogTestContext.Activate(ui.RootElement.Q<Button>("btn-open-console"));
            Assert.AreEqual(InputContext.TextEntry, Services.Get<global::Game.Input.InputService>().CurrentContext);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground run=power", Host.Context).Kind);
            Assert.AreEqual(InputContext.Flight, Services.Get<global::Game.Input.InputService>().CurrentContext);
            Assert.NotNull(ui.RootElement.Q<VisualElement>("dev-power-trial"));
            EM.CompleteAllTrackedJobs();
            byte requested = EM.GetComponentData<global::Game.Simulation.Power.ShieldControl>(Root).RequestedOn;
            yield return RunCommand("playground run=shield");
            EM.CompleteAllTrackedJobs();
            Assert.AreNotEqual(requested, EM.GetComponentData<global::Game.Simulation.Power.ShieldControl>(Root).RequestedOn);
            yield return RunCommand("playground run=boost");
            EM.CompleteAllTrackedJobs();
            Assert.Greater(EM.GetComponentData<global::Game.Simulation.Power.ShipStatMultipliers>(Root).Get(global::Game.Data.Combat.StatId.Thrust), 1);
            int scrapBefore = economy.Inventory.CountOf(new ContentId("resource_scrap"));
            yield return RunCommand("playground run=salvage");
            Assert.AreEqual(scrapBefore, economy.Inventory.CountOf(new ContentId("resource_scrap")), "Wreck preparation grants no cargo.");
            yield return Until(() => Host.Context.TryGetInteractionTarget(out var salvageTarget)
                && salvageTarget.Kind == InteractionKind.Salvage && salvageTarget.InRange != 0,
                "Actual salvage prompt is available", 3);
            yield return RunCommand("playground run=salvage");
            Assert.Greater(economy.Inventory.CountOf(new ContentId("resource_scrap")), scrapBefore, "Production salvage transfers actual cargo.");
            Assert.IsTrue(ui.Navigator.Get<InventoryView>().IsOpen);
            ui.Navigator.Pop(ui.Navigator.Get<InventoryView>());
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=combat", Host.Context).Kind);
            yield return RunCommand("playground activate");
            EM.CompleteAllTrackedJobs();
            using (var fighters = EM.CreateEntityQuery(typeof(AiBrain), typeof(Allegiance)))
            using (var allegiances = fighters.ToComponentDataArray<Allegiance>(Allocator.Temp))
            using (var factions = EM.CreateEntityQuery(typeof(FactionRelations)))
            {
                bool hostile = false;
                var matrix = factions.GetSingleton<FactionRelations>();
                foreach (var allegiance in allegiances) hostile |= matrix.IsHostile(Allegiance.Player, allegiance);
                Assert.IsTrue(hostile, "Combat trial must spawn an actual hostile faction, not only a differently numbered team");
            }
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground activate", Host.Context).Kind, "Repeated combat activation cannot spawn another fighter");
            var standings = Services.Get<Game.Galaxy.Services.FactionService>();
            var neutral = standings.Indices.ByIndex(1);
            Assert.IsFalse(neutral.RelationLocked);
            standings.SetRelation(neutral, -70);
            Assert.AreEqual(-70, standings.GetRelation(neutral));
            yield return RunCommand("playground reset");
            Assert.AreEqual(neutral.BasePlayerRelation, standings.GetRelation(neutral), "Reset clears faction changes as well as the visible trial");
            Assert.IsFalse(Services.Get<Game.Narrative.Dialog.DialogService>().IsActive, "Sandbox reset must not replay the campaign intro");
            Assert.AreEqual(QuestStatus.NotStarted, quests.GetStatus(questIndex)); Assert.AreEqual(0, quests.GetInstanceCount(questIndex));
            Assert.IsFalse(clues.IsKnown(clueIndex)); Assert.IsFalse(clues.IsKnown(rumorIndex)); Assert.IsFalse(clues.IsSeen(clueIndex));
            Assert.AreEqual(0, economy.Inventory.CountOf(palladium)); Assert.AreEqual(0, query.Markers.Count);
            Assert.IsNull(galaxy.Galaxy, "Reset releases the explicitly generated map through the normal session owner.");
            Assert.IsFalse(EM.Exists(originalPlayer), "Reset disposes previous player");
            Assert.IsFalse(EM.Exists(originalStation), "Reset disposes previous station");
            Assert.IsTrue(EM.Exists(range.Target));
            StringAssert.Contains("credits 0", Host.Commands.Execute("inv", Host.Context).Text);
            StringAssert.Contains("Playground active", Host.Commands.Execute("playground status", Host.Context).Text);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=editor", Host.Context).Kind);
            yield return RunCommand("playground activate");
            Assert.AreEqual(global::Game.Core.Flow.GameScene.BlueprintLab, Game.Session.CurrentScene);
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<Game.App.EditorHost.ShipEditorHost>());
            Assert.AreEqual(global::Game.Core.InputContext.Editor, Services.Get<global::Game.Input.InputService>().CurrentContext);
            // The isolated Lab is part of the playground: reset must work directly without a manual Exit step.
            yield return RunCommand("playground reset");
            Assert.IsTrue(Game.Session.IsSandbox);
            StringAssert.Contains("Playground active", Host.Commands.Execute("playground status", Host.Context).Text);
            var trialProfile = Services.Get<global::Game.Persistence.Services.SaveService>().ActiveProfile;
            yield return RunCommand("playground run=campaign");
            Assert.IsFalse(Game.Session.IsSandbox);
            Assert.AreEqual(trialProfile, Services.Get<global::Game.Persistence.Services.SaveService>().ActiveProfile);
            Assert.IsTrue(Services.Get<Game.Narrative.Dialog.DialogService>().IsActive, "The real opening dialogue must be playable.");
            Assert.NotNull(ui.RootElement.Q<VisualElement>("dev-campaign-guide"));
            yield return RunCommand("playground reset");
            Assert.IsTrue(Game.Session.IsSandbox);
            Assert.IsFalse(Services.Get<Game.Narrative.Dialog.DialogService>().IsActive);
            Assert.IsNull(ui.RootElement.Q<VisualElement>("dev-campaign-guide"));
            yield return Run(Game.Session.QuitToMainMenuAsync(false));
            yield return null;
            StringAssert.Contains("Playground inactive", Host.Commands.Execute("playground status", Host.Context).Text);
        }
    }
}
