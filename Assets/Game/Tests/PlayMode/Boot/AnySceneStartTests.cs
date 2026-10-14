using System.Collections;
using Game.App.Boot;
using Game.App.Flow;
using Game.Core;
using Game.Core.Components;
using Game.Core.Flow;
using Game.Tests.Common;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Boot
{
    /// <summary>FND-015: actual bootstrap/SceneFlow mapping, including creative Lab startup.</summary>
    public sealed class AnySceneStartTests : GameFlowFixture
    {
        IEnumerator Restart(string scene, GameScene expected)
        {
            if (Service<IGameFlow>().HasActiveSession)
            {
                var leave = Service<IGameFlow>().QuitToMainMenuAsync(false).GetAwaiter();
                yield return WaitUntil(() => leave.IsCompleted, 10, "Leave before startup check"); Assert.AreEqual(FlowResult.Ok, leave.GetResult());
            }
            if (GameRoot.Instance != null) { Object.Destroy(GameRoot.Instance.gameObject); yield return null; }
            var loaded = SceneManager.LoadSceneAsync(scene); yield return loaded; double started = Time.realtimeSinceStartupAsDouble; Bootstrap.Boot();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                using var query = Em.CreateEntityQuery(typeof(VoxelConfig));
                if (!query.IsEmptyIgnoreFilter) { var config = query.GetSingleton<VoxelConfig>(); config.RequireRenderer = 0; query.SetSingleton(config); }
            }
            yield return WaitUntil(() => Services.TryGet<SceneFlow>(out var flow) && flow.CurrentScene == expected && !flow.IsTransitioning, 10, scene + " startup reaches " + expected);
            Assert.Less(Time.realtimeSinceStartupAsDouble - started, 10);
        }
        [UnityTest] public IEnumerator Boot()
        { yield return Restart("Boot", GameScene.MainMenu); Assert.IsFalse(Service<IGameFlow>().HasActiveSession); }
        [UnityTest] public IEnumerator MainMenu()
        {
            yield return Restart("MainMenu", GameScene.MainMenu); var handle = SceneManager.GetActiveScene().handle;
            var started = Service<SceneFlow>().StartupAsync().GetAwaiter(); yield return WaitUntil(() => started.IsCompleted, 10, "In-place menu entry");
            Assert.AreEqual(FlowResult.Ok, started.GetResult()); Assert.AreEqual(handle, SceneManager.GetActiveScene().handle); Assert.IsFalse(Service<IGameFlow>().HasActiveSession);
        }
        [UnityTest] public IEnumerator World_DevQuickStart()
        {
            yield return Restart("World", GameScene.World); Assert.IsTrue(Service<IGameFlow>().HasActiveSession); Assert.IsFalse(Service<IGameFlow>().IsCreativeSession);
            yield return WaitUntil(() => PlayerReady, 10, "Actual quick-start player is ready");
            using var active = Em.CreateEntityQuery(typeof(GameplayActive)); Assert.IsFalse(active.IsEmptyIgnoreFilter);
        }
        [UnityTest] public IEnumerator BlueprintLab_Creative()
        {
            yield return Restart("BlueprintLab", GameScene.BlueprintLab); Assert.IsTrue(Service<IGameFlow>().HasActiveSession); Assert.IsTrue(Service<IGameFlow>().IsCreativeSession);
        }
    }
}
