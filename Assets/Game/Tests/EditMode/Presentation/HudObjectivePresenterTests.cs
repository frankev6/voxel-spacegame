using Game.Narrative.ShipLog;
using Game.Presentation.Presenters;
using Game.UI.ViewModels;
using NUnit.Framework;

namespace Game.Tests.EditMode.Presentation
{
    public sealed class HudObjectivePresenterTests
    {
        [Test] public void QuestChange_UpdatesText()
        {
            var model = new HudViewModel { HasObjective = true, ObjectiveTitle = "Stale", ObjectiveText = "Stale" };
            using var presenter = new HudObjectivePresenter(model);
            // Test the real query contract. Its event add accessor is currently an explicit QST-006 stub.
            if (!presenter.Bind(new ShipLogQuery()))
            {
                Assert.IsFalse(model.HasObjective); Assert.IsEmpty(model.ObjectiveTitle); Assert.IsEmpty(model.ObjectiveText);
                Assert.Ignore("QST-006 is not implemented: real ShipLogQuery.Changed/TryGetTracked cannot produce a tracked quest change. Complete this required integration case with that owner; no fake query is substituted.");
            }
            Assert.Fail("QST-006 is now available: wire its real QuestService/FactStore fixture here and verify the tracked objective change, rather than passing on bind alone.");
        }
    }
}
