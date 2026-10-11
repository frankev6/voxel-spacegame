using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.TestKit
{
    /// <summary>SPEC-71 TST-008: fast iteration requires both reloads to remain disabled.</summary>
    public sealed class ProjectSettingsTests
    {
        [Test] public void EnterPlayMode_ReloadsDisabled()
        {
            Assert.IsTrue(EditorSettings.enterPlayModeOptionsEnabled);
            const EnterPlayModeOptions required = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            Assert.AreEqual(required, EditorSettings.enterPlayModeOptions & required);
        }
    }
}
