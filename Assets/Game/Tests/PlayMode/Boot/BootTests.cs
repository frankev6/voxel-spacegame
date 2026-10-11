using System.Collections;
using System.Linq;
using Game.App.Boot;
using Game.Core;
using Game.Core.Settings;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Boot
{
    /// <summary>FND-002: Bootstrap creates exactly one GameRoot and one Default World; tearing GameRoot down disposes the World
    /// (SPEC-01 §5.1). The real Play Mode exit is covered by the EditMode BootPlayModeExitTests.</summary>
    public sealed class BootTests
    {
        static int DefaultWorlds()
        {
            int n = 0;
            foreach (var w in World.All) if (w.IsCreated && w.Name == Bootstrap.WorldName) n++;   // World.All forbids LINQ (no boxing)
            return n;
        }

        [UnityTest]
        public IEnumerator OneGameRoot_OneWorld()
        {
            yield return null;
            Assert.IsNotNull(GameRoot.Instance, "Bootstrap ran before the first scene");
            Assert.AreEqual(1, Object.FindObjectsByType<GameRoot>(FindObjectsInactive.Include).Length);
            Assert.AreEqual(1, DefaultWorlds());
            Assert.AreEqual(Bootstrap.WorldName, World.DefaultGameObjectInjectionWorld.Name);
            Assert.IsTrue(Services.IsRegistered<ISettingsService>() && Services.IsRegistered<IPauseService>(), "composition steps 1 and 5 ran");
            var root = GameRoot.Instance.transform;
            CollectionAssert.AreEquivalent(new[] { "UI", "AudioRoot", "CameraRoot" }, Enumerable.Range(0, root.childCount).Select(i => root.GetChild(i).name));

            Bootstrap.Boot();                                         // a second boot is a no-op
            yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<GameRoot>(FindObjectsInactive.Include).Length);
            Assert.AreEqual(1, DefaultWorlds());
        }

        [UnityTest]
        public IEnumerator ExitPlay_DisposesWorld()
        {
            yield return null;
            var world = World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(GameRoot.Instance);
            Object.Destroy(GameRoot.Instance.gameObject);              // OnDestroy is what a Play Mode exit runs
            yield return null;
            try
            {
                Assert.IsFalse(world.IsCreated, "the Default World is disposed with GameRoot");
                Assert.IsNull(GameRoot.Instance);
                Assert.IsFalse(Services.IsRegistered<ISettingsService>(), "services are reset");
                Assert.AreEqual(0, DefaultWorlds());
            }
            finally
            {
                Bootstrap.Boot();                                     // leave a booted game for the next test
            }
            yield return null;
            Assert.IsNotNull(GameRoot.Instance);
            Assert.AreEqual(1, DefaultWorlds());
        }
    }
}
