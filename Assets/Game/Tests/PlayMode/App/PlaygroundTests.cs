using System.Collections;
using Game.App.DevRuntime;
using Game.App.Session;
using Game.Core;
using Game.Economy;
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
            var originalPlayer = Root;
            Assert.IsTrue(Services.TryGet<FiringRangeService>(out var range));
            var originalStation = range.Target;
            Assert.IsTrue(EM.Exists(originalStation));
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=cargo", Host.Context).Kind);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("key keys=F hold=0.02", Host.Context).Kind);
            yield return Until(() => Host.Commands.Execute("inv", Host.Context).Text.Contains("credits 1000"), "F activates real cargo/wallet pad", 5);
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground goto=combat", Host.Context).Kind);
            yield return RunCommand("playground activate");
            Assert.AreEqual(DevResultKind.Ok, Host.Commands.Execute("playground activate", Host.Context).Kind, "Repeated combat activation cannot spawn another fighter");
            yield return RunCommand("playground reset");
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
