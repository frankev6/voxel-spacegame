using System;
using Game.Core;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-61 §5.6/HUD-011: fixed 200-slot cargo pool and captured-pointer drag/drop.</summary>
    public sealed class InventoryView : UIScreen
    {
        const int SlotCount = 200;
        readonly InventoryViewModel viewModel;
        readonly IInventoryCommands commands;
        readonly Slot[] slots = new Slot[SlotCount];
        readonly VisualElement[] rows = new VisualElement[20];
        readonly InventorySlotViewModel[] observed = new InventorySlotViewModel[SlotCount];
        readonly EventHandler<BindablePropertyChangedEventArgs>[] rowChanged = new EventHandler<BindablePropertyChangedEventArgs>[SlotCount];
        ScrollView grid;
        Label capacity, detailName, description, count, value, wallet;
        Button sort, stack;
        bool observing;
        Drag activeDrag;
        public InventoryView(InventoryViewModel vm, IInventoryCommands commands)
        { viewModel = vm ?? throw new ArgumentNullException(nameof(vm)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands)); }
        public override string Key => "inventory";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => false;
        public override bool IsRoot => false;
        public override VisualElement DefaultFocus => slots[0];
        protected override void OnBuild()
        {
            Root.dataSource = viewModel; grid = Q<ScrollView>("inventory-grid"); capacity = Q<Label>("inventory-capacity");
            detailName = Q<Label>("inventory-detail-name"); description = Q<Label>("inventory-detail-description");
            count = Q<Label>("inventory-detail-count"); value = Q<Label>("inventory-detail-value"); wallet = Q<Label>("inventory-wallet");
            sort = Q<Button>("inventory-sort"); stack = Q<Button>("inventory-stack-all");
            sort.clicked += () => { if (observing) commands.Sort(); }; stack.clicked += () => { if (observing) commands.StackAll(); };
            Q<Button>("inventory-close").clicked += () => { if (observing) Close(); };
            for (int i = 0; i < rows.Length; i++)
            { rows[i] = new VisualElement { name = "inventory-row-" + i, pickingMode = PickingMode.Ignore }; rows[i].AddToClassList("inventory-grid-row"); grid.Add(rows[i]); }
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i; var slot = slots[i] = new Slot { name = "inventory-slot-" + i, userData = i };
                rows[i / 10].Add(slot); slot.AddManipulator(new Drag(this, i));
                slot.RegisterCallback<NavigationSubmitEvent>(evt => { if (observing) commands.Select(index); evt.StopPropagation(); });
                slot.RegisterCallback<FocusInEvent>(_ => grid.ScrollTo(slot));
                rowChanged[i] = (_, _) => { RefreshSlot(index); RefreshRow(index / 10); };
            }
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root)) Stop(); });
            Root.RegisterCallback<AttachToPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root) && IsOpen) Start(); });
        }
        public override void OnPush(object args) => Start();
        public override void OnPop() => Stop();
        public override bool OnBack() { if (activeDrag == null) return false; activeDrag.Cancel(); return true; }
        void Start()
        { if (observing) return; observing = true; viewModel.propertyChanged += Changed; ObserveRows(); Refresh(); }
        void Stop()
        {
            activeDrag?.Cancel(); if (!observing) return; observing = false; viewModel.propertyChanged -= Changed;
            for (int i = 0; i < observed.Length; i++) { if (observed[i] != null) observed[i].propertyChanged -= rowChanged[i]; observed[i] = null; }
        }
        void ObserveRows()
        {
            for (int i = 0; i < observed.Length; i++)
            {
                var next = i < viewModel.Slots.Count ? viewModel.Slots[i] : null;
                if (ReferenceEquals(next, observed[i])) continue;
                if (observed[i] != null) observed[i].propertyChanged -= rowChanged[i]; observed[i] = next;
                if (next != null) next.propertyChanged += rowChanged[i];
            }
        }
        void Changed(object sender, BindablePropertyChangedEventArgs args)
        { if (args.propertyName == nameof(InventoryViewModel.Slots)) ObserveRows(); Refresh(); }
        void Refresh()
        {
            capacity.text = NumberStringCache.Get(viewModel.UsedSlots) + " / " + NumberStringCache.Get(viewModel.Capacity) +
                (viewModel.OverflowCount > 0 ? " (+" + NumberStringCache.Get(viewModel.OverflowCount) + " overflow)" : "");
            detailName.text = viewModel.SelectedName ?? ""; description.text = viewModel.SelectedDescription ?? "";
            count.text = viewModel.SelectedIndex >= 0 ? "Count · " + NumberStringCache.Get(viewModel.SelectedCount) : "";
            value.text = viewModel.SelectedIndex >= 0 ? "Value · " + NumberStringCache.Get(viewModel.SelectedValue) : "";
            wallet.text = "Credits · " + (viewModel.WalletText ?? "0"); sort.SetEnabled(viewModel.Capacity > 0); stack.SetEnabled(viewModel.Capacity > 0);
            for (int i = 0; i < slots.Length; i++) RefreshSlot(i);
            for (int i = 0; i < rows.Length; i++) RefreshRow(i);
        }
        bool Visible(int index) => index < viewModel.Slots.Count && (viewModel.Slots[index].InCapacity || !viewModel.Slots[index].IsEmpty);
        void RefreshSlot(int index)
        {
            var model = index < viewModel.Slots.Count ? viewModel.Slots[index] : null; var slot = slots[index];
            slot.EnableInClassList(UIStyleClasses.IsHidden, !Visible(index)); slot.dataSource = model;
            slot.SetIcon(model?.Icon); slot.SetCount(model?.Count ?? 0); slot.SetState(model?.IsSelected == true, false, false);
            slot.EnableInClassList(UIStyleClasses.IsEmpty, model?.IsEmpty != false);
            slot.EnableInClassList("slot--overflow", model != null && !model.InCapacity && !model.IsEmpty);
            if (activeDrag != null && activeDrag.Index == index && !activeDrag.Matches()) activeDrag.Cancel();
        }
        void RefreshRow(int row)
        { bool any = false; for (int i = row * 10; i < (row + 1) * 10; i++) any |= Visible(i); rows[row].EnableInClassList(UIStyleClasses.IsHidden, !any); }
        sealed class Drag : PointerManipulator
        {
            readonly InventoryView view;
            internal readonly int Index;
            int pointer = -1, sourceCount;
            string sourceId;
            internal Drag(InventoryView view, int index)
            { this.view = view; Index = index; activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse }); }
            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(Down); target.RegisterCallback<PointerUpEvent>(Up);
                target.RegisterCallback<PointerCancelEvent>(Cancelled); target.RegisterCallback<PointerCaptureOutEvent>(CaptureLost);
            }
            protected override void UnregisterCallbacksFromTarget()
            {
                Cancel(); target.UnregisterCallback<PointerDownEvent>(Down); target.UnregisterCallback<PointerUpEvent>(Up);
                target.UnregisterCallback<PointerCancelEvent>(Cancelled); target.UnregisterCallback<PointerCaptureOutEvent>(CaptureLost);
            }
            void Down(PointerDownEvent evt)
            {
                if (!view.observing || !CanStartManipulation(evt) || !view.Visible(Index)) return;
                view.commands.Select(Index); var model = view.viewModel.Slots[Index];
                if (model.IsEmpty) { evt.StopPropagation(); return; }
                view.activeDrag?.Cancel(); sourceId = model.ItemId; sourceCount = model.Count; pointer = evt.pointerId;
                view.activeDrag = this; target.CapturePointer(pointer); target.AddToClassList("inventory-slot--dragging"); evt.StopPropagation();
            }
            internal bool Matches()
            { var model = view.viewModel.Slots[Index]; return !model.IsEmpty && model.ItemId == sourceId && model.Count == sourceCount; }
            void Up(PointerUpEvent evt)
            {
                if (evt.pointerId != pointer || !CanStopManipulation(evt)) return;
                var picked = target.panel?.Pick(new Vector2(evt.position.x, evt.position.y));
                var destination = picked as Slot ?? picked?.GetFirstAncestorOfType<Slot>();
                bool valid = view.observing && Matches() && destination?.userData is int index &&
                    index != Index && index >= 0 && index < view.slots.Length && ReferenceEquals(destination, view.slots[index]) && view.Visible(index);
                int to = valid ? (int)destination.userData : -1; Cancel();
                if (valid) view.commands.Move(Index, to); evt.StopPropagation();
            }
            void Cancelled(PointerCancelEvent evt) { if (evt.pointerId == pointer) Cancel(); }
            void CaptureLost(PointerCaptureOutEvent evt) { if (evt.pointerId == pointer) Cancel(); }
            internal void Cancel()
            {
                int captured = pointer; pointer = -1; sourceId = null;
                if (ReferenceEquals(view.activeDrag, this)) view.activeDrag = null;
                target.RemoveFromClassList("inventory-slot--dragging");
                if (captured >= 0 && target.HasPointerCapture(captured)) target.ReleasePointer(captured);
            }
        }
    }
}
