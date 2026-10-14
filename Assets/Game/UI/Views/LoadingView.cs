using System;
using Game.Core;
using Game.UI.Controls;
using UnityEngine.UIElements;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>HUD-019: non-dismissable loading root, with live status and bounded progress.</summary>
    public sealed class LoadingView : UIScreen
    {
        readonly LoadingViewModel viewModel;
        Meter progress;
        Label status;
        bool observing;

        public LoadingView(LoadingViewModel vm) => viewModel = vm ?? throw new ArgumentNullException(nameof(vm));

        public override string Key => "loading";
        public override UILayer Layer => UILayer.Modals;
        public override InputContext Context => InputContext.None;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => true;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            status = Q<Label>("loading-status");
            progress = Q<Meter>("loading-progress");
            progress.LowThreshold = 0;
        }
        public override void OnPush(object args)
        {
            if (!observing) { viewModel.propertyChanged += OnModelChanged; observing = true; }
            RefreshProgress();
        }
        public override void OnPop() { if (observing) { viewModel.propertyChanged -= OnModelChanged; observing = false; } }
        public override bool OnBack() => true;
        void OnModelChanged(object sender, BindablePropertyChangedEventArgs args) => RefreshProgress();
        void RefreshProgress()
        {
            status.text = viewModel.StatusText;
            progress.SetFraction(viewModel.Progress);
            progress.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.ShowProgress);
        }
    }
}
