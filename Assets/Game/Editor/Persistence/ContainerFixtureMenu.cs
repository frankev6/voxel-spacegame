using System;
using Game.Core;
using UnityEditor;

namespace Game.Editor.Persistence
{
    /// <summary>SPEC-05 PERS-010: write missing version fixtures once without an Editor → tests assembly dependency.</summary>
    public static class ContainerFixtureMenu
    {
        [MenuItem("Game/Dev/Persistence/Write Container Fixtures")]
        public static void Write()
        {
            var type = Type.GetType("Game.Tests.EditMode.Persistence.SaveFixtureHarness, Game.Tests.EditMode", true);
            type.GetMethod("WriteContainerFixtures").Invoke(null, null);
            AssetDatabase.Refresh();
            GameLog.Info("Persistence", "Container fixtures written; existing fixtures were preserved.");
        }
    }
}
