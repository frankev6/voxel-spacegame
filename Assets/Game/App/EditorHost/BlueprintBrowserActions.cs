using System;
using System.Collections.Generic;
using System.IO;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.App.EditorHost
{
    /// <summary>SPEC-51 EDT-020: browser command component for EDT-022's presenter; borrows all owners.</summary>
    internal sealed class BlueprintBrowserActions : IBlueprintBrowserCommands
    {
        readonly IBlueprintStore store;
        readonly EditSession session;
        readonly EditorContext context;
        readonly ShipEditorController controller;
        readonly BlueprintBrowserViewModel model;
        readonly Action close;
        readonly Action<string> status;
        readonly List<BlueprintInfo> entries = new();
        internal BlueprintBrowserActions(IBlueprintStore store, EditSession session, EditorContext context,
            ShipEditorController controller, BlueprintBrowserViewModel model, Action close, Action<string> status)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store)); this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.context = context ?? throw new ArgumentNullException(nameof(context)); this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.model = model ?? throw new ArgumentNullException(nameof(model)); this.close = close; this.status = status;
        }
        internal void Refresh()
        {
            string selected = Selected()?.BlueprintId;
            try { store.List(entries); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { status?.Invoke(e.Message); return; }
            model.Rows.Clear(); int selectedIndex = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i]; model.Rows.Add(new BlueprintRowViewModel { BlueprintId = entry.Id, Name = entry.DisplayName,
                    BlockCount = entry.BlockCount, ReadOnly = entry.ReadOnly });
                if (entry.Id == selected) selectedIndex = i;
            }
            Select(selectedIndex); model.NotifyRows();
        }
        BlueprintRowViewModel Selected() => model.SelectedIndex >= 0 && model.SelectedIndex < model.Rows.Count ? model.Rows[model.SelectedIndex] : null;
        public void Select(int index)
        {
            model.SelectedIndex = index >= 0 && index < model.Rows.Count ? index : -1;
            for (int i = 0; i < model.Rows.Count; i++) model.Rows[i].IsSelected = i == model.SelectedIndex;
            var selected = Selected(); model.CanLoad = selected != null; model.CanDelete = selected != null && !selected.ReadOnly;
        }
        bool Load(out VoxelStructure source)
        {
            source = null; var row = Selected(); if (row == null) return false;
            if (!store.TryLoad(row.BlueprintId, out source, out var error)) { status?.Invoke(error); return false; }
            if (source.CellSize == session.Draft.CellSize) return true;
            source.Dispose(); source = null; status?.Invoke("Blueprint cell size does not match this draft."); return false;
        }
        public void LoadStamp()
        {
            if (!Load(out var source)) return;
            using (source)
            {
                using var records = new NativeList<BlockRecord>(Allocator.TempJob); new DraftReader(source, context).Enumerate(records);
                if (records.Length == 0) { status?.Invoke("Blueprint is empty."); return; }
                controller.BeginStamp(source);
                if (controller.LastError != PlacementError.None) { status?.Invoke(controller.LastError.ToString()); return; }
            }
            close?.Invoke();
        }
        public void LoadReplace()
        {
            if (!Load(out var source)) return;
            using (source)
            using (var existing = new NativeList<BlockRecord>(Allocator.TempJob))
            using (var loaded = new NativeList<BlockRecord>(Allocator.TempJob))
            using (var batch = new EditBatch())
            {
                new DraftReader(session.Draft, context).Enumerate(existing); new DraftReader(source, context).Enumerate(loaded);
                int3 destination = Core(existing), origin = Core(loaded);
                foreach (var record in existing) batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = record });
                try
                {
                    foreach (var original in loaded)
                    {
                        var record = original; record.DamageOffset = -1;
                        for (int axis = 0; axis < 3; axis++) record.Origin[axis] = checked((int)((long)record.Origin[axis] + destination[axis] - origin[axis]));
                        batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = record });
                    }
                }
                catch (OverflowException) { status?.Invoke("Blueprint alignment is out of bounds."); return; }
                if (batch.Ops.Length == 0) { close?.Invoke(); return; }
                batch.Groups.Add(new BatchGroup { OpStart = 0, OpCount = batch.Ops.Length }); var report = new ApplyReport();
                // Stroke zero always creates a distinct undo command, independent of controller gestures.
                if (!session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Paste, "Load blueprint", report))
                { status?.Invoke(report.GroupErrors.Count > 0 ? report.GroupErrors[0].ToString() : "Blueprint could not be loaded."); return; }
            }
            close?.Invoke();
        }
        int3 Core(NativeList<BlockRecord> records)
        {
            bool found = false; int3 origin = default;
            foreach (var record in records)
            {
                if (!context.Definitions.Value.Blocks[record.Block].IsCore) continue;
                int3 p = record.Origin;
                if (!found || p.x < origin.x || p.x == origin.x && (p.y < origin.y || p.y == origin.y && p.z < origin.z)) { found = true; origin = p; }
            }
            return origin;
        }
        public void Delete()
        {
            var row = Selected(); if (row == null || row.ReadOnly) return;
            if (!store.TryDelete(row.BlueprintId, out var error)) { status?.Invoke(error); return; }
            Refresh();
        }
    }
}
