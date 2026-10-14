using System.Collections;
using Game.App.DevRuntime;
using Game.App.Session;
using Game.Core;
using Game.Economy;
using Game.Simulation;
using Game.Simulation.Ai;
using Unity.Entities;
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
            Assert.IsFalse(EM.Exists(originalPlayer), "Reset disposes previous player");
            Assert.IsFalse(EM.Exists(originalStation), "Reset disposes previous station");
            Assert.IsTrue(EM.Exists(range.Target));
            StringAssert.Contains("credits 0", Host.Commands.Execute("inv", Host.Context).Text);
            StringAssert.Contains("Playground active", Host.Commands.Execute("playground status", Host.Context).Text);
            yield return Run(Game.Session.QuitToMainMenuAsync(false));
            yield return null;
            StringAssert.Contains("Playground inactive", Host.Commands.Execute("playground status", Host.Context).Text);
        }
    }
}
