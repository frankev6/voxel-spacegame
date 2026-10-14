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
        VisualElement ghost;   // QA-001: follows the pointer while dragging (not a Slot: the pool stays 200)
        Image ghostIcon; Label ghostCount;
        int dropHover = -1;
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
                // QA-001 conveniences: right-click splits a stack, shift-click gathers that item; hover shows the item.
                slot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (!observing || !Visible(index)) return;
                    if (evt.button == (int)MouseButton.RightMouse) { commands.Split(index); evt.StopPropagation(); }
                    else if (evt.button == (int)MouseButton.LeftMouse && evt.shiftKey) { commands.Gather(index); evt.StopPropagation(); }
                }, TrickleDown.TrickleDown);
                slot.RegisterCallback<PointerEnterEvent>(_ => { if (observing && activeDrag == null && index < viewModel.Slots.Count && !viewModel.Slots[index].IsEmpty) slot.tooltip = Tooltip(index); });
                rowChanged[i] = (_, _) => { RefreshSlot(index); RefreshRow(index / 10); };
            }
            ghost = new VisualElement { name = "inventory-drag-ghost", pickingMode = PickingMode.Ignore };
            ghost.AddToClassList("slot"); ghost.AddToClassList("inventory-drag-ghost"); ghost.AddToClassList(UIStyleClasses.IsHidden);
            ghostIcon = new Image { pickingMode = PickingMode.Ignore }; ghostIcon.AddToClassList("slot__icon"); ghost.Add(ghostIcon);
            ghostCount = new Label { pickingMode = PickingMode.Ignore }; ghostCount.AddToClassList("slot__count"); ghost.Add(ghostCount);
            Root.Add(ghost);
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
        string Tooltip(int index)
        {
            var model = viewModel.Slots[index];
            return model.Name + "  ×" + NumberStringCache.Get(model.Count) + "\nLeft-drag move · Right-click split · Shift-click gather";
        }
        void MoveGhost(Vector2 panelPosition)
        {
            ghost.style.left = panelPosition.x - Root.worldBound.x + 10; ghost.style.top = panelPosition.y - Root.worldBound.y + 10;
        }
        void HoverDrop(Vector2 panelPosition)
        {
            var picked = Root.panel?.Pick(panelPosition);
            var slot = picked as Slot ?? picked?.GetFirstAncestorOfType<Slot>();
            int index = slot != null && slot.userData is int i ? i : -1;
            if (index == dropHover) return;
            if (dropHover >= 0) slots[dropHover].RemoveFromClassList("inventory-slot--drop");
            dropHover = activeDrag != null && index != activeDrag.Index ? index : -1;
            if (dropHover >= 0) slots[dropHover].AddToClassList("inventory-slot--drop");
        }
        void EndDragPresentation()
        {
            ghost?.AddToClassList(UIStyleClasses.IsHidden);
            if (dropHover >= 0) slots[dropHover].RemoveFromClassList("inventory-slot--drop"); dropHover = -1;
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
                target.RegisterCallback<PointerDownEvent>(Down); target.RegisterCallback<PointerUpEvent>(Up); target.RegisterCallback<PointerMoveEvent>(Move);
                target.RegisterCallback<PointerCancelEvent>(Cancelled); target.RegisterCallback<PointerCaptureOutEvent>(CaptureLost);
            }
            protected override void UnregisterCallbacksFromTarget()
            {
                Cancel(); target.UnregisterCallback<PointerDownEvent>(Down); target.UnregisterCallback<PointerUpEvent>(Up); target.UnregisterCallback<PointerMoveEvent>(Move);
                target.UnregisterCallback<PointerCancelEvent>(Cancelled); target.UnregisterCallback<PointerCaptureOutEvent>(CaptureLost);
            }
            void Down(PointerDownEvent evt)
            {
                if (!view.observing || !CanStartManipulation(evt) || evt.shiftKey || !view.Visible(Index)) return;
                view.commands.Select(Index); var model = view.viewModel.Slots[Index];
                if (model.IsEmpty) { evt.StopPropagation(); return; }
                view.activeDrag?.Cancel(); sourceId = model.ItemId; sourceCount = model.Count; pointer = evt.pointerId;
                view.activeDrag = this; target.CapturePointer(pointer); target.AddToClassList("inventory-slot--dragging"); evt.StopPropagation();
                view.ghostIcon.sprite = model.Icon; view.ghostCount.text = model.Count > 1 ? NumberStringCache.Get(model.Count) : ""; view.MoveGhost(evt.position);
                view.ghost.RemoveFromClassList(UIStyleClasses.IsHidden); view.ghost.BringToFront();
            }
            void Move(PointerMoveEvent evt)
            {
                if (evt.pointerId != pointer || !ReferenceEquals(view.activeDrag, this)) return;
                view.MoveGhost(evt.position); view.HoverDrop(evt.position);
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
                target.RemoveFromClassList("inventory-slot--dragging"); view.EndDragPresentation();
                if (captured >= 0 && target.HasPointerCapture(captured)) target.ReleasePointer(captured);
            }
        }
    }
}
