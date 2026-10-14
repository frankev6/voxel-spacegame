using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.ShipEditor.Validation;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Tools
{
    /// <summary>Whole-stamp placement; rotations are rebuilt from the unchanged pristine clipboard.</summary>
    internal sealed class PasteTool : IEditorTool, IDisposable
    {
        readonly Clipboard clipboard;
        readonly ValidationScratch scratch = new();
        readonly PlacementValidator validator;
        NativeList<BlockRecord> originals = new(Allocator.Persistent), copies = new(Allocator.Persistent), previewRecords = new(Allocator.Persistent);
        NativeHashSet<BlockRecord> identities = new(64, Allocator.Persistent);
        byte rotation;
        public ToolId Id => ToolId.Paste;
        public bool HasGesture => false;
        public bool ConsumesWheel => false;
        public int Version { get; private set; }
        internal PasteTool(Clipboard clipboard, EditSession session, EditorContext content)
        { this.clipboard = clipboard; validator = new PlacementValidator(content, session.Placement, scratch); }
        internal void Reset() { rotation = 0; Version++; }
        internal void Rotate(EditorContext content, bool reverse)
        { rotation = content.Orientations.Value.Compose(content.Orientations.Value.AxisTurn(1, reverse ? -1 : 1), rotation); Version++; }
        public void Tick(ToolContext context, in EditorInputFrame input)
        {
            if (!input.PointerAllowed || !input.PrimaryPressed || !Prepare(context)) return;
            context.BeginStroke();
            context.Session.TryApply(context.Batch, AtomicityMode.AllOrNothing, context.StrokeId, Id, "Paste blocks", context.Report);
            context.SetError(context.Report.GroupErrors.Count > 0 ? context.Report.GroupErrors[0] : PlacementError.None);
        }
        public void BuildPreview(ToolContext context, PreviewBuffer preview)
        {
            if (!Prepare(context)) { preview.Set(previewRecords.AsArray(), context.Error); return; }
            context.Report.Clear();
            validator.Validate(context.Session.Draft, context.Batch, context.Session.OriginalCounts,
                context.Session.Options.Creative, AtomicityMode.AllOrNothing, context.Report);
            context.SetError(context.Report.GroupErrors.Count > 0 ? context.Report.GroupErrors[0] : PlacementError.None);
            preview.Set(previewRecords.AsArray(), context.Error);
        }
        bool Prepare(ToolContext context)
        {
            context.Batch.Clear(); originals.Clear(); copies.Clear(); previewRecords.Clear(); identities.Clear(); context.SetError(PlacementError.None);
            if (clipboard.Count == 0 || context.Hover.Kind == DraftPicker.HitKind.None) return false;
            try
            {
                int3 min = new(int.MaxValue);
                var matrix = context.Content.Orientations.Value.Matrices[rotation];
                foreach (var source in clipboard.Records)
                {
                    var record = source;
                    for (int axis = 0; axis < 3; axis++)
                        record.Origin[axis] = checked((int)((long)matrix.c0[axis] * source.Origin.x +
                            (long)matrix.c1[axis] * source.Origin.y + (long)matrix.c2[axis] * source.Origin.z));
                    record.Orient = context.Content.Orientations.Value.Compose(rotation, source.Orient);
                    var footprint = context.Content.Footprints.Get(record.Block, record.Orient); int3 low = default;
                    for (int axis = 0; axis < 3; axis++) low[axis] = checked(record.Origin[axis] + footprint.Min[axis]);
                    min = math.min(min, low); originals.Add(record);
                }
                for (int i = 0; i < originals.Length; i++)
                {
                    var record = originals[i];
                    for (int axis = 0; axis < 3; axis++)
                        record.Origin[axis] = checked((int)((long)record.Origin[axis] - min[axis] + context.Hover.PlacementCell[axis]));
                    originals[i] = record; Add(context, record); identities.Add(Identity(record));
                }
            }
            catch (OverflowException)
            { context.Batch.Clear(); previewRecords.Clear(); context.SetError(PlacementError.OutOfBounds); return false; }
            foreach (var record in originals)
            {
                copies.Clear(); var error = context.Mapper.BuildCopies(record, default, context.Symmetry, copies, context.Batch.Damage);
                if (error != PlacementError.None) { context.Batch.Clear(); context.SetError(error); return false; }
                // Preserve every source record; drop only redundant mirror copies of the same placed block.
                for (int i = 1; i < copies.Length; i++) if (identities.Add(Identity(copies[i]))) Add(context, copies[i]);
            }
            context.Batch.Groups.Add(new BatchGroup { OpStart = 0, OpCount = context.Batch.Ops.Length });
            return true;
        }
        static BlockRecord Identity(BlockRecord record) { record.Color = 0; return record; }
        void Add(ToolContext context, BlockRecord record)
        { context.Batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = record }); previewRecords.Add(record); }
        public bool Cancel() => false;
        public void Dispose()
        {
            scratch.Dispose(); if (originals.IsCreated) originals.Dispose(); if (copies.IsCreated) copies.Dispose();
            if (previewRecords.IsCreated) previewRecords.Dispose(); if (identities.IsCreated) identities.Dispose();
        }
    }
}
