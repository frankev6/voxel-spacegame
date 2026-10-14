using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Encounters;
using Game.Galaxy.Services;
using Game.Simulation.Ai;
using Game.Tests.Common;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Galaxy
{
    public sealed class EnctIntegrationTests : GameFlowFixture
    {
        [UnityTest] public IEnumerator SaveLoad_RetainsStoryInstanceAndBlocksCombat()
        {
            yield return NewGame(42);
            var director = Service<EncounterDirectorService>(); director.Enabled = false;
            var encounter = Service<ContentService>().Catalog.Get<EncounterDefinition>(new ContentId("encounter_raiders"));
            Assert.AreEqual(1, director.SpawnEncounter(encounter, EncounterAnchor.Player(), 1, EncounterSource.Narrative));
            var instances = new List<EncounterInstanceInfo>(); director.GetActiveInstances(instances);
            uint id = instances[0].Id;
            var flow = Service<IGameFlow>();
            using (HoldSimulation("ENCT capture"))
            {
                using var status = Em.CreateEntityQuery(typeof(PlayerCombatStatus));
                status.CompleteDependency();
                var previous = status.GetSingleton<PlayerCombatStatus>();
                status.SetSingleton(new PlayerCombatStatus { HostilesEngaging = 1 });
                var blocked = flow.SaveGameAsync("encounter", SaveReason.Manual).GetAwaiter();
                yield return WaitUntil(() => blocked.IsCompleted, 10, "Combat save refusal");
                Assert.AreEqual(FlowResult.Blocked, blocked.GetResult());
                status.SetSingleton(default(PlayerCombatStatus));
                var saved = flow.SaveGameAsync("encounter", SaveReason.Manual).GetAwaiter();
                yield return WaitUntil(() => saved.IsCompleted, 10, "Encounter save");
                Assert.AreEqual(FlowResult.Ok, saved.GetResult());
                status.SetSingleton(previous);
            }
            var loaded = flow.LoadGameAsync("encounter").GetAwaiter();
            yield return WaitUntil(() => loaded.IsCompleted, 10, "Encounter load");
            Assert.AreEqual(FlowResult.Ok, loaded.GetResult());
            yield return WaitUntil(() => PlayerReady && director.TryGetInstance(id, out _), 10, "Saved story encounter respawns");
            director.GetActiveInstances(instances);
            Assert.AreEqual(1, instances.Count);
            Assert.AreEqual(id, instances[0].Id);
            Assert.AreEqual(encounter, instances[0].Definition);
            Assert.IsTrue(instances[0].Persistent);
        }
    }
}
