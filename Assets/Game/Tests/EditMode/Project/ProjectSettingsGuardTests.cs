using Game.Editor;
using NUnit.Framework;

namespace Game.Tests.EditMode.Project
{
    /// <summary>Fails when a required Unity setting drifts (see Game.Editor.ProjectSetup and docs/01-architecture.md §2).</summary>
    [Category("Project")]
    public class ProjectSettingsGuardTests
    {
        [Test]
        public void RequiredSettings_AllApplied_NoDeviations()
        {
            var issues = ProjectSetup.Check();
            Assert.IsEmpty(issues, "Run menu Game > Project Setup > Apply Required Settings.\n" + string.Join("\n", issues));
        }
    }
}
