using Game.App.DevRuntime.Playground;
using Game.Core.Flow;
using NUnit.Framework;

namespace Game.Tests.EditMode.App
{
    /// <summary>Owner (2026-11-05): Enter playground from any scene except a Blueprint Lab with unsaved work.</summary>
    public sealed class PlaygroundEntryTests
    {
        [Test] public void CanEnterFrom_AnySceneButLab()
        {
            foreach (var scene in new[] { GameScene.None, GameScene.Boot, GameScene.MainMenu, GameScene.World })
                Assert.IsTrue(DevPlayground.CanEnterFrom(scene, false), scene.ToString());
            Assert.IsFalse(DevPlayground.CanEnterFrom(GameScene.BlueprintLab, false), "A campaign's lab keeps its unsaved edits");
            Assert.IsTrue(DevPlayground.CanEnterFrom(GameScene.BlueprintLab, true), "The playground's own lab resets directly");
        }
    }
}
