using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-51 §4.4: virtualized blueprint browser, with current row identities and screen-scoped subscriptions.</summary>
    public sealed class BlueprintBrowserView : UIScreen
    {
        readonly BlueprintBrowserViewModel viewModel;
        readonly IBlueprintBrowserCommands commands;
        readonly List<Row> rows = new();
        readonly int[] selected = new int[1];
        ListView list;
        Button stamp, replace, delete;
        Label empty;
        bool observing, refreshing;
        public BlueprintBrowserView(BlueprintBrowserViewModel vm, IBlueprintBrowserCommands commands)
        { viewModel = vm ?? throw new ArgumentNullException(nameof(vm)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands)); }
        public override string Key => "blueprint-browser";
        public override UILayer Layer => UILayer.Modals;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => false;
        public override bool IsRoot => false;
        public override VisualElement DefaultFocus => list;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel; list = Q<ListView>("blueprint-browser-list"); empty = Q<Label>("blueprint-browser-empty");
            stamp = Q<Button>("blueprint-browser-stamp"); replace = Q<Button>("blueprint-browser-replace"); delete = Q<Button>("blueprint-browser-delete");
            list.itemsSource = viewModel.Rows; list.selectionType = SelectionType.Single;
            list.makeItem = () => { var row = new Row(); rows.Add(row); return row.Root; };
            list.bindItem = (element, index) => ((Row)element.userData).Bind(index >= 0 && index < viewModel.Rows.Count ? viewModel.Rows[index] : null, index, observing);
            list.unbindItem = (element, _) => ((Row)element.userData).Unbind();
            list.destroyItem = element => { var row = (Row)element.userData; row.Unbind(); rows.Remove(row); };
            list.selectionChanged += _ => { if (observing && !refreshing) commands.Select(list.selectedIndex); };
            list.itemsChosen += _ => { if (observing && viewModel.CanLoad) commands.LoadStamp(); };
            stamp.clicked += () => { if (observing && viewModel.CanLoad) commands.LoadStamp(); };
            replace.clicked += () => { if (observing && viewModel.CanLoad) commands.LoadReplace(); };
            delete.clicked += () => { if (observing && viewModel.CanDelete) commands.Delete(); };
            Q<Button>("blueprint-browser-close").clicked += () => { if (observing) Close(); };
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root)) Stop(); });
            Root.RegisterCallback<AttachToPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root) && IsOpen) Start(); });
        }
        public override void OnPush(object args) => Start();
        public override void OnPop() => Stop();
        void Start() { if (observing) return; observing = true; viewModel.propertyChanged += Changed; Refresh(); }
        void Stop()
        {
            if (!observing) return; observing = false; viewModel.propertyChanged -= Changed;
            foreach (var row in rows) row.Unbind();
        }
        void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        void Refresh()
        {
            if (!observing || refreshing) return; refreshing = true;
            try
            {
                var row = viewModel.SelectedIndex >= 0 && viewModel.SelectedIndex < viewModel.Rows.Count ? viewModel.Rows[viewModel.SelectedIndex] : null;
                stamp.SetEnabled(viewModel.CanLoad && row != null); replace.SetEnabled(viewModel.CanLoad && row != null);
                delete.SetEnabled(viewModel.CanDelete && row != null && !row.ReadOnly);
                empty.EnableInClassList(UIStyleClasses.IsHidden, viewModel.Rows.Count != 0);
                selected[0] = viewModel.SelectedIndex;
                list.SetSelectionWithoutNotify(row != null ? selected : Array.Empty<int>()); list.RefreshItems();
            }
            finally { refreshing = false; }
        }
        sealed class Row
        {
            internal readonly VisualElement Root = new() { pickingMode = PickingMode.Ignore };
            readonly Label name = new() { pickingMode = PickingMode.Ignore }, count = new() { pickingMode = PickingMode.Ignore }, readOnly = new() { pickingMode = PickingMode.Ignore, text = "Content · read-only" };
            BlueprintRowViewModel model;
            bool observing;
            internal Row()
            {
                Root.userData = this; Root.AddToClassList("blueprint-row");
                name.AddToClassList("blueprint-row-name"); name.AddToClassList(UIStyleClasses.TextEllipsis);
                var details = new VisualElement { pickingMode = PickingMode.Ignore }; details.AddToClassList("blueprint-row-details");
                count.AddToClassList("text-s"); readOnly.AddToClassList("text-muted"); details.Add(count); details.Add(readOnly); Root.Add(name); Root.Add(details);
            }
            internal void Bind(BlueprintRowViewModel value, int index, bool active)
            {
                Unbind(); model = value; Root.dataSource = model; Root.name = "blueprint-browser-row-" + index;
                name.name = Root.name + "-name"; count.name = Root.name + "-count"; readOnly.name = Root.name + "-readonly";
                if (active && model != null) { model.propertyChanged += Changed; observing = true; }
                Refresh();
            }
            void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh();
            void Refresh()
            {
                name.text = model?.Name ?? ""; count.text = model == null ? "" : NumberStringCache.Get(model.BlockCount) + " blocks";
                readOnly.EnableInClassList(UIStyleClasses.IsHidden, model?.ReadOnly != true);
                Root.EnableInClassList(UIStyleClasses.IsSelected, model?.IsSelected == true);
            }
            internal void Unbind() { if (observing) model.propertyChanged -= Changed; observing = false; model = null; Root.dataSource = null; }
        }
    }
}
