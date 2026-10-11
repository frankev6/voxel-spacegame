using System;
using System.Collections.Generic;
using System.IO;
using Game.App.Flow;
using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.Tests.Common;
using Game.Tests.EditMode.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.App
{
    internal sealed class SessionFixture : IDisposable
    {
        public readonly EcsTestWorld World = EcsTestWorld.Create();
        public readonly GameConfigDefinition Config = ScriptableObject.CreateInstance<GameConfigDefinition>();
        public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "Session_" + Guid.NewGuid().ToString("N"));
        public readonly SaveService Save;
        public readonly SceneFlow Flow;
        public readonly GameSession Session;
        public SessionFixture()
        {
            World.AddSingleton(default(PersistentIdAllocator));
            Save = new SaveService(new SavePaths(DirectoryPath), null, null, new SaveServiceConfig());
            Flow = new SceneFlow(World.World, new FakePause(), Save, Config);
            Session = new GameSession(Flow, Save, World.World, null, Config);
            Save.AttachContext(Session); Save.Register(new SessionSaveParticipant(Session)); SetScene(GameScene.MainMenu);
        }
        public void SetScene(GameScene scene) => typeof(SceneFlow).GetProperty(nameof(SceneFlow.CurrentScene)).SetValue(Flow, scene);
        public void SetState(SceneFlowState state) => typeof(SceneFlow).GetProperty(nameof(SceneFlow.State)).SetValue(Flow, state);
        public void Begin(bool creative = false) => Session.BeginNewGame(new NewGameArgs("Rook", 987654321, creative, ContentId.None));
        public void Dispose()
        {
            Session.Dispose(); Flow.Dispose(); Save.Dispose(); World.Dispose(); UnityEngine.Object.DestroyImmediate(Config);
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true);
        }
    }

    public sealed class GameSessionTests
    {
        [SetUp] public void SetUp() => EventHub.Reset();
        [TearDown] public void TearDown() => EventHub.Reset();
        sealed class Model : ISessionModel, INewGameInitializer
        {
            readonly List<string> log; readonly string name;
            public int NewGameOrder { get; }
            public Model(List<string> log, string name, int order) { this.log = log; this.name = name; NewGameOrder = order; }
            public void ResetToEmpty() => log.Add("reset " + name);
            public void InitializeNewGame(in NewGameArgs args) => log.Add("init " + name);
        }
        sealed class Blocker : ISaveBlocker { public SaveBlockReason Reason; public SaveBlockReason GetSaveBlock() => Reason; }

        [Test]
        public void NewGame_ResetsAndInitializes()
        {
            using var f = new SessionFixture(); var log = new List<string>();
            var a = new Model(log, "a", 20); var b = new Model(log, "b", 5);
            f.Session.RegisterModel(a); f.Session.RegisterModel(b);
            f.Session.RegisterNewGameInitializer(a); f.Session.RegisterNewGameInitializer(b);
            var reasons = new List<SessionResetReason>(); EventBus<SessionReset>.Subscribe(e => reasons.Add(e.Reason));
            f.Begin(); EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "reset a", "reset b", "init b", "init a" }, log);
            CollectionAssert.AreEqual(new[] { SessionResetReason.NewGame }, reasons);
            Assert.IsTrue(f.Session.HasActiveSession); Assert.AreEqual("Rook", f.Session.CommanderName);
            Assert.AreEqual(987654321ul, f.Session.CampaignSeed); Assert.AreEqual(1ul, f.Session.NextPersistentId);
            log.Clear(); f.Begin(); EventHub.DrainAll();
            CollectionAssert.AreEqual(new[] { "reset a", "reset b", "init b", "init a" }, log);
            Assert.AreEqual(2, reasons.Count, "one event per reset on the same model instances");
        }

        [Test]
        public void BlockReasons_Table()
        {
            using var f = new SessionFixture(); var blocker = new Blocker(); f.Session.RegisterSaveBlocker(blocker);
            Assert.AreEqual(SaveBlockReason.NoSession, f.Session.GetBlockReason()); f.Begin();
            var cases = new[] {
                (SceneFlowState.Loading, GameScene.BlueprintLab, true, SaveBlockReason.InCombat, SaveBlockReason.Transitioning),
                (SceneFlowState.Idle, GameScene.BlueprintLab, true, SaveBlockReason.InCombat, SaveBlockReason.EditorOpen),
                (SceneFlowState.Idle, GameScene.World, true, SaveBlockReason.InCombat, SaveBlockReason.PlayerDead),
                (SceneFlowState.Idle, GameScene.World, false, SaveBlockReason.InCombat, SaveBlockReason.InCombat),
                (SceneFlowState.Idle, GameScene.World, false, SaveBlockReason.None, SaveBlockReason.None),
            };
            foreach (var c in cases)
            {
                f.SetState(c.Item1); f.SetScene(c.Item2); f.Session.PlayerDefeated = c.Item3; blocker.Reason = c.Item4;
                Assert.AreEqual(c.Item5, f.Session.GetBlockReason()); Assert.AreEqual(c.Item5, f.Session.GetSaveBlock());
            }
        }

        [Test]
        public void DisallowedScene_NotAllowed()
        {
            using var f = new SessionFixture(); f.SetScene(GameScene.None);
            Assert.AreEqual(FlowResult.NotAllowed, f.Session.NewGameAsync(default).GetAwaiter().GetResult());
            Assert.AreEqual(FlowResult.NotAllowed, f.Session.LoadGameAsync(null).GetAwaiter().GetResult());
            Assert.AreEqual(FlowResult.NotAllowed, f.Session.SaveGameAsync(null, SaveReason.Manual).GetAwaiter().GetResult());
            Assert.AreEqual(FlowResult.NotAllowed, f.Session.QuitToMainMenuAsync(true).GetAwaiter().GetResult());
            Assert.AreEqual(FlowResult.NotAllowed, f.Session.EnterBlueprintLabAsync(default).GetAwaiter().GetResult());
            Assert.AreEqual(FlowResult.NotAllowed, f.Session.ExitBlueprintLabAsync().GetAwaiter().GetResult());
            f.SetScene(GameScene.World);
            Assert.AreEqual(FlowResult.NotAllowed, f.Session.EnterBlueprintLabAsync(new BlueprintLabArgs(BlueprintLabMode.Creative, 0, ContentId.None)).GetAwaiter().GetResult());
            f.SetScene(GameScene.MainMenu);
            Assert.AreEqual(FlowResult.NotAllowed, f.Session.EnterBlueprintLabAsync(new BlueprintLabArgs(BlueprintLabMode.FleetShip, 1, ContentId.None)).GetAwaiter().GetResult());
            f.SetState(SceneFlowState.Loading);
            Assert.AreEqual(FlowResult.Busy, f.Session.NewGameAsync(default).GetAwaiter().GetResult());
        }
    }
}
