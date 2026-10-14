using System;
using Unity.Collections;
using Unity.Profiling;
using Unity.Mathematics;
using Game.ShipEditor.Data;
using Game.Economy;
using Game.Core;
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
        readonly ProfilerMarker commitMarker = new("EditSession.Commit");
        bool committing;
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
            CheckAlive(); CheckNotCommitting();
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
            CheckAlive(); CheckNotCommitting(); CompleteRelabel(); if (!History.CanUndo) return false;
            var command = History[History.Cursor - 1]; ApplyCommand(command, false); History.MoveUndo();
            Notify(EditChangeKind.Undone, command.Ops.Length); return true;
        }
        public bool Redo()
        {
            CheckAlive(); CheckNotCommitting(); CompleteRelabel(); if (!History.CanRedo) return false;
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
        public CommitResult Commit()
        {
            CheckAlive();
            if (committing) return Result(CommitStatus.Invalid, "A ship commit is already in progress");
            if (!IsDirty) return Result(CommitStatus.NothingToCommit, "No changes to commit");
            using var marker = commitMarker.Auto();
            committing = true;
            VoxelStructure committed = null, baseline = null;
            BlockCountTables baselineCounts = null;
            InventoryModel.Checkpoint checkpoint = null;
            bool accepted = false;
            try
            {
                RefreshIssuesNow();
                foreach (var issue in issues) if (issue.IsError) return Result(CommitStatus.Invalid, IssueText(issue));
                if (entry.CommitTarget == null) return Result(CommitStatus.TargetRejected, "Ship commit target is unavailable");
                if (Original.IsPinned) return Result(CommitStatus.Invalid, "Original ship is still being read");
                var material = GetBom(); int used = Options.Creative ? 0 : material.TotalRequired, refunded = Options.Creative ? 0 : material.TotalRefunded;
                var deltas = new System.Collections.Generic.List<ItemDelta>();
                if (!Options.Creative)
                {
                    if (entry.Inventory == null) return Result(CommitStatus.Invalid, "Ship inventory is unavailable");
                    foreach (var line in material.Lines)
                        if (line.HasItem && line.Refunded != line.Required) deltas.Add(new ItemDelta(line.Item, line.Refunded - line.Required));
                }
                var changes = deltas.ToArray();
                if (!Options.Creative && !CanAfford(changes, out var refusal)) return refusal;
                committed = Draft.Clone(); baseline = Draft.Clone();
                baselineCounts = new BlockCountTables(context.Items.Count); baselineCounts.CopyFrom(DraftCounts);
                if (!Options.Creative)
                {
                    checkpoint = entry.Inventory.CaptureCheckpoint();
                    if (!entry.Inventory.TryApplyAtomic(changes))
                    { checkpoint = null; if (!CanAfford(changes, out refusal)) return refusal; return Result(CommitStatus.MissingItems, "Inventory changed; try again"); }
                }
                string error;
                try { accepted = entry.CommitTarget.TryCommitStructure(committed, out error); }
                catch (Exception exception) { error = exception.Message; }
                if (!accepted) return Result(CommitStatus.TargetRejected, string.IsNullOrEmpty(error) ? "Ship commit was rejected" : error);
                committed = null; // Target owns this clone from this point onward.
                checkpoint = null;
                Original.Dispose(); Original = baseline; baseline = null;
                OriginalCounts.Dispose(); OriginalCounts = baselineCounts; baselineCounts = null;
                originalStats.CopyFrom(draftStats); originalStatsDirty = true;
                History.Clear(); InvalidateBom(); nextIssueRefresh = 0;
                try { Notify(EditChangeKind.Committed, 0); }
                catch (Exception exception) { GameLog.Warn("EDT", "Ship committed; observer failed: " + exception.Message); }
                return new CommitResult { Status = CommitStatus.Committed, Message = "Ship changes committed", ItemsUsed = used, ItemsRefunded = refunded };
            }
            catch (Exception exception)
            { return Result(accepted ? CommitStatus.Committed : CommitStatus.TargetRejected, exception.Message); }
            finally
            {
                try
                {
                    if (checkpoint != null)
                        try { entry.Inventory.RestoreCheckpoint(checkpoint); }
                        catch (Exception exception) { GameLog.Error("EDT", "inventory rollback failed: " + exception.Message); }
                }
                finally { committed?.Dispose(); baseline?.Dispose(); baselineCounts?.Dispose(); committing = false; }
            }
        }
        bool CanAfford(ItemDelta[] changes, out CommitResult result)
        {
            var missing = new System.Collections.Generic.List<ItemShortfall>();
            if (entry.Inventory.CanApply(changes, missing, out bool full)) { result = default; return true; }
            if (missing.Count > 0)
            {
                var first = missing[0];
                string name = context.Catalog.TryGet<Game.Data.Definitions.ItemDefinition>(first.Item, out var definition) ? definition.DisplayName : first.Item.Value;
                result = Result(CommitStatus.MissingItems, "Missing " + first.Missing + " " + name);
            }
            else result = Result(full ? CommitStatus.CargoFull : CommitStatus.MissingItems, full ? "Cargo full: free space before committing" : "Inventory changes cannot be applied");
            return false;
        }
        static CommitResult Result(CommitStatus status, string message) => new() { Status = status, Message = message };
        static string IssueText(EditIssue issue) => issue.Kind switch
        {
            EditIssueKind.NoCore => "Ship needs a core", EditIssueKind.MultipleCores => "Ship has multiple cores",
            EditIssueKind.Disconnected => "Ship contains disconnected blocks", EditIssueKind.OutOfBounds => "Ship exceeds the size limit",
            EditIssueKind.OverCellBudget => "Ship exceeds the cell budget", EditIssueKind.DamageInvariant => "Damaged block count increased",
            EditIssueKind.UnbuildableBlock => "Ship requires an unavailable block", _ => "Ship is invalid"
        };
        public void Discard()
        {
            CheckAlive(); CheckNotCommitting(); CompleteRelabel(); var replacement = Original.Clone();
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
        void CheckNotCommitting() { if (committing) throw new InvalidOperationException("EDT: edits cannot change during commit."); }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(EditSession)); }
        public void Dispose()
        {
            if (disposed) return; CheckNotCommitting();
            connectivity?.Dispose();
            if (!Options.Creative && entry.Inventory != null) entry.Inventory.Changed -= InvalidateBom;
            OriginalCounts?.Dispose(); DraftCounts?.Dispose(); placement?.Dispose(); validation?.Dispose();
            History?.Dispose(); Draft?.Dispose(); Original?.Dispose(); dirty.Clear();
            Changed = null; DraftApplied = null; disposed = true;
        }
    }
}
