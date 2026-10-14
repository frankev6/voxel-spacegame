using System;
using Game.Narrative.ShipLog;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.Presentation.Presenters
{
    /// <summary>SPEC-61 §5.4/HUD-010: tracked objective projection, refreshed only by the canonical query's change event.</summary>
    public sealed class HudObjectivePresenter : IDisposable
    {
        readonly HudViewModel model;
        ShipLogQuery query;
        bool disposed;
        public bool IsBound => query != null;
        public HudObjectivePresenter(HudViewModel model) => this.model = model ?? throw new ArgumentNullException(nameof(model));
        /// <summary>False means no canonical ShipLogQuery is supplied; composition binds it when registered.</summary>
        public bool Bind(ShipLogQuery source)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HudObjectivePresenter)); Unbind();
            if (source == null) return false;
            source.Changed += Refresh;
            query = source;
            try { Refresh(); return true; }
            catch { Unbind(); throw; }
        }
        public void Unbind()
        {
            if (query != null) { query.Changed -= Refresh; query = null; }
            model.HasObjective = false; model.ObjectiveTitle = ""; model.ObjectiveText = "";
        }
        void Refresh()
        {
            if (query == null || !query.TryGetTracked(out var quest, out var objective))
            { model.HasObjective = false; model.ObjectiveTitle = ""; model.ObjectiveText = ""; return; }
            model.ObjectiveTitle = quest.Title;
            string text = string.IsNullOrEmpty(objective.Text) ? quest.StageText : objective.Text;
            model.ObjectiveText = objective.Target > 0 ? text + " (" + NumberStringCache.Get(objective.Progress) + "/" + NumberStringCache.Get(objective.Target) + ")" : text;
            model.HasObjective = true;
        }
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; }
    }
}
