using System;
using Unity.Collections;
using Unity.Profiling;
using Unity.Mathematics;
using Game.ShipEditor.Data;
using Game.ShipEditor.Validation;
using Game.Voxel.Model;

namespace Game.ShipEditor.Services
{
    /// <summary>Implements SPEC-51 §6. EDT-005 owns the session; related methods retain their implementing task IDs.</summary>
    public sealed class EditSession : System.IDisposable
    {
        readonly EditorContext context;
        readonly ShipEditorEntryArgs entry;
        readonly DirtyBrickSet dirty = new();
        readonly PlacementIndexes placement;
        readonly ConnectivityTracker connectivity;
        internal ConnectivityTracker Connectivity => connectivity;
        readonly ValidationScratch validation;
        readonly PlacementValidator validator;
        readonly ProfilerMarker applyMarker = new("EditSession.Apply");
        readonly ProfilerMarker undoMarker = new("EditSession.Undo");
        readonly ProfilerMarker validateMarker = new("EditSession.Validate");
        readonly BillOfMaterials bom = new();
        bool bomDirty = true;
        readonly System.Collections.Generic.List<EditIssue> issues = new();
        bool issuesDirty = true;
        double nextIssueRefresh;
        internal PlacementIndexes Placement => placement;
        StatsCalculator originalStats, draftStats;
        StatTotals originalTotals, draftTotals;
        bool originalStatsDirty = true, draftStatsDirty = true;
        internal BlockCountTables OriginalCounts { get; private set; }
        internal BlockCountTables DraftCounts { get; private set; }
        bool disposed;
        public EditSession(ShipEditorEntryArgs args, EditorContext context)
        {
            entry = args ?? throw new ArgumentNullException(nameof(args));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (args.Source == null) throw new ArgumentException("Editor source is required.", nameof(args));
            Options = args.Options;
            try
            {
                connectivity = new ConnectivityTracker(context.Config.SplitCheckBudgetCells);
                placement = new PlacementIndexes(); validation = new ValidationScratch();
                validator = new PlacementValidator(context, placement, validation);
                Original = args.Source.Clone(); Draft = args.Source.Clone();
                History = new EditHistory(context.Config.HistoryBudgetBytes, context.Config.HistoryMaxEntries);
                MarkAllBricks(Draft);
                originalStats = new StatsCalculator(context); draftStats = new StatsCalculator(context);
                OriginalCounts = new BlockCountTables(context.Items.Count); OriginalCounts.Initialize(Original, context, originalStats.AddInitial);
                DraftCounts = new BlockCountTables(context.Items.Count); DraftCounts.CopyFrom(OriginalCounts);
                placement.Initialize(Draft, context); draftStats.CopyFrom(originalStats);
                if (!Options.Creative && entry.Inventory != null) entry.Inventory.Changed += InvalidateBom;
                connectivity.Initialize(Draft);
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
        public bool ConnectivityPending => connectivity.ConnectivityPending;
        public event Action<EditChange> Changed;
        // Borrowed only during the callback, before History.Push can merge/dispose this command.
        // Presentation observers must contain their failures so they cannot interrupt history bookkeeping.
        internal event Action<DiffCommand, bool> DraftApplied;
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
            using (validateMarker.Auto())
                if (!validator.Validate(Draft, batch, OriginalCounts, Options.Creative, mode, report)) return false;
            count = validation.AcceptedOps.Length;
            var command = new DiffCommand(tool, label, strokeId);
            try
            {
                command.Damage.AddRange(batch.Damage.AsArray());
                var reader = new DraftReader(Draft, context);
                // Earlier operations can create the very block a later operation removes.
                // Track exact block identities: damaged blocks may legally share an origin.
                var pending = new System.Collections.Generic.Dictionary<BlockRecord, BlockRecord>();
                for (int i = 0; i < count; i++)
                {
                    var op = batch.Ops[validation.AcceptedOps[i]]; var record = op.Record;
                    if (op.Kind == DiffOpKind.Remove)
                    {
                        if (pending.TryGetValue(record, out var added))
                        {
                            if (!added.Equals(record)) throw new InvalidOperationException("EDT: removal disagrees with the preceding addition.");
                            record.DamageOffset = added.DamageOffset; pending.Remove(record);
                        }
                        else record.DamageOffset = reader.CaptureDamage(record, command.Damage);
                    }
                    else if (op.Kind == DiffOpKind.Add) pending[record] = record;
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
            DraftCounts.Apply(command, forward, context); placement.Apply(command, forward, context); InvalidateBom();
            draftStats.Apply(command, forward); draftStatsDirty = true;
            using (var cells = new NativeList<int3>(Allocator.Temp))
            {
                for (int n = 0; n < command.Ops.Length; n++)
                {
                    var op = command.Ops[forward ? n : command.Ops.Length - 1 - n];
                    var record = op.Record; cells.Clear();
                    var footprint = context.Footprints.Get(record.Block, record.Orient).Cells;
                    for (int i = 0; i < footprint.Length; i++)
                        if (record.DamageOffset < 0 || command.Damage[record.DamageOffset + i] > 0) cells.Add(record.Origin + footprint[i]);
                    if ((op.Kind == DiffOpKind.Add) == forward) connectivity.OnAdd(cells.AsArray());
                    else connectivity.OnRemove(cells.AsArray());
                }
            }
            DraftApplied?.Invoke(command, forward);
        }
        void MarkAllBricks(VoxelStructure source)
        {
            foreach (var brick in source.Data.Bricks) if (brick.SolidCount != 0) dirty.Add(brick.Coord);
        }
        internal void DrainDirtyBricks(NativeList<int3> output) { CheckAlive(); dirty.Drain(output); }
        void Notify(EditChangeKind kind, int count)
        { Revision++; Changed?.Invoke(new EditChange { Kind = kind, OpsApplied = count, Revision = Revision }); }
        void InvalidateBom() { bomDirty = true; issuesDirty = true; }
        public BillOfMaterials GetBom()
        {
            CheckAlive();
            if (bomDirty)
            {
                BomCalculator.Refresh(bom, OriginalCounts, DraftCounts, context.Items, entry.Inventory, Options.Creative);
                bomDirty = false;
            }
            return bom;
        }
        public StatTotals GetStats(bool original)
        {
            CheckAlive();
            if (original)
            {
                if (originalStatsDirty) { originalTotals = originalStats.Calculate(OriginalCounts, Original); originalStatsDirty = false; }
                return originalTotals;
            }
            if (draftStatsDirty) { draftTotals = draftStats.Calculate(DraftCounts, Draft); draftStatsDirty = false; }
            return draftTotals;
        }
        public void GetIssues(System.Collections.Generic.List<EditIssue> result)
        {
            CheckAlive(); if (result == null) throw new ArgumentNullException(nameof(result));
            if (issuesDirty && UnityEngine.Time.realtimeSinceStartupAsDouble >= nextIssueRefresh) RefreshIssuesNow();
            result.Clear(); result.AddRange(issues);
        }
        internal void RefreshIssuesNow()
        {
            CheckAlive(); FullValidator.Evaluate(this, context, issues); issuesDirty = false;
            nextIssueRefresh = UnityEngine.Time.realtimeSinceStartupAsDouble + .25;
        }
        public bool RemoveFloatingParts(ApplyReport report)
        {
            CheckAlive(); if (report == null) throw new ArgumentNullException(nameof(report)); report.Clear(); CompleteRelabel();
            if (connectivity.IslandCount <= 1) return false;
            using var records = new NativeList<BlockRecord>(Allocator.TempJob); new DraftReader(Draft, context).Enumerate(records);
            int keep = FullValidator.KeptRoot(this, context, records);
            using var batch = new EditBatch();
            foreach (var record in records)
            {
                if (context.Definitions.Value.Blocks[record.Block].IsCore) continue;
                foreach (var offset in context.Footprints.Get(record.Block, record.Orient).Cells)
                {
                    int3 cell = record.Origin + offset;
                    if (!FullValidator.OwnsCell(this, record, cell) || connectivity.RootAt(cell) == keep) continue;
                    batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = record }); break;
                }
            }
            return TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Remove, "Remove floating parts", report);
        }
        // TODO(EDT-011): atomic inventory/target commit.
        public CommitResult Commit() => throw new System.NotImplementedException("EDT-011");
        public void Discard()
        {
            CheckAlive(); CompleteRelabel(); var replacement = Original.Clone();
            MarkAllBricks(Draft); MarkAllBricks(replacement);
            Draft.Dispose(); Draft = replacement; History.Clear();
            DraftCounts.CopyFrom(OriginalCounts); placement.Initialize(Draft, context); InvalidateBom();
            draftStats.CopyFrom(originalStats); draftStatsDirty = true;
            connectivity.Initialize(Draft);
            Notify(EditChangeKind.Discarded, 0);
        }
        public void CompleteRelabel()
        { CheckAlive(); connectivity.CompleteRelabel(); }
        public ulong ComputeDraftHash() { CheckAlive(); return new DraftReader(Draft, context).ComputeDraftHash(); }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(EditSession)); }
        public void Dispose()
        {
            if (disposed) return;
            connectivity?.Dispose();
            if (!Options.Creative && entry.Inventory != null) entry.Inventory.Changed -= InvalidateBom;
            OriginalCounts?.Dispose(); DraftCounts?.Dispose(); placement?.Dispose(); validation?.Dispose();
            History?.Dispose(); Draft?.Dispose(); Original?.Dispose(); dirty.Clear();
            Changed = null; DraftApplied = null; disposed = true;
        }
    }
}
