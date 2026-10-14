using System.Collections;
using Game.App.DevRuntime;
using Game.Core;
using Game.Galaxy.Components;
using Game.Galaxy.Services;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    /// <summary>DEV-018: real command, save file, PLOC restore and playable player root.</summary>
    public sealed class SaveLoadCommandTests : SessionCommandFixture
    {
        [UnityTest] public IEnumerator RoundTrip_Position()
        {
            yield return RunCommand("newgame seed=7");
            var profile = Game.Save.ActiveProfile;
            var galaxy = Services.Get<GalaxyService>();
            var system = galaxy.State.CurrentStarSystem;
            double3 saved;
            // Freeze capture/mutation only; load completion requires the real Flight context.
            using (Services.Get<IPauseService>().Acquire("DEV-018 round trip"))
            {
                EM.CompleteAllTrackedJobs();
                saved = EM.GetComponentData<UniversePosition>(Root).Value;
                yield return RunCommand("save slot=a");
                var moved = Host.Commands.Execute("teleport x=64 z=-32", Host.Context);
                Assert.AreEqual(DevResultKind.Ok, moved.Kind, moved.Text);
                Assert.Greater(math.distance(saved, EM.GetComponentData<UniversePosition>(Root).Value), 1,
                    "The live player must move before restoring the save.");
            }
            yield return RunCommand("load slot=a");
            EM.CompleteAllTrackedJobs();
            Assert.LessOrEqual(math.distance(saved, EM.GetComponentData<UniversePosition>(Root).Value), .01,
                "Restore the actual player UniversePosition, not just the saved model.");
            Assert.AreEqual(system, galaxy.State.CurrentStarSystem);
            Assert.AreEqual(profile, Game.Save.ActiveProfile);
            Assert.AreEqual("true", Host.Commands.Execute("until cond=player.ready", Host.Context).Text);
        }
    }
}
