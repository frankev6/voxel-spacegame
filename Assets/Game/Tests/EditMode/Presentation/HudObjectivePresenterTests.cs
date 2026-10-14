using Game.Core.Events;
using Game.Presentation.Presenters;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Narrative;
using Game.UI.ViewModels;
using NUnit.Framework;

namespace Game.Tests.EditMode.Presentation
{
    public sealed class HudObjectivePresenterTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        [Test] public void QuestChange_UpdatesText()
        {
            using var source = new ShipLogQueryTests.Fixture();
            var model = new HudViewModel { HasObjective = true, ObjectiveTitle = "Stale", ObjectiveText = "Stale" };
            using var presenter = new HudObjectivePresenter(model);
            Assert.IsFalse(presenter.Bind(null)); Assert.IsFalse(model.HasObjective); Assert.IsEmpty(model.ObjectiveText);
            Assert.IsTrue(presenter.Bind(source.Log)); Assert.IsFalse(model.HasObjective);
            source.Quests.Start(source.MainIndex); Assert.IsFalse(model.HasObjective);
            source.Frame(); Assert.IsTrue(model.HasObjective); Assert.AreEqual("Main", model.ObjectiveTitle);
            Assert.AreEqual("Collect three (0/3)", model.ObjectiveText);
            source.Set("story_count", 2); Assert.AreEqual("Collect three (0/3)", model.ObjectiveText);
            source.Frame(); Assert.AreEqual("Collect three (2/3)", model.ObjectiveText);
            string unchanged = model.ObjectiveText; source.Frame(); Assert.AreSame(unchanged, model.ObjectiveText);
            source.Set("story_count", 3); source.Frame(); Assert.AreEqual("Go back", model.ObjectiveText);
            source.Quests.ForceComplete(source.MainIndex); Assert.IsTrue(model.HasObjective);
            source.Frame(); Assert.IsFalse(model.HasObjective); Assert.IsEmpty(model.ObjectiveTitle); Assert.IsEmpty(model.ObjectiveText);
            presenter.Unbind(); source.Quests.Start(source.ContractIndex); source.Frame(); Assert.IsFalse(model.HasObjective);
        }
    }
}
