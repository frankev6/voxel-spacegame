using System.Reflection;
using Game.Core.Components;
using Game.Data.Definitions;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace Game.Tests.EditMode.App
{
    /// <summary>FND-001 contract checks (SPEC-01 §9).</summary>
    public class FoundationContractTests
    {
        [Test]
        public void WorldSceneActive_IsTag()
        {
            var t = typeof(WorldSceneActive);
            Assert.IsTrue(t.IsValueType);
            Assert.IsTrue(typeof(IComponentData).IsAssignableFrom(t));
            Assert.AreEqual(0, t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length, "a tag has no fields");
            Assert.IsTrue(TypeManager.IsZeroSized(TypeManager.GetTypeIndex<WorldSceneActive>()), "zero-sized ECS tag");
        }

        [Test]
        public void GameConfig_Defaults()
        {
            var c = ScriptableObject.CreateInstance<GameConfigDefinition>();
            try
            {
                Assert.IsNull(c.StartBlueprint);
                Assert.AreEqual("Commander", c.DefaultCommanderName);
                Assert.AreEqual(12345ul, c.DevQuickStartSeed);
                Assert.AreEqual(30f, c.SceneReadyTimeoutSeconds);
                Assert.AreEqual(4096, c.PersistentIdBlockSize);
                Assert.AreEqual(1024, c.PersistentIdRefillThreshold);
                Assert.Less(c.PersistentIdRefillThreshold, c.PersistentIdBlockSize);
                Assert.AreEqual(3f, c.DefeatDelaySeconds);
                Assert.AreEqual(10f, c.CombatDamageWindowSeconds);
                Assert.AreEqual(0.5f, c.SettingsSaveDebounceSeconds);
                var prefix = typeof(GameConfigDefinition).GetCustomAttribute<ContentIdPrefixAttribute>();
                Assert.IsNotNull(prefix);
                CollectionAssert.AreEqual(new[] { "config_" }, prefix.Prefixes);
            }
            finally { Object.DestroyImmediate(c); }
        }
    }
}
