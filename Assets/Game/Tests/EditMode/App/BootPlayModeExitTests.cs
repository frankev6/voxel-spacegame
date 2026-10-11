using System.Collections;
using Game.App.Boot;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.App
{
    /// <summary>FND-002 acceptance: entering Play Mode boots one GameRoot and one Default World; exiting disposes the World and leaves
    /// GameRoot.Instance == null (domain reload is off, so the static survives the exit and can be checked).</summary>
    public sealed class BootPlayModeExitTests
    {
        [UnityTest]
        public IEnumerator ExitPlayMode_DisposesWorld_ClearsInstance()
        {
            yield return new EnterPlayMode();
            Assert.IsNotNull(GameRoot.Instance);
            var world = World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world);
            Assert.AreEqual(Bootstrap.WorldName, world.Name);
            yield return new ExitPlayMode();
            Assert.IsNull(GameRoot.Instance);
            Assert.IsFalse(world.IsCreated, "the Default World is disposed on exit");
        }
    }
}
