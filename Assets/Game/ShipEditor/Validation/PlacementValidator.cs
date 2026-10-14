using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Voxel.Model;
using Unity.Mathematics;

namespace Game.ShipEditor.Validation
{
    /// <summary>SPEC-51 §5.4: ordered validation against an overlay, without mutating the draft.</summary>
    internal sealed class PlacementValidator
    {
        readonly EditorContext context;
        readonly PlacementIndexes indexes;
        readonly ValidationScratch scratch;
        VoxelStructure draft;
        EditBatch batch;
        BlockCountTables original;
        bool creative;
        internal PlacementValidator(EditorContext context, PlacementIndexes indexes, ValidationScratch scratch)
        { this.context = context; this.indexes = indexes; this.scratch = scratch; }
        internal bool Validate(VoxelStructure source, EditBatch input, BlockCountTables originalCounts, bool isCreative,
            AtomicityMode mode, ApplyReport report)
        {
            draft = source; batch = input; original = originalCounts; creative = isCreative; scratch.Clear();
            int groups = math.max(1, batch.Groups.Length);
            for (int group = 0; group < groups; group++)
            {
                int start = batch.Groups.Length == 0 ? 0 : batch.Groups[group].OpStart;
                int count = batch.Groups.Length == 0 ? batch.Ops.Length : batch.Groups[group].OpCount;
                var checkpoint = scratch.Mark(); var error = PlacementError.None; bool hasAdd = false;
                for (int n = start; n < start + count && error == PlacementError.None; n++)
                {
                    var op = batch.Ops[n];
                    if (op.Kind == DiffOpKind.Remove) error = Remove(op.Record);
                    else if (op.Kind == DiffOpKind.Add) { hasAdd = true; error = Add(op.Record, group); }
                    else throw new ArgumentException("EDT: unknown diff operation.", nameof(input));
                }
                if (error == PlacementError.None && hasAdd)
                {
                    error = Attachment(group, checkpoint.Candidates);
                    if (error == PlacementError.None && indexes.Cores + scratch.CoreDelta > 1) error = PlacementError.CoreLimit;
                    if (error == PlacementError.None && indexes.SolidCells + scratch.CellDelta > context.Config.MaxCells) error = PlacementError.OverBudget;
                }
                report.AddGroup(error);
                if (error != PlacementError.None)
                {
                    scratch.Rollback(checkpoint);
                    if (mode == AtomicityMode.AllOrNothing) { scratch.AcceptedOps.Clear(); report.AcceptedGroups = 0; return false; }
                }
                else for (int n = start; n < start + count; n++) scratch.AcceptedOps.Add(n);
            }
            return scratch.AcceptedOps.Length != 0;
        }
        ValidationScratch.CellState At(int3 cell)
        {
            scratch.MissingDelta.TryGetValue(cell, out int missingDelta);
            bool reserved = indexes.MissingCount(cell) + missingDelta > 0;
            if (scratch.Cells.TryGetValue(cell, out var changed)) { changed.Reserved = reserved; return changed; }
            if (!draft.GetCell(cell).IsAir && draft.TryGetBlockAt(cell, out var placed, out _))
                return new ValidationScratch.CellState { Present = true, Reserved = reserved, Group = -1,
                    Block = new BlockRecord { Block = placed.Block, Origin = placed.Origin, Orient = placed.Orient, Color = placed.Color, DamageOffset = -1 } };
            return new ValidationScratch.CellState { Reserved = reserved, Group = -1 };
        }

