using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.ShipEditor.Validation;
using Unity.Collections;

namespace Game.ShipEditor.Tools
{
    /// <summary>Reusable edit/preview workspace, owned by the controller. Session and content are borrowed.</summary>
    internal sealed class ToolContext : IDisposable
    {
        internal readonly EditSession Session;
        internal readonly EditorContext Content;
        internal readonly ApplyReport Report = new();
        internal readonly EditBatch Batch = new();
        internal readonly SymmetryMapper Mapper;
        internal readonly Selection Selection;
        readonly ValidationScratch previewScratch = new();
        readonly PlacementValidator previewValidator;
        NativeList<BlockRecord> copies = new(Allocator.Persistent);
        internal DraftPicker.Hit Hover;
        internal UnityEngine.Ray GridRay;
        internal SymmetrySettings Symmetry;
        internal ushort SelectedBlock;
        internal byte Orient, Color;
        internal BoxMode BoxMode;
        internal string StatusText { get; private set; } = "";
        internal int StrokeId { get; private set; }
        internal PlacementError Error { get; private set; }
        internal ToolContext(EditSession session, EditorContext content)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Mapper = new SymmetryMapper(content); Selection = new Selection(session, content);
            previewValidator = new PlacementValidator(content, session.Placement, previewScratch);
        }
        internal void SetError(PlacementError error) => Error = error;
        internal void SetStatus(string text, PlacementError error = PlacementError.None)
        { StatusText = text ?? ""; Error = error; }
        internal bool AllowsArea()
        {
            if (SelectedBlock != 0 && Content.Footprints.Get(SelectedBlock, Orient).Cells.Length == 1) return true;
            SetStatus("This tool requires a single-cell block.", PlacementError.MultiCellNotAllowed); return false;
        }
        bool PrepareArea(NativeArray<Unity.Mathematics.int3> cells, Unity.Mathematics.int3 normal)
        {
            Batch.Clear(); copies.Clear(); SetStatus("");
            if (!AllowsArea()) return false;
            foreach (var target in cells)
            {
                copies.Clear(); var footprint = Content.Footprints.Get(SelectedBlock, Orient);
                var record = new BlockRecord { Block = SelectedBlock, Orient = Orient, Color = Color, DamageOffset = -1,
                    Origin = PlacementMath.OriginFor(target, normal, footprint.Min, footprint.Max) };
                var error = Mapper.BuildCopies(record, default, Symmetry, copies, Batch.Damage);
                if (error != PlacementError.None) { Error = error; continue; }
                int start = Batch.Ops.Length;
                foreach (var copy in copies) Batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = copy });
                if (Batch.Ops.Length > Content.Config.MaxBatchCells)
                { Batch.Clear(); SetStatus("Region too large", PlacementError.OverBudget); return false; }
                Batch.Groups.Add(new BatchGroup { OpStart = start, OpCount = Batch.Ops.Length - start });
            }
            return Batch.Ops.Length != 0;
        }
        internal bool ApplyArea(NativeArray<Unity.Mathematics.int3> cells, Unity.Mathematics.int3 normal, ToolId tool, string label)
        {
            if (!PrepareArea(cells, normal)) return false;
            var mappingError = Error;
            bool changed = Session.TryApply(Batch, AtomicityMode.PerGroup, StrokeId, tool, label, Report);
            Error = mappingError;
            foreach (var error in Report.GroupErrors) if (error != PlacementError.None) { Error = error; break; }
            return changed;
        }
        internal void PreviewArea(NativeArray<Unity.Mathematics.int3> cells, Unity.Mathematics.int3 normal, PreviewBuffer preview)
        {
            if (!PrepareArea(cells, normal)) { preview.Clear(); preview.SetMessage(StatusText, Error); return; }
            var mappingError = Error; Report.Clear();
            previewValidator.Validate(Session.Draft, Batch, Session.OriginalCounts, Session.Options.Creative, AtomicityMode.PerGroup, Report);
            Error = mappingError;
            foreach (var error in Report.GroupErrors) if (error != PlacementError.None) { Error = error; break; }
            using var records = new NativeList<BlockRecord>(Allocator.Temp);
            foreach (var op in Batch.Ops) records.Add(op.Record);
            preview.Set(records.AsArray(), Error);
        }
        internal void BeginStroke() { StrokeId = checked(StrokeId + 1); }
        internal bool TryHovered(out BlockRecord record)
        {
            record = default;
            return Hover.Kind == DraftPicker.HitKind.Block && new DraftReader(Session.Draft, Content).TryGetBlockAt(Hover.Cell, out record);
        }
        internal bool TryPlacement(out BlockRecord record)
        {
            record = default;
            if (Hover.Kind == DraftPicker.HitKind.None || SelectedBlock == 0 || SelectedBlock >= Content.Footprints.BlockCount) return false;
            var footprint = Content.Footprints.Get(SelectedBlock, Orient);
            record = new BlockRecord { Block = SelectedBlock, Orient = Orient, Color = Color, DamageOffset = -1,
                Origin = PlacementMath.OriginFor(Hover.PlacementCell, Hover.Normal, footprint.Min, footprint.Max) };
            return true;
        }
        bool PreparePlacement()
        {
            Batch.Clear(); copies.Clear(); Error = PlacementError.None;
            if (!TryPlacement(out var record)) return false;
            Error = Mapper.BuildCopies(record, default, Symmetry, copies, Batch.Damage);
            if (Error != PlacementError.None) return false;
            foreach (var copy in copies) Batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = copy });
            Batch.Groups.Add(new BatchGroup { OpStart = 0, OpCount = Batch.Ops.Length });
            return true;
        }
        internal bool Place()
            => PreparePlacement() && Apply(ToolId.Place, "Place blocks");
        internal bool RemoveHovered(ToolId tool)
        {
            return TryHovered(out var record) && Remove(record, tool);
        }
        internal bool Remove(BlockRecord record, ToolId tool)
        {
            Batch.Clear(); copies.Clear(); Error = PlacementError.None;
            Error = Mapper.BuildRemovals(record, Symmetry, Session.Draft, copies);
            if (Error != PlacementError.None) return false;
            foreach (var copy in copies) Batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = copy });
            Batch.Groups.Add(new BatchGroup { OpStart = 0, OpCount = Batch.Ops.Length });
            return Apply(tool, "Remove blocks");
        }
        internal bool Paint(BlockRecord record)
        {
            if (record.Color == Color) return false;
            Batch.Clear(); Error = PlacementError.None;
            record.DamageOffset = new DraftReader(Session.Draft, Content).CaptureDamage(record, Batch.Damage);
            Batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = record });
            record.Color = Color;
            Batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = record });
            Batch.Groups.Add(new BatchGroup { OpStart = 0, OpCount = 2 });
            return Apply(ToolId.Paint, "Paint blocks");
        }
        internal void Sample(BlockRecord record)
        { SelectedBlock = record.Block; Orient = record.Orient; Color = record.Color; }
        internal void PreviewPlacement(PreviewBuffer preview)
        {
            if (!PreparePlacement()) { preview.Set(copies.AsArray(), Error); return; }
            Report.Clear();
            previewValidator.Validate(Session.Draft, Batch, Session.OriginalCounts, Session.Options.Creative, AtomicityMode.PerGroup, Report);
            Error = Report.GroupErrors.Count > 0 ? Report.GroupErrors[0] : PlacementError.None;
            preview.Set(copies.AsArray(), Error);
        }
        bool Apply(ToolId tool, string label)
        {
            bool changed = Session.TryApply(Batch, AtomicityMode.PerGroup, StrokeId, tool, label, Report);
            Error = Report.GroupErrors.Count > 0 ? Report.GroupErrors[0] : PlacementError.None;
            return changed;
        }
        public void Dispose()
        {
            Selection.Dispose(); Batch.Dispose(); previewScratch.Dispose();
            if (copies.IsCreated) copies.Dispose(); copies = default;
        }
    }
}
