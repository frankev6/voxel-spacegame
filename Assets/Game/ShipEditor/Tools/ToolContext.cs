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
        readonly ValidationScratch previewScratch = new();
        readonly PlacementValidator previewValidator;
        NativeList<BlockRecord> copies = new(Allocator.Persistent);
        internal DraftPicker.Hit Hover;
        internal SymmetrySettings Symmetry;
        internal ushort SelectedBlock;
        internal byte Orient, Color;
        internal int StrokeId { get; private set; }
        internal PlacementError Error { get; private set; }
        internal ToolContext(EditSession session, EditorContext content)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Mapper = new SymmetryMapper(content);
            previewValidator = new PlacementValidator(content, session.Placement, previewScratch);
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
            Batch.Dispose(); previewScratch.Dispose();
            if (copies.IsCreated) copies.Dispose(); copies = default;
        }
    }
}
