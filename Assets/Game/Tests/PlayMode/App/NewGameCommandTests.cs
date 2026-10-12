using System.Collections;
using System.IO;
using Game.App.DevRuntime;
using Game.App.Testing;
using Game.Core.Components;
using Game.Core.Save;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.App
{
    public abstract class SessionCommandFixture : ProductionSessionFixture
    {
        protected IEnumerator RunCommand(string line)
        {
            var result = Host.Commands.Execute(line, Host.Context);
            Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text);
            Assert.IsTrue(Host.TryGetOperation(result.OperationId, out var operation));
            yield return Until(() => operation.Status != DevOperationStatus.Running, "Command completion: " + line, 60);
            Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status, operation.Message);
        }
    }
    /// <summary>DEV-008: commands use production flow/input/physics; saves stay in the selected harness profile.</summary>
    public sealed class NewGameCommandTests : SessionCommandFixture
    {
        [UnityTest] public IEnumerator Seed42_ReachesReady()
        {
            var harness = Game.Save.ActiveProfile;
            var player = Game.Save.CreateProfile("Player");
            string playerFolder = Path.Combine(GameRootTestHooks.SaveRoot, player.Id.Value);
            string before = File.ReadAllText(Path.Combine(playerFolder, "profile.json"));
            yield return RunCommand("newgame seed=42");
            Assert.AreEqual("true", Host.Commands.Execute("until cond=player.ready", Host.Context).Text);
            Assert.AreEqual(42ul, Game.Session.CampaignSeed);
            Assert.AreEqual(harness, Game.Save.ActiveProfile);
            yield return Run(Game.Session.SaveGameAsync("manual_008", SaveReason.Manual));
            Assert.IsTrue(File.Exists(Path.Combine(GameRootTestHooks.SaveRoot, harness.Value, "manual_008", "world.sav")));
            Assert.AreEqual(before, File.ReadAllText(Path.Combine(playerFolder, "profile.json")));
            Assert.AreEqual(0, Game.Save.ListSlots(player.Id).Count, "No harness saves in the player's profile");
            yield return Run(Game.Session.QuitToMainMenuAsync(false));
        }
        [UnityTest] public IEnumerator SameSeed_SameSpawn()
        {
            yield return RunCommand("newgame seed=42");
            var root = Host.Context.PlayerShipRoot;
            ulong id = EM.GetComponentData<PersistentId>(root).Value;
            float3 position = EM.GetComponentData<LocalTransform>(root).Position;
            yield return RunCommand("newgame seed=42");
            root = Host.Context.PlayerShipRoot;
            Assert.AreEqual(id, EM.GetComponentData<PersistentId>(root).Value);
            Assert.AreEqual(position, EM.GetComponentData<LocalTransform>(root).Position);
            Assert.AreEqual(1, Game.Save.ListProfiles().Count, "Reuse the existing harness profile");
            yield return Run(Game.Session.QuitToMainMenuAsync(false));
        }
    }
}
