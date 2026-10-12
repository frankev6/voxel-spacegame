using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.App.Flow;
using Game.App.Settings;
using Game.App.Testing;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Core.Settings;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.Tests.Common;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.TestKit
{
    public sealed class GameFlowFixtureTests : GameFlowFixture
    {
        Dictionary<string, DateTime> realProfileTimes;
        bool checkCleanup;
        [SetUp] public void SnapshotRealProfiles() { realProfileTimes = RealProfileTimes(); checkCleanup = false; }
        [UnityTest] public IEnumerator Boot_ReachesMenu()
        {
            Assert.AreEqual(GameScene.MainMenu, Service<SceneFlow>().CurrentScene);
            Assert.IsFalse(Service<SceneFlow>().IsTransitioning);
            Assert.AreEqual(Unity.Entities.Entity.Null, PlayerRoot);
            Assert.IsTrue(GameRootTestHooks.SkipIntroScreens);
            yield break;
        }
        [UnityTest] public IEnumerator Sandbox_PlayerReady()
        {
            yield return NewGame(42);
            Assert.IsTrue(PlayerReady);
            yield return WaitTicks(2);
            using (HoldSimulation("Fixture pause helper")) yield return WaitFrames(2);
            yield return EnterSandbox();
            Assert.IsTrue(PlayerReady);
            Assert.IsTrue(Service<IGameFlow>().IsCreativeSession);
        }
        [UnityTest] public IEnumerator Saves_InTempRoot()
        {
            yield return NewGame(42);
            var save = Service<SaveService>();
            var slot = new SlotRef(save.ActiveProfile, new SlotId("manual_001"));
            var operation = Service<IGameFlow>().SaveGameAsync(slot.Slot.Value, SaveReason.Manual).GetAwaiter();
            yield return WaitUntil(() => operation.IsCompleted, 30, "Save completes");
            Assert.AreEqual(FlowResult.Ok, operation.GetResult());
            Assert.IsTrue(File.Exists(new SavePaths(Path.Combine(TestDirectory, "saves")).WorldFile(slot)));
            var settings = Service<ISettingsService>(); var edit = settings.CreateEditableCopy();
            edit.Audio.MasterVolume = .37f;
            settings.Apply(edit, SettingsSection.Audio); settings.Flush();
            Assert.IsTrue(File.Exists(Path.Combine(TestDirectory, "settings.json")));
            CollectionAssert.AreEquivalent(realProfileTimes, RealProfileTimes(), "Real profile/settings timestamps stay unchanged");
        }
        [UnityTest] public IEnumerator Teardown_Cleans()
        {
            yield return NewGame(42);
            Assert.IsTrue(Directory.Exists(TestDirectory));
            checkCleanup = true;
        }
        [UnityTearDown] public IEnumerator VerifyCleanup()
        {
            string folder = TestDirectory;
            yield return GameFlowTearDown();
            if (checkCleanup)
            {
                Assert.IsFalse(Directory.Exists(folder));
                Assert.IsNull(GameRootTestHooks.SaveRoot); Assert.IsNull(GameRootTestHooks.SettingsPath);
                Assert.IsFalse(GameRootTestHooks.SkipIntroScreens);
            }
            CollectionAssert.AreEquivalent(realProfileTimes, RealProfileTimes(), "Fixture leaves real profiles/settings untouched");
        }
        static Dictionary<string, DateTime> RealProfileTimes()
        {
            var times = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            string root = SavePaths.CreateDefault().Root;
            if (Directory.Exists(root))
            {
                times[root] = Directory.GetLastWriteTimeUtc(root);
                foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories)) times[dir] = Directory.GetLastWriteTimeUtc(dir);
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) times[file] = File.GetLastWriteTimeUtc(file);
            }
            string settings = SettingsPaths.Default.FilePath;
            if (File.Exists(settings)) times[settings] = File.GetLastWriteTimeUtc(settings);
            return times;
        }
    }
}
