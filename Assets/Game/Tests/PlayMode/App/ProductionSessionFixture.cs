using System;
using System.Collections;
using System.IO;
using Game.App.Boot;
using Game.App.DevRuntime;
using Game.App.Testing;
using Game.Core;
using Game.Core.Flow;
using Game.Core.Save;
using Game.Simulation.Commands;
using Game.Voxel.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.App
{
    /// <summary>Shared real-boot fixture with isolated save/settings paths; only headless runs bypass GPU readiness.</summary>
    public abstract class ProductionSessionFixture
    {
        /// <summary>Campaigns drop their automatic beats (the opening dialogue pauses the game); story cases opt out.</summary>
        protected virtual bool QuietCampaign => true;
        string directory;
        internal GameComposition Game => GameRoot.Instance.Composition;
        protected EntityManager EM => World.DefaultGameObjectInjectionWorld.EntityManager;
        protected DevRuntimeHost Host => Services.Get<DevRuntimeHost>();
        protected Entity Root { get { Assert.IsTrue(Game.Session.PlayerSpawner.TryGetRoot(out var root)); return root; } }
        protected PlayerCommand Command { get { using var q = EM.CreateEntityQuery(typeof(PlayerCommand)); return q.GetSingleton<PlayerCommand>(); } }
        protected float3 Position => EM.GetComponentData<LocalTransform>(Root).Position;
        protected float Speed => math.length(EM.GetComponentData<PhysicsVelocity>(Root).Linear);
        protected static IEnumerator Until(Func<bool> condition, string message, double seconds = 10)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(condition(), message);
        }
        internal static IEnumerator Run(Awaitable<FlowResult> operation)
        {
            var awaiter = operation.GetAwaiter();
            yield return Until(() => awaiter.IsCompleted, "Flow did not complete", 30);
            Assert.AreEqual(FlowResult.Ok, awaiter.GetResult());
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            if (GameRoot.Instance != null) { Object.Destroy(GameRoot.Instance.gameObject); yield return null; }
            directory = Path.Combine(Application.temporaryCachePath, "PlayableShip_" + Guid.NewGuid().ToString("N"));
            GameRootTestHooks.SaveRoot = Path.Combine(directory, "saves");
            GameRootTestHooks.SettingsPath = Path.Combine(directory, "settings.json");
            GameRootTestHooks.SkipIntroScreens = true;
            // The campaign opening has its own owner tests (OpeningChapterTests); flight fixtures start in free flight.
            GameRootTestHooks.SkipCampaignOpening = QuietCampaign;
            Bootstrap.Boot();
            // Only headless functional runs skip the GPU prerequisite. Live/GPU runs use the production renderer unchanged.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                using var q = EM.CreateEntityQuery(typeof(VoxelConfig)); var config = q.GetSingleton<VoxelConfig>();
                config.RequireRenderer = 0; q.SetSingleton(config);
            }
            yield return null;
            yield return Until(() => !Game.Flow.IsTransitioning, "Startup completed");
            // Test Runner uses its own scene; Startup intentionally leaves non-game scenes alone (SPEC-01 Q9).
            if (Game.Session.HasActiveSession) yield return Run(Game.Session.QuitToMainMenuAsync(false));
            else if (Game.Flow.CurrentScene != GameScene.MainMenu)
                yield return Run(Game.Flow.TransitionAsync(GameScene.MainMenu, SceneEntryReason.Startup));
            Game.Save.SetActiveProfile(Game.Save.CreateProfile("harness").Id);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            // Restore from a settled menu: otherwise Bootstrap in World starts a new async
            // quick-start which the next fixture destroys while it still owns scene work.
            if (GameRoot.Instance != null)
            {
                yield return Until(() => !Game.Flow.IsTransitioning, "Fixture transition settled", 30);
                if (Game.Session.HasActiveSession) yield return Run(Game.Session.QuitToMainMenuAsync(false));
            }
            if (GameRoot.Instance != null) { Object.Destroy(GameRoot.Instance.gameObject); yield return null; }
            GameRootTestHooks.Reset();
            Bootstrap.Boot(); yield return null;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
