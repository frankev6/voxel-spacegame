using System;
using Game.App.Flow;
using Game.Core.Events;
using Game.UI.ViewModels;

namespace Game.App.Presenters
{
    /// <summary>App-owned transition data; the composition polls Tick with the UI frame, without a second clock.</summary>
    internal sealed class LoadingPresenter : IDisposable
    {
        readonly SceneFlow flow;
        readonly LoadingViewModel model;
        bool bound;
        internal LoadingPresenter(SceneFlow flow, LoadingViewModel model)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
        }
        internal void Bind()
        {
            if (bound) return;
            bound = true;
            EventBus<SceneTransitionStarted>.Subscribe(OnStarted);
            EventBus<SceneTransitionFinished>.Subscribe(OnFinished);
            Refresh();
        }
        internal void Tick() { if (bound && flow.IsTransitioning) Refresh(); }
        void OnStarted(SceneTransitionStarted value) => Refresh();
        void OnFinished(SceneTransitionFinished value) => Refresh();
        void Refresh()
        {
            // Events are deferred: read current flow rather than replaying stale transition state.
            model.StatusText = "Loading…";
            model.Progress = flow.Progress;
            model.ShowProgress = flow.IsTransitioning;
        }
        public void Dispose()
        {
            if (!bound) return;
            bound = false;
            EventBus<SceneTransitionStarted>.Unsubscribe(OnStarted);
            EventBus<SceneTransitionFinished>.Unsubscribe(OnFinished);
        }
    }
}
