#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Game.App.Presenters;
using Game.App.Session;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Economy;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Narrative.Beats;
using Game.Narrative.Clues;
using Game.Narrative.Quests;
using Game.Narrative.ShipLog;
using Game.UI.Services;
using Game.UI.Views;

namespace Game.App.DevRuntime.Playground
{
    /// <summary>Explicit sandbox actions using the registered story/map owners and authored content.</summary>
    internal static class DevPlaygroundStory
    {
        internal static DevCommandResult Activate(DevContext ctx, UIRoot ui, string action)
        {
            if (!ctx.TryGetService<GameSession>(out var session) || !session.IsSandbox
                || !ctx.TryGetService<ContentService>(out var content) || !ctx.TryGetService<QuestService>(out var quests)
                || !ctx.TryGetService<ClueService>(out var clues) || !ctx.TryGetService<BeatService>(out var beats)
                || !ctx.TryGetService<ShipLogQuery>(out _) || !ctx.TryGetService<GalaxyService>(out var galaxy)
                || !ctx.TryGetService<EconomyService>(out var economy) || !ctx.TryGetService<UiComposition>(out var screens))
                return DevCommandResult.Fail("Story playground requires the isolated sandbox and registered story/map services.");
            var catalog = content.Catalog;
            if (!catalog.TryGet<QuestDefinition>(new ContentId("quest_survey_local_materials"), out var quest)
                || !catalog.TryGet<ClueDefinition>(new ContentId("clue_fragmented_warning"), out var clue)
                || !catalog.TryGet<RumorDefinition>(new ContentId("rumor_hidden_research_station"), out var rumor)
                || !catalog.TryGet<ItemDefinition>(new ContentId("resource_palladium"), out var material) || rumor.Target == null)
                return DevCommandResult.Fail("Authored survey, clue, rumor or material content is unavailable.");
            // The sandbox has no generated galaxy until an explicit map/story trial asks for one.
            // NewGame initializes chart metadata (including its current=start system); the sandbox has no
            // campaign PlayerLocation. Do not load bodies, publish a system entry or move the player.
            if (galaxy.Galaxy == null) galaxy.NewGame(session.CampaignSeed);
            var target = StarSystemId.FromAuthored(rumor.Target.Id.Hash);
            if (!galaxy.Galaxy.TryGetIndex(target, out _)) return DevCommandResult.Fail("The authored rumor target is absent from this galaxy.");
            int questIndex = catalog.IndexOf(quest);
            if (quests.GetStatus(questIndex) == QuestStatus.NotStarted && !quests.Start(questIndex))
                return DevCommandResult.Fail("The survey quest could not start.");
            clues.Reveal(catalog.IndexOf<ClueDefinition>(clue)); clues.Reveal(catalog.IndexOf<ClueDefinition>(rumor));
            if (action == "survey" && economy.Inventory.CountOf(material.Id) < 3
                && !economy.Inventory.TryAdd(material.Id, 1, out string reason)) return DevCommandResult.Fail(reason);

            // Campaign evaluation stays disabled. One explicit dev action evaluates the actual quest/clue
            // owners and flushes the normal log/marker hook; false prevents automatic beat dispatch.
            quests.Evaluate(Array.Empty<LogicDependency>(), true); clues.Evaluate(Array.Empty<LogicDependency>(), true);
            beats.AfterEvaluation(false);
            if (action == "map") return screens.ShowGalaxyMapSelected(target)
                ? DevCommandResult.Ok("Galaxy map opened at “" + rumor.Target.DisplayName + "”. Inspect the rumor marker and select systems; no travel is started.")
                : DevCommandResult.Fail("The actual galaxy map could not display the rumor target.");
            screens.ShipLogModel.SelectedTab = 0; screens.ShipLogModel.ShowCompleted = false;
            ui.Navigator.Push<ShipLogView>();
            return DevCommandResult.Ok("Survey log opened: " + economy.Inventory.CountOf(material.Id) + "/3 palladium. Visit SURVEY SUPPLIES to collect one unit, then inspect the quest, clue and Rumors → Show on map. Automatic campaign dialogue remains off.");
        }
        internal static void RefreshResetMarkers(DevContext ctx)
        {
            // Sandbox reset already cleared the canonical facts/quests/clues; publish their empty marker projection.
            if (ctx.TryGetService<BeatService>(out var beats)) beats.AfterEvaluation(false);
        }
    }
}
#endif
