using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;
using System;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-61 HUD-004: virtualized real slot rows and mode-specific save/load actions.</summary>
    public sealed class SaveSlotsView : UIScreen
    {
        readonly SaveSlotsViewModel viewModel;
        readonly ISaveSlotsCommands commands;
        ListView list;
        Button save, load, create, delete;
        Label title, empty;
        readonly int[] selection = new int[1];
        bool observing;

        public SaveSlotsView(SaveSlotsViewModel vm, ISaveSlotsCommands commands)
        {
            viewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public override string Key => "save-slots";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        public override VisualElement DefaultFocus => list;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            list = Q<ListView>("save-slots-list");
            var template = list.itemTemplate ?? throw new InvalidOperationException("save-slots: missing row template");
            // Take over construction explicitly while retaining the authored UXML reference.
            list.itemTemplate = null;
            list.makeItem = () => template.Instantiate();
            list.bindItem = BindItem;
            list.unbindItem = (row, _) => row.dataSource = null;
            list.itemsSource = viewModel.Slots;
            list.selectionType = SelectionType.Single;
            list.selectionChanged += _ => commands.Select(list.selectedIndex);
            title = Q<Label>("save-slots-title"); empty = Q<Label>("save-slots-empty");
            save = Q<Button>("save-slots-save"); load = Q<Button>("save-slots-load");
            create = Q<Button>("save-slots-new"); delete = Q<Button>("save-slots-delete");
            save.clicked += commands.SaveToSelected; load.clicked += commands.LoadSelected;
            create.clicked += commands.SaveNew; delete.clicked += commands.DeleteSelected;
            Q<Button>("save-slots-close").clicked += Close;
        }
        void BindItem(VisualElement row, int index)
        {
            var item = viewModel.Slots[index]; row.dataSource = item;
            row.Q<VisualElement>("save-slot-row").EnableInClassList(UIStyleClasses.IsSelected, item.IsSelected);
            row.Q<Label>("save-slot-autosave").EnableInClassList(UIStyleClasses.IsHidden, !item.IsAutosave);
        }
        public override void OnPush(object args)
        {
            if (args is SaveSlotsMode mode) viewModel.Mode = mode;
            if (!observing) { viewModel.propertyChanged += Changed; observing = true; }
            Refresh();
        }
        public override void OnPop()
        { if (observing) { viewModel.propertyChanged -= Changed; observing = false; } }
        void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        void Refresh()
        {
            bool saving = viewModel.Mode == SaveSlotsMode.Save;
            title.text = saving ? "Save game" : "Load game";
            empty.text = viewModel.EmptyText;
            empty.EnableInClassList(UIStyleClasses.IsHidden, viewModel.Slots.Count != 0);
            save.EnableInClassList(UIStyleClasses.IsHidden, !saving); create.EnableInClassList(UIStyleClasses.IsHidden, !saving);
            load.EnableInClassList(UIStyleClasses.IsHidden, saving);
            save.SetEnabled(viewModel.CanSave && viewModel.SelectedIndex >= 0);
            create.SetEnabled(viewModel.CanSave); load.SetEnabled(viewModel.CanLoad); delete.SetEnabled(viewModel.CanDelete);
            selection[0] = viewModel.SelectedIndex;
            list.SetSelectionWithoutNotify(viewModel.SelectedIndex >= 0 ? selection : Array.Empty<int>());
            list.RefreshItems();
        }
    }
}
