using System;
using Unity.Collections;
using Unity.Profiling;
using Unity.Mathematics;
using Game.ShipEditor.Data;
using Game.Voxel.Model;

namespace Game.ShipEditor.Services
{
    /// <summary>Implements SPEC-51 §6. EDT-005 owns the session; related methods retain their implementing task IDs.</summary>
    public sealed class EditSession : System.IDisposable
    {
        readonly EditorContext context;
        readonly ShipEditorEntryArgs entry;
        readonly DirtyBrickSet dirty = new();
        readonly ProfilerMarker applyMarker = new("EditSession.Apply");
        readonly ProfilerMarker undoMarker = new("EditSession.Undo");
        readonly ProfilerMarker validateMarker = new("EditSession.Validate");
        bool disposed;
        public EditSession(ShipEditorEntryArgs args, EditorContext context)
        {
            entry = args ?? throw new ArgumentNullException(nameof(args));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (args.Source == null) throw new ArgumentException("Editor source is required.", nameof(args));
            Options = args.Options;
            try
            {
                Original = args.Source.Clone(); Draft = args.Source.Clone();
                History = new EditHistory(context.Config.HistoryBudgetBytes, context.Config.HistoryMaxEntries);
                MarkAllBricks(Draft);
                // TODO(EDT-006/007/008/010): initialize count/placement/connectivity/stat indexes from one enumeration.
                Notify(EditChangeKind.Opened, 0);
            }
            catch { Dispose(); throw; }
        }
        public EditSessionOptions Options { get; }
        public VoxelStructure Draft { get; private set; }
        public VoxelStructure Original { get; private set; }
        public EditHistory History { get; private set; }
        public bool IsDirty => History.IsDirty;
        public int Revision { get; private set; }
        // TODO(EDT-008): own and complete asynchronous connectivity work.
        public bool ConnectivityPending => false;
        public event Action<EditChange> Changed;
        public bool TryApply(EditBatch batch, AtomicityMode mode, int strokeId, ToolId tool, string label, ApplyReport report)
        {
            CheckAlive();
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (report == null) throw new ArgumentNullException(nameof(report));
            report.Clear(); CompleteRelabel();
            if (mode != AtomicityMode.AllOrNothing && mode != AtomicityMode.PerGroup) throw new ArgumentOutOfRangeException(nameof(mode));
            int count = batch.Ops.Length, end = 0;
            using (validateMarker.Auto())
            {
                for (int i = 0; i < batch.Groups.Length; i++)
                {
                    var group = batch.Groups[i];
                    if (group.OpStart != end || group.OpCount < 0 || group.OpCount > count - end)
                        throw new ArgumentException("EDT: groups must partition the operations.", nameof(batch));
                    end += group.OpCount;
                }
                if (batch.Groups.Length != 0 && end != count) throw new ArgumentException("EDT: groups must cover the operations.", nameof(batch));
            }
            if (count == 0) return false;
            // TODO(EDT-007): replace the specified pass-through stage with per-group placement validation.
            for (int i = 0; i < math.max(1, batch.Groups.Length); i++) report.AddGroup(PlacementError.None);
            var command = new DiffCommand(tool, label, strokeId);
            try
            {
                command.Damage.AddRange(batch.Damage.AsArray());
                var reader = new DraftReader(Draft, context);
                // Earlier operations can create the very block a later operation removes.
                // Track only touched origins, rather than rescanning the full command or ship.
                var pending = new System.Collections.Generic.Dictionary<int3, BlockRecord>();
                for (int i = 0; i < count; i++)
                {
                    var op = batch.Ops[i]; var record = op.Record;
                    if (op.Kind == DiffOpKind.Remove)
                    {
                        if (pending.TryGetValue(record.Origin, out var added))
                        {
                            if (!added.Equals(record)) throw new InvalidOperationException("EDT: removal disagrees with the preceding addition.");
                            record.DamageOffset = added.DamageOffset; pending.Remove(record.Origin);
                        }
                        else record.DamageOffset = reader.CaptureDamage(record, command.Damage);
                    }
                    else if (op.Kind == DiffOpKind.Add) pending[record.Origin] = record;
                    else throw new ArgumentException("EDT: unknown diff operation.", nameof(batch));
                    op.Record = record; command.Ops.Add(op);
                }
                ApplyCommand(command, true); History.Push(command, strokeId);
            }
            catch { if (!command.Owned) command.Dispose(); throw; }
            report.OpsApplied = count; Notify(EditChangeKind.Applied, count); return true;
        }
        public bool Undo()
        {
            using var marker = undoMarker.Auto();
            CheckAlive(); CompleteRelabel(); if (!History.CanUndo) return false;
            var command = History[History.Cursor - 1]; ApplyCommand(command, false); History.MoveUndo();
            Notify(EditChangeKind.Undone, command.Ops.Length); return true;
        }
        public bool Redo()
        {
            CheckAlive(); CompleteRelabel(); if (!History.CanRedo) return false;
            var command = History[History.Cursor]; ApplyCommand(command, true); History.MoveRedo();
            Notify(EditChangeKind.Redone, command.Ops.Length); return true;
        }
        void ApplyCommand(DiffCommand command, bool forward)
        {
            using var marker = applyMarker.Auto();
            using var touched = new NativeList<int3>(Allocator.Temp);
            new DraftWriter(Draft, context).Apply(command.Ops.AsArray(), command.Damage.AsArray(), forward, touched);
            foreach (var brick in touched) dirty.Add(brick);
            foreach (var op in command.Ops)
                foreach (var cell in context.Footprints.Get(op.Record.Block, op.Record.Orient).Cells)
                    dirty.AddCell(op.Record.Origin + cell);
            // TODO(EDT-006/007/008/010): update count, clearance, reserved-missing, connectivity and stats indexes in operation order.
        }
        void MarkAllBricks(VoxelStructure source)
        {
            foreach (var brick in source.Data.Bricks) if (brick.SolidCount != 0) dirty.Add(brick.Coord);
        }
        internal void DrainDirtyBricks(NativeList<int3> output) { CheckAlive(); dirty.Drain(output); }
        void Notify(EditChangeKind kind, int count)
        { Revision++; Changed?.Invoke(new EditChange { Kind = kind, OpsApplied = count, Revision = Revision }); }
        // TODO(EDT-006): lazily refreshed BOM.
        public BillOfMaterials GetBom() => throw new System.NotImplementedException("EDT-006");
        // TODO(EDT-010): original/draft stats.
        public StatTotals GetStats(bool original) => throw new System.NotImplementedException("EDT-010");
        // TODO(EDT-009): full validation and issue list.
        public void GetIssues(System.Collections.Generic.List<EditIssue> result) => throw new System.NotImplementedException("EDT-009");
        // TODO(EDT-011): atomic inventory/target commit.
        public CommitResult Commit() => throw new System.NotImplementedException("EDT-011");
        public void Discard()
        {
            CheckAlive(); CompleteRelabel(); var replacement = Original.Clone();
            MarkAllBricks(Draft); MarkAllBricks(replacement);
            Draft.Dispose(); Draft = replacement; History.Clear();
            // TODO(EDT-006/007/008/010): reset owned indexes from Original alongside the clone.
            Notify(EditChangeKind.Discarded, 0);
        }
        public void CompleteRelabel()
        { CheckAlive(); /* TODO(EDT-008): complete the owned relabel job before reads or writes. */ }
        public ulong ComputeDraftHash() { CheckAlive(); return new DraftReader(Draft, context).ComputeDraftHash(); }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(EditSession)); }
        public void Dispose()
        {
            if (disposed) return;
            // TODO(EDT-008): join/release connectivity jobs before disposing source storage.
            History?.Dispose(); Draft?.Dispose(); Original?.Dispose(); dirty.Clear();
            Changed = null; disposed = true;
        }
    }
}
