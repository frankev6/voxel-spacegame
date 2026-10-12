using System.Collections;
using Game.Core.Components;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    /// <summary>DEV-008: no-player sandbox completes without pretending player.ready is true.</summary>
    public sealed class SandboxCommandTests : SessionCommandFixture
    {
        [UnityTest] public IEnumerator NoPlayer_GameplayActive()
        {
            yield return RunCommand("sandbox player=false seed=17");
            using var query = EM.CreateEntityQuery(typeof(GameplayActive));
            Assert.AreEqual(1, query.CalculateEntityCount());
            Assert.AreEqual("false", Host.Commands.Execute("until cond=player.exists", Host.Context).Text);
            Assert.AreEqual("false", Host.Commands.Execute("until cond=player.ready", Host.Context).Text);
            Assert.IsTrue(Game.Session.IsSandbox);
            Assert.AreEqual(17ul, Game.Session.CampaignSeed);
            yield return Run(Game.Session.QuitToMainMenuAsync(false));
        }
    }
}
