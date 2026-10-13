#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Presentation.Vfx;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.VFX;
namespace Game.Tests.PlayMode.Vfx
{
    public sealed class FxContentTests
    {
        internal static FxDefinition[] Definitions() => AssetDatabase.FindAssets("t:ContentDatabase")
            .Select(g => AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(g)))
            .SelectMany(d => d.Definitions).OfType<FxDefinition>().Distinct().ToArray();
        internal static VfxConfigDefinition Config() => AssetDatabase.FindAssets("t:VfxConfigDefinition")
            .Select(g => AssetDatabase.LoadAssetAtPath<VfxConfigDefinition>(AssetDatabase.GUIDToAssetPath(g))).Single(d => d.IdString == "config_vfx");
        [Test] public void AllDefinitions_PassValidate()
        {
            var report = new ValidationReport("VFX authored content"); var definitions = Definitions();
            Assert.GreaterOrEqual(definitions.Length, 7);
            foreach (var definition in definitions) definition.Validate(report.For(definition.IdString), null);
            Config().Validate(report.For("config_vfx"), null);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
            Assert.AreEqual(6, report.WarningCount, "Only the six explicitly staged families may be missing.");
        }
        [UnityTest] public IEnumerator Rig_CreatesWithoutContractErrors()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires VFX graphics device.");
            var rig = FxRig.Create(null, Config(), Definitions(), null, VfxQuality.High);
            try
            {
                Assert.AreEqual(10, rig.GetComponentsInChildren<VisualEffect>().Length);
                Assert.AreEqual(7, rig.SlotByHash.Count);
                yield return null; LogAssert.NoUnexpectedReceived();
                TestContext.WriteLine($"Authored rig GraphicsBuffers: {rig.TotalBufferBytes} bytes; upload/native memory is additional.");
            }
            finally { rig.Dispose(); Object.Destroy(rig.gameObject); }
        }
    }
}
#endif