        PlacementError Remove(BlockRecord record)
        {
            bool found = scratch.Origins.TryGetValue(record, out var current);
            if (!found) found = indexes.TryGetBlock(record, out current);
            if (!found || current.Block == 0 || !current.Equals(record)) return PlacementError.NotFound;
            var footprint = context.Footprints.Get(current.Block, current.Orient);
            foreach (var offset in footprint.Cells)
            {
                int3 cell = current.Origin + offset;
                var occupant = At(cell);
                if (occupant.Present && occupant.Block.Equals(record))
                { scratch.CellDelta--; scratch.SetOverlayCell(cell, new ValidationScratch.CellState { Group = -1 }); }
                else scratch.ChangeMissing(cell, -1);
            }
            foreach (var offset in footprint.Clearance) scratch.ChangeClearance(current.Origin + offset, -1);
            if (context.Definitions.Value.Blocks[current.Block].IsCore) scratch.CoreDelta--;
            scratch.SetOrigin(current, default); return PlacementError.None;
        }
        PlacementError Add(BlockRecord record, int group)
        {
            if (record.Block == 0 || record.Block >= context.Definitions.Value.Blocks.Length ||
                !creative && !context.Items.HasItem(record.Block) && original.Intact(record.Block) + original.Damaged(record.Block) == 0)
                return PlacementError.NotAvailable;
            ref var definition = ref context.Definitions.Value.Blocks[record.Block];
            if (record.Orient >= 24 || (definition.AllowedOrientations & (1u << record.Orient)) == 0)
                return PlacementError.OrientationNotAllowed;
            var footprint = context.Footprints.Get(record.Block, record.Orient);
            if (record.DamageOffset < -1 || record.DamageOffset >= 0 && record.DamageOffset > batch.Damage.Length - footprint.Cells.Length - 4)
                throw new ArgumentException("EDT: damage bytes are truncated.");
            int3 min = default, max = default; bool bounds = indexes.TryGetBounds(out min, out max);
            if (scratch.HasBounds) { min = bounds ? math.min(min, scratch.Min) : scratch.Min; max = bounds ? math.max(max, scratch.Max) : scratch.Max; bounds = true; }
            foreach (var offset in footprint.Cells)
            {
                if (!InBounds(record.Origin, offset, out int3 cell)) return PlacementError.OutOfBounds;
                min = bounds ? math.min(min, cell) : cell; max = bounds ? math.max(max, cell) : cell; bounds = true;
            }
            var limit = context.Config.MaxExtentCells;
            if ((long)max.x - min.x + 1 > limit.x || (long)max.y - min.y + 1 > limit.y || (long)max.z - min.z + 1 > limit.z)
                return PlacementError.OutOfBounds;
            foreach (var offset in footprint.Cells)
            {
                int3 cell = record.Origin + offset; var occupant = At(cell);
                if (occupant.Present || occupant.Reserved)
                    return occupant.Group == group ? PlacementError.SymmetryOverlap : PlacementError.Occupied;
                scratch.ClearanceDelta.TryGetValue(cell, out int delta);
                if (indexes.Clearance[cell] + delta != 0) return PlacementError.InOtherClearance;
            }
            foreach (var offset in footprint.Clearance)
            {
                var occupant = At(record.Origin + offset);
                if (occupant.Present) return PlacementError.ClearanceBlocked;
            }
            for (int i = 0; i < footprint.Cells.Length; i++)
            {
                int3 cell = record.Origin + footprint.Cells[i];
                bool present = record.DamageOffset == -1 || batch.Damage[record.DamageOffset + i] != 0;
                scratch.SetOverlayCell(cell, new ValidationScratch.CellState { Block = record, Group = group, Present = present, Reserved = !present });
                if (present) { scratch.CellDelta++; scratch.Candidates.Add(cell); }
                else scratch.ChangeMissing(cell, 1);
                scratch.Include(cell);
            }
            foreach (var offset in footprint.Clearance) scratch.ChangeClearance(record.Origin + offset, 1);
            if (definition.IsCore) scratch.CoreDelta++;
            scratch.SetOrigin(record, record); return PlacementError.None;
        }
        bool InBounds(int3 origin, int3 offset, out int3 cell)
        {
            var low = context.Config.BuildLimitMin; var high = context.Config.BuildLimitMax;
            long x = (long)origin.x + offset.x, y = (long)origin.y + offset.y, z = (long)origin.z + offset.z;
            cell = default;
            if (x < low.x || x > high.x || y < low.y || y > high.y || z < low.z || z > high.z) return false;
            cell = new int3((int)x, (int)y, (int)z); return true;
        }
        PlacementError Attachment(int group, int firstCandidate)
        {
            scratch.GroupCells.Clear(); scratch.Visited.Clear();
            for (int i = firstCandidate; i < scratch.Candidates.Length; i++)
            {
                int3 cell = scratch.Candidates[i]; var state = At(cell);
                if (state.Present && state.Group == group) scratch.GroupCells.Add(cell);
            }
            if (indexes.SolidCells + scratch.CellDelta == scratch.GroupCells.Count) return PlacementError.None;
            foreach (var root in scratch.GroupCells)
            {
                if (!scratch.Visited.Add(root)) continue;
                bool attached = false; scratch.Stack.Clear(); scratch.Stack.Add(root);
                while (scratch.Stack.Length != 0)
                {
                    int last = scratch.Stack.Length - 1; int3 cell = scratch.Stack[last]; scratch.Stack.RemoveAt(last);
                    var own = At(cell); byte ownFaces = (byte)context.Footprints.Get(own.Block.Block, own.Block.Orient).AttachFaces;
                    for (int face = 0; face < 6; face++)
                    {
                        int3 neighbour = cell; neighbour[face / 2] += (face & 1) == 0 ? 1 : -1;
                        if (scratch.GroupCells.Contains(neighbour))
                        { if (scratch.Visited.Add(neighbour)) scratch.Stack.Add(neighbour); continue; }
                        var other = At(neighbour);
                        if (!other.Present || (ownFaces & (1 << face)) == 0) continue;
                        byte faces = (byte)context.Footprints.Get(other.Block.Block, other.Block.Orient).AttachFaces;
                        if ((faces & (1 << (face ^ 1))) != 0) attached = true;
                    }
                }
                if (!attached) return PlacementError.NoAttachment;
            }
            return PlacementError.None;
        }
    }
}
