using System;
using System.Collections.Generic;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-51 §5.14: authored fragments with bounded, pre-created rows. The host owns the four regions.</summary>
    public static class ShipEditorPanels
    {
        public static void Mount(VisualElement top, VisualElement left, VisualElement right, VisualElement bottom,
            ShipEditorViewModel vm, IShipEditorCommands commands, UIRoot ui)
        {
            if (top == null || left == null || right == null || bottom == null || vm == null || commands == null || ui == null)
                throw new ArgumentNullException("Editor panels require four regions, model, commands and UI root.");
            if (top.Q("editor-toolbar") != null) throw new InvalidOperationException("Editor panels are already mounted.");
            var toolbar = ui.CloneFragment("editor-toolbar-fragment"); var palette = ui.CloneFragment("editor-palette-fragment");
            var stats = ui.CloneFragment("editor-stats-fragment"); var footer = ui.CloneFragment("editor-footer-fragment");
            var binding = new Binding(toolbar, palette, stats, footer, vm, commands, ui);
            top.Add(toolbar); left.Add(palette); right.Add(stats); bottom.Add(footer);
            binding.Start();
        }
        sealed class Page
        {
            internal readonly ScrollView Scroll;
            readonly VisualElement pager;
            readonly Button previous, next;
            readonly Label label;
            internal readonly int Capacity;
            internal int Index;
            int lastIndex = -1, lastPages = -1;
            internal int Offset => Index * Capacity;
            internal Page(VisualElement root, string id, int capacity, Action refresh)
            {
                Capacity = capacity; Scroll = root.Q<ScrollView>(id); pager = root.Q(id + "-pages");
                previous = new Button(() => { if (Index > 0) { Index--; refresh(); Scroll.scrollOffset = Vector2.zero; } })
                    { name = id + "-previous", text = "Previous" };
                next = new Button(() => { Index++; refresh(); Scroll.scrollOffset = Vector2.zero; }) { name = id + "-next", text = "Next" };
                previous.AddToClassList("btn"); previous.AddToClassList("btn--small"); next.AddToClassList("btn"); next.AddToClassList("btn--small");
                label = new Label { name = id + "-page", pickingMode = PickingMode.Ignore }; label.AddToClassList("editor-page-label");
                pager.Add(previous); pager.Add(label); pager.Add(next);
            }
            internal void Refresh(int count)
            {
                int pages = Math.Max(1, (count + Capacity - 1) / Capacity); Index = Math.Min(Index, pages - 1);
                Show(pager, pages > 1); previous.SetEnabled(Index > 0); next.SetEnabled(Index + 1 < pages);
                if (lastIndex != Index || lastPages != pages) { label.text = N(Index + 1) + " / " + N(pages); lastIndex = Index; lastPages = pages; }
            }
        }
        sealed class PaletteRow
        {
            internal readonly Slot Slot;
            internal readonly Label Count;
            internal EditorPaletteItemViewModel Model;
            internal string Id;
            internal PaletteRow(int index)
            {
                Slot = new Slot { name = "editor-palette-pool-" + index };
                Count = Slot.Q<Label>(className: UIStyleClasses.SlotCount);
            }
        }
        sealed class StatRow
        {
            internal readonly VisualElement Root;
            internal readonly Label Label, Value, Delta;
            internal string Id;
            internal StatRow(int index)
            {
                Root = new VisualElement { name = "editor-stat-pool-" + index }; Root.AddToClassList("editor-stat-row");
                Label = Text(Root, "editor-stat-label-" + index, "editor-stat-label");
                Value = Text(Root, "editor-stat-value-" + index, "editor-stat-value");
                Delta = Text(Root, "editor-stat-delta-" + index, "editor-stat-delta");
            }
        }
        sealed class BomRow
        {
            internal readonly VisualElement Root;
            internal readonly Label Name;
            internal readonly Label[] Counts = new Label[5];
            internal EditorBomRowViewModel Model;
            internal string Id;
            readonly int[] values = { int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue };
            internal void Number(int column, int value, string prefix) { if (values[column] == value) return; values[column] = value; Counts[column].text = prefix + N(value); }
            internal BomRow(int index)
            {
                Root = new VisualElement { name = "editor-bom-pool-" + index }; Root.AddToClassList("editor-bom-row");
                Name = Text(Root, "editor-bom-name-" + index, "text-s");
                var numbers = new VisualElement(); numbers.AddToClassList("editor-bom-counts"); Root.Add(numbers);
                for (int i = 0; i < Counts.Length; i++) Counts[i] = Text(numbers, "editor-bom-count-" + index + "-" + i, "editor-bom-count");
            }
        }
        sealed class IssueRow
        {
            internal readonly Button Button;
            internal EditorIssueRowViewModel Model;
            internal int Index = int.MinValue;
            internal IssueRow(int index)
            { Button = new Button { name = "editor-issue-pool-" + index }; Button.AddToClassList("btn"); Button.AddToClassList("btn--small"); Button.AddToClassList(UIStyleClasses.TextEllipsis); }
        }
        sealed class Binding
        {
            readonly VisualElement top, left, right, bottom;
            readonly ShipEditorViewModel vm;
            readonly IShipEditorCommands commands;
            readonly UIRoot ui;
            readonly PaletteRow[] slots = new PaletteRow[128];
            readonly StatRow[] stats = new StatRow[12];
            readonly BomRow[] bom = new BomRow[64];
            readonly IssueRow[] issues = new IssueRow[16];
            readonly Button[] colors = new Button[16], categories = new Button[12], tools = new Button[9];
            readonly Slot[] hotbar = new Slot[9];   // QA-001 creative hotbar (number keys 1–9)
            Button blocksToggle, detailsToggle;
            int hotbarVersion = -1;
            readonly Page palettePage, statsPage, bomPage, issuesPage;
            readonly TabBar tabs;
            readonly TextField search;
            readonly List<ViewModelBase> observed = new(256);
            readonly List<(VisualElement Element, Action<TooltipContent> Fill)> tips = new(256);
            bool active, refreshing;
            int hintTool = -1, errorCount = -1, warningCount = -1;
            internal Binding(VisualElement top, VisualElement left, VisualElement right, VisualElement bottom,
                ShipEditorViewModel vm, IShipEditorCommands commands, UIRoot ui)
            {
                this.top = top; this.left = left; this.right = right; this.bottom = bottom; this.vm = vm; this.commands = commands; this.ui = ui;
                foreach (var region in new[] { top, left, right, bottom }) { region.AddToClassList("editor-fragment"); region.pickingMode = PickingMode.Ignore; region.dataSource = vm; }
                palettePage = new Page(left, "editor-palette-grid", slots.Length, Refresh);
                statsPage = new Page(right, "editor-stats", stats.Length, Refresh);
                bomPage = new Page(right, "editor-bom", bom.Length, Refresh);
                issuesPage = new Page(right, "editor-issues", issues.Length, Refresh);
                tabs = left.Q<TabBar>("editor-palette-tabs"); search = left.Q<TextField>("editor-palette-search");
                var labels = new string[categories.Length]; var names = new string[categories.Length];
                for (int i = 0; i < names.Length; i++) { labels[i] = ""; names[i] = "editor-palette-category-" + i; }
                tabs.SetTabs(labels, names);
                for (int i = 0; i < categories.Length; i++) categories[i] = tabs.Q<Button>(names[i]);
                tabs.SelectionChanged += index => { if (active && !refreshing && index >= 0) commands.SelectCategory(index); };
                search.RegisterValueChangedCallback(evt => { if (active && !refreshing) commands.SetSearch(evt.newValue); });
                for (int i = 0; i < slots.Length; i++)
                {
                    var row = slots[i] = new PaletteRow(i); palettePage.Scroll.Add(row.Slot);
                    void Select() { if (active && row.Model != null && !row.Model.IsLocked) { commands.SelectBlock(row.Model.BlockId); ui.PlaySound(UISound.Click); } }
                    row.Slot.RegisterCallback<ClickEvent>(_ => Select());
                    row.Slot.RegisterCallback<NavigationSubmitEvent>(evt => { Select(); evt.StopPropagation(); });
                    Tip(row.Slot, content => { content.Title = row.Model?.DisplayName; content.Body = row.Model?.LockReason; });
                }
                for (int i = 0; i < colors.Length; i++)
                {
                    int index = i; var color = colors[i] = new Button(() => { if (active && vm.Palette != null && index < vm.Palette.Colors.Count) commands.SelectColor(vm.Palette.Colors[index].Index); })
                        { name = "editor-color-" + i, text = N(i) };
                    color.AddToClassList("editor-swatch"); left.Q("editor-colors").Add(color);
                }
                for (int i = 0; i < stats.Length; i++) { var row = stats[i] = new StatRow(i); statsPage.Scroll.Add(row.Root); }
                for (int i = 0; i < bom.Length; i++)
                {
                    var row = bom[i] = new BomRow(i); bomPage.Scroll.Add(row.Root);
                    Tip(row.Root, content => { content.Title = row.Model?.Name; content.Body = row.Model == null ? "" : "Required " + N(row.Model.Required) + ", refunded " + N(row.Model.Refunded) + ", available " + N(row.Model.Available) + ", shortfall " + N(row.Model.Shortfall) + ", damaged lost " + N(row.Model.LostDamaged); });
                }
                for (int i = 0; i < issues.Length; i++)
                {
                    var row = issues[i] = new IssueRow(i); issuesPage.Scroll.Add(row.Button);
                    row.Button.clicked += () => { if (active && row.Model != null) commands.FocusIssue(row.Model.Index); };
                    Tip(row.Button, content => content.Body = row.Model?.Text);
                }
                string[] namesTools = { "place", "line", "box", "fill", "remove", "replace", "paint", "eyedropper", "select" };
                for (int i = 0; i < tools.Length; i++) { int id = i + 1; tools[i] = Button(top, "editor-tool-" + namesTools[i], () => commands.SelectTool(id)); }
                var undo = Button(top, "editor-undo", commands.Undo); Tip(undo, content => content.Title = vm.UndoLabel ?? "Undo");
                var redo = Button(top, "editor-redo", commands.Redo); Tip(redo, content => content.Title = vm.RedoLabel ?? "Redo");
                for (int i = 0; i < 3; i++)
                {
                    int axis = i; string letter = "xyz"[i].ToString();
                    Button(top, "editor-mirror-" + letter, () => commands.ToggleMirror(axis));
                    Button(top, "editor-mirror-" + letter + "-minus", () => commands.NudgeMirrorPlane(axis, -1));
                    Button(top, "editor-mirror-" + letter + "-plus", () => commands.NudgeMirrorPlane(axis, 1));
                }
                Button(top, "editor-slice-toggle", commands.ToggleSlice); Button(top, "editor-slice-down", () => commands.StepSlice(-1));
                Button(top, "editor-slice-up", () => commands.StepSlice(1)); Button(top, "editor-xray", commands.ToggleXRay); Button(top, "editor-box-mode", commands.CycleBoxMode);
                Button(top, "editor-save-blueprint", commands.SaveBlueprint); Button(top, "editor-load-blueprint", commands.OpenBlueprintBrowser);
                Button(top, "editor-settings", commands.OpenSettings); Button(top, "editor-leave", commands.RequestLeave);
                Button(right, "editor-remove-floating", commands.RemoveFloatingParts); Button(right, "editor-revert", commands.RevertAll);
                var commit = Button(right, "editor-commit", commands.RequestCommit); Tip(commit, content => { content.Title = vm.CommitLabel; content.Body = vm.CommitBlockedReason; });
                BuildCreativeLayout();
                top.RegisterCallback<AttachToPanelEvent>(evt => { if (ReferenceEquals(evt.target, top)) Connect(); });
                top.RegisterCallback<DetachFromPanelEvent>(evt => { if (ReferenceEquals(evt.target, top)) Disconnect(); });
            }
            // QA-001 (owner: "looks like an excel spreadsheet"): a block hotbar and paint strip along the bottom, the block
            // catalogue and the full stats/BOM/issues as drawers, and a slim toolbar whose secondary controls appear in context.
            void BuildCreativeLayout()
            {
                var bar = new VisualElement { name = "editor-hotbar", pickingMode = PickingMode.Ignore }; bar.AddToClassList("editor-hotbar");
                for (int i = 0; i < hotbar.Length; i++)
                {
                    int index = i; var slot = hotbar[i] = new Slot { name = "editor-hotbar-" + i }; slot.SetKeyHint(N(i + 1));
                    slot.RegisterCallback<ClickEvent>(_ => { if (active) { commands.SelectHotbar(index); ui.PlaySound(UISound.Click); } });
                    Tip(slot, content => { content.Title = vm.HotbarNames[index] ?? "Empty"; content.Body = "Key " + (index + 1) + " · Tab opens the block catalogue; picking a block fills this slot"; });
                    bar.Add(slot);
                }
                var paint = left.Q("editor-colors"); paint.AddToClassList("editor-colors--compact"); bar.Add(paint);
                foreach (var swatch in colors) swatch.text = "";   // colour, not an index number
                bottom.Insert(0, bar); bottom.AddToClassList("editor-bottom--creative");
                var row = top.Q(className: "editor-toolbar-row");
                blocksToggle = new Button(() => { if (active) commands.TogglePalette(); }) { name = "editor-blocks-toggle", text = "Blocks · Tab" };
                detailsToggle = new Button(() => { if (active) commands.ToggleDetails(); }) { name = "editor-details-toggle", text = "Details" };
                foreach (var toggle in new[] { blocksToggle, detailsToggle }) { toggle.AddToClassList("btn"); toggle.AddToClassList("btn--small"); toggle.AddToClassList("editor-tool"); }
                row.Insert(0, blocksToggle); row.Add(detailsToggle);
                Tip(blocksToggle, content => content.Title = "Block catalogue (Tab)");
                Tip(detailsToggle, content => content.Title = "Full stats, bill of materials and issues");
            }
            [Game.Core.StaticStateOk("constant stat ids, never written")] static readonly string[] KeyStats = { "Mass kg", "Thrust / weight", "Power generation", "Power use", "DPS", "Cost" };
            Button Button(VisualElement region, string name, Action action)
            { var button = region.Q<Button>(name) ?? throw new InvalidOperationException("Editor panel missing " + name); button.clicked += () => { if (active) action(); }; return button; }
            void Tip(VisualElement element, Action<TooltipContent> fill) => tips.Add((element, fill));
            internal void Start() { Refresh(); if (top.panel != null) Connect(); }
            void Connect()
            {
                if (active) return; active = true; Refresh();
                foreach (var tip in tips) ui.Tooltips.Attach(tip.Element, tip.Fill);
            }
            void Disconnect()
            {
                if (!active) return; active = false; Unobserve();
                foreach (var tip in tips)
                    try { ui.Tooltips.Detach(tip.Element); } catch (ObjectDisposedException) { break; /* UIRoot already released its tooltip registry. */ }
            }
            void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh();
            void Unobserve() { foreach (var model in observed) model.propertyChanged -= Changed; observed.Clear(); }
            void Observe(ViewModelBase model)
            { if (!active || model == null || observed.Contains(model)) return; observed.Add(model); model.propertyChanged += Changed; }
            void RefreshCreative()
            {
                if (hotbarVersion != vm.HotbarVersion) { hotbarVersion = vm.HotbarVersion; for (int i = 0; i < hotbar.Length; i++) hotbar[i].SetIcon(vm.HotbarIcons[i]); }
                for (int i = 0; i < hotbar.Length; i++) hotbar[i].SetState(i == vm.HotbarIndex, false, false);
                // left/right are the mounted fragments; their parents are the host's region plates.
                Show(left, vm.PaletteOpen); if (left.parent != null) Show(left.parent, vm.PaletteOpen);
                blocksToggle.EnableInClassList(UIStyleClasses.IsSelected, vm.PaletteOpen);
                detailsToggle.EnableInClassList(UIStyleClasses.IsSelected, vm.DetailsOpen);
                // Compact: six key stats with deltas and the issue/commit lines; Details adds every stat, the BOM and the issue list.
                foreach (var name in new[] { "editor-bom-heading", "editor-bom", "editor-bom-pages", "editor-issues", "editor-issues-pages", "editor-stats-pages" })
                    Show(right.Q(name), vm.DetailsOpen);
                right.EnableInClassList("editor-details--open", vm.DetailsOpen); right.parent?.EnableInClassList("editor-details--open", vm.DetailsOpen);
                Show(right.Q("editor-remove-floating"), vm.Issues?.CanRemoveFloating == true);
                for (int i = 0; i < stats.Length; i++)
                    if (!vm.DetailsOpen && stats[i].Id != null && Array.IndexOf(KeyStats, stats[i].Id) < 0) Show(stats[i].Root, false);
                // Secondary controls only when their mode is on.
                for (int i = 0; i < 3; i++)
                {
                    bool on = i == 0 ? vm.MirrorX : i == 1 ? vm.MirrorY : vm.MirrorZ; string letter = "xyz"[i].ToString();
                    Show(top.Q("editor-mirror-" + letter + "-minus"), on); Show(top.Q("editor-mirror-" + letter + "-plus"), on);
                }
                Show(top.Q("editor-slice-down"), vm.SliceEnabled); Show(top.Q("editor-slice-up"), vm.SliceEnabled);
                Show(top.Q("editor-box-mode"), vm.ActiveTool == 3);
            }
            internal void Refresh()
            {
                if (refreshing) return; refreshing = true;
                try
                {
                    Unobserve(); Observe(vm); Observe(vm.Palette); Observe(vm.Stats); Observe(vm.Bom); Observe(vm.Issues);
                    var palette = vm.Palette; palettePage.Refresh(palette?.Items.Count ?? 0);
                    for (int i = 0; i < categories.Length; i++)
                    { bool exists = palette != null && i < palette.Categories.Count; Show(categories[i], exists); categories[i].text = exists ? palette.Categories[i] : ""; }
                    tabs.SelectedIndex = palette != null && palette.Categories.Count > 0 ? Math.Min(Math.Max(0, palette.SelectedCategory), categories.Length - 1) : -1;
                    search.SetValueWithoutNotify(palette?.SearchText ?? "");
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var row = slots[i]; int index = palettePage.Offset + i; row.Model = palette != null && index < palette.Items.Count ? palette.Items[index] : null;
                        var model = row.Model; Observe(model); Show(row.Slot, model != null); row.Slot.dataSource = model;
                        if (row.Id != model?.BlockId) { row.Id = model?.BlockId; row.Slot.name = model == null ? "editor-palette-pool-" + i : "editor-palette-" + model.BlockId; }
                        row.Slot.SetIcon(model?.Icon); row.Slot.SetCount(model?.Available ?? 0);
                        row.Count.text = model?.ShowCount == true ? N(model.Available) : ""; Show(row.Count, model?.ShowCount == true);
                        row.Slot.SetState(model != null && (model.IsSelected || model.BlockId == palette.SelectedBlockId), false, model?.IsLocked == true);
                    }
                    for (int i = 0; i < colors.Length; i++)
                    {
                        var model = palette != null && i < palette.Colors.Count ? palette.Colors[i] : null; Observe(model); Show(colors[i], model != null);
                        if (model != null) { colors[i].style.backgroundColor = model.Swatch; colors[i].EnableInClassList(UIStyleClasses.IsSelected, model.IsSelected || model.Index == palette.SelectedColor); }
                    }
                    statsPage.Refresh(vm.Stats?.Rows.Count ?? 0);
                    for (int i = 0; i < stats.Length; i++)
                    {
                        int index = statsPage.Offset + i; var model = vm.Stats != null && index < vm.Stats.Rows.Count ? vm.Stats.Rows[index] : null;
                        var row = stats[i]; Observe(model); Show(row.Root, model != null); row.Root.dataSource = model;
                        if (row.Id != model?.StatId) { row.Id = model?.StatId; row.Root.name = model == null ? "editor-stat-pool-" + i : "editor-stat-" + model.StatId; }
                        row.Label.text = model?.Label ?? ""; row.Value.text = model?.Value ?? ""; row.Delta.text = model?.Delta ?? "";
                        row.Delta.EnableInClassList("editor-delta--better", model?.DeltaSign > 0); row.Delta.EnableInClassList("editor-delta--worse", model?.DeltaSign < 0);
                    }
                    bomPage.Refresh(vm.Bom?.Rows.Count ?? 0);
                    for (int i = 0; i < bom.Length; i++)
                    {
                        var row = bom[i]; int index = bomPage.Offset + i; row.Model = vm.Bom != null && index < vm.Bom.Rows.Count ? vm.Bom.Rows[index] : null;
                        var model = row.Model; Observe(model); Show(row.Root, model != null); row.Root.dataSource = model;
                        if (row.Id != model?.ItemId) { row.Id = model?.ItemId; row.Root.name = model == null ? "editor-bom-pool-" + i : "editor-bom-" + model.ItemId; } row.Name.text = model?.Name ?? "";
                        row.Number(0, model?.Required ?? 0, "Use "); row.Number(1, model?.Refunded ?? 0, "+");
                        row.Number(2, model?.Available ?? 0, "Have "); row.Number(3, model?.Shortfall ?? 0, "Need "); row.Number(4, model?.LostDamaged ?? 0, "Lost ");
                        row.Counts[3].EnableInClassList("editor-shortfall", model?.Shortfall > 0);
                    }
                    right.Q<Label>("editor-bom-summary").text = vm.Bom?.SummaryText ?? "";
                    issuesPage.Refresh(vm.Issues?.Rows.Count ?? 0);
                    for (int i = 0; i < issues.Length; i++)
                    {
                        var row = issues[i]; int index = issuesPage.Offset + i; row.Model = vm.Issues != null && index < vm.Issues.Rows.Count ? vm.Issues.Rows[index] : null;
                        var model = row.Model; Observe(model); Show(row.Button, model != null); row.Button.dataSource = model;
                        if (row.Index != (model?.Index ?? int.MinValue)) { row.Index = model?.Index ?? int.MinValue; row.Button.name = model == null ? "editor-issue-pool-" + i : "editor-issue-" + model.Index; } row.Button.text = model?.Text ?? "";
                        row.Button.EnableInClassList("editor-error", model?.IsError == true); row.Button.EnableInClassList("editor-warning", model != null && !model.IsError);
                    }
                    if (errorCount != (vm.Issues?.ErrorCount ?? 0) || warningCount != (vm.Issues?.WarningCount ?? 0))
                    { errorCount = vm.Issues?.ErrorCount ?? 0; warningCount = vm.Issues?.WarningCount ?? 0; right.Q<Label>("editor-issues-summary").text = "ISSUES · " + N(errorCount) + " errors · " + N(warningCount) + " warnings"; }
                    right.Q<Button>("editor-remove-floating").SetEnabled(vm.Issues?.CanRemoveFloating == true); right.Q<Button>("editor-revert").SetEnabled(vm.IsDirty);
                    var commit = right.Q<Button>("editor-commit"); commit.SetEnabled(vm.CanCommit); commit.text = vm.CommitLabel ?? (vm.IsCreative ? "Save blueprint" : "Commit");
                    top.Q<Button>("editor-undo").SetEnabled(vm.CanUndo); top.Q<Button>("editor-redo").SetEnabled(vm.CanRedo);
                    for (int i = 0; i < tools.Length; i++) tools[i].EnableInClassList(UIStyleClasses.IsSelected, vm.ActiveTool == i + 1);
                    top.Q<Button>("editor-mirror-x").EnableInClassList(UIStyleClasses.IsSelected, vm.MirrorX);
                    top.Q<Button>("editor-mirror-y").EnableInClassList(UIStyleClasses.IsSelected, vm.MirrorY);
                    top.Q<Button>("editor-mirror-z").EnableInClassList(UIStyleClasses.IsSelected, vm.MirrorZ);
                    top.Q<Button>("editor-slice-toggle").EnableInClassList(UIStyleClasses.IsSelected, vm.SliceEnabled);
                    top.Q<Button>("editor-slice-up").SetEnabled(vm.SliceEnabled); top.Q<Button>("editor-slice-down").SetEnabled(vm.SliceEnabled);
                    top.Q<Label>("editor-slice-level").text = vm.SliceEnabled ? N(vm.SliceLevel) : "";
                    top.Q<Button>("editor-xray").EnableInClassList(UIStyleClasses.IsSelected, vm.XRay);
                    top.Q<Button>("editor-box-mode").text = vm.BoxMode == 1 ? "Hollow" : vm.BoxMode == 2 ? "Frame" : "Solid";
                    top.Q<Label>("editor-orientation").text = vm.OrientationText ?? "";
                    bottom.Q<Label>("editor-hover-info").text = vm.HoverText ?? ""; bottom.Q<Label>("editor-status").text = vm.StatusText ?? "";
                    RefreshCreative();
                    if (hintTool != vm.ActiveTool)
                    {
                        hintTool = vm.ActiveTool;
                        bottom.Q<KeyHint>("editor-key-primary").text = vm.ActiveTool == 5 ? "Primary · Remove" : vm.ActiveTool == 7 ? "Primary · Paint" : vm.ActiveTool == 8 ? "Primary · Pick" : vm.ActiveTool == 9 ? "Primary · Select" : vm.ActiveTool == 10 ? "Primary · Paste" : "Primary · Place";
                        bottom.Q<KeyHint>("editor-key-secondary").text = "Secondary · Remove";
                        bottom.Q<KeyHint>("editor-key-cancel").text = "Back · Cancel / Done";
                    }
                }
                finally { refreshing = false; }
            }
        }
        static string N(int value) => NumberStringCache.Get(value);
        static void Show(VisualElement element, bool visible) => element.EnableInClassList(UIStyleClasses.IsHidden, !visible);
        static Label Text(VisualElement parent, string name, string style)
        {
            var label = new Label { name = name, pickingMode = PickingMode.Ignore };
            label.AddToClassList(style); label.AddToClassList(UIStyleClasses.TextEllipsis); parent.Add(label); return label;
        }
    }
}
