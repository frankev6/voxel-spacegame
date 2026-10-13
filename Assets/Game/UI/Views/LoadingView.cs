using System;
using Game.Core;
using Game.UI.Controls;
using UnityEngine.UIElements;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Screen contract shell with SPEC-61 §4.1 metadata and §6 constructor.</summary>
    public sealed class LoadingView : UIScreen
    {
        readonly LoadingViewModel viewModel;
        Meter progress;

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
            Q<Label>("loading-status");
            progress = Q<Meter>("loading-progress");
            progress.LowThreshold = 0;
        }
        public override void OnPush(object args)
        {
            viewModel.propertyChanged += OnModelChanged;
            RefreshProgress();
        }
        public override void OnPop() => viewModel.propertyChanged -= OnModelChanged;
        public override bool OnBack() => true;
        void OnModelChanged(object sender, BindablePropertyChangedEventArgs args) => RefreshProgress();
        void RefreshProgress()
        {
            progress.SetFraction(viewModel.Progress);
            progress.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.ShowProgress);
        }
        // TODO(HUD-019): verify the complete loading/dock screen acceptance after composition integration.
    }
}
