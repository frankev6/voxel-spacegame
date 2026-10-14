using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Model
{
    public sealed partial class VoxelStructure
    {
        /// <summary>ADR-0082: validate the entire ordered batch in a touched-data overlay before publishing any mutation.</summary>
        public bool ApplyEdits(NativeArray<VoxelEdit> edits, in VoxelContent content, NativeList<int3> touchedBricks)
            => ApplyEdits(edits, default, content, touchedBricks);

        /// <summary>ADR-0089: damaged placement writes only surviving cells; damage storage is borrowed for this call.</summary>
        public bool ApplyEdits(NativeArray<VoxelEdit> edits, NativeArray<byte> damage, in VoxelContent content, NativeList<int3> touchedBricks)
        {
            RequireUnpinned();
            if (IsReadOnly || !content.Blocks.IsCreated || !content.Orientations.IsCreated) return false;
            var plan = new EditPlan(this, content, damage);
            for (int i = 0; i < edits.Length; i++) if (!plan.Add(edits[i])) return false;
            if (!plan.ValidateFinal()) return false;
            plan.Commit(touchedBricks);
            return true;
        }
        sealed class EditPlan
        {
            readonly VoxelStructure owner;
            readonly VoxelContent content;
            readonly NativeArray<byte> damage;
            readonly Dictionary<int3, Cell> cells = new();
            readonly Dictionary<int, BlockInstance> instances = new();
            readonly Dictionary<(int3, ushort, byte, byte), int> origins = new();
            readonly List<PaletteEntry> palettes = new();
            readonly List<int> released = new();
            int freeCursor, instanceLength;
            public EditPlan(VoxelStructure owner, VoxelContent content, NativeArray<byte> damage)
            {
                this.owner = owner; this.content = content; this.damage = damage;
                freeCursor = owner.data.FreeInstances.Length; instanceLength = owner.data.Instances.Length;
            }
            Cell Read(int3 cell) => cells.TryGetValue(cell, out var value) ? value : owner.GetCell(cell);
            BlockInstance Instance(int slot) => instances.TryGetValue(slot, out var value) ? value : owner.data.Instances[slot];
            PaletteEntry Palette(int slot) => slot < owner.data.Palette.Length ? owner.data.Palette[slot] : palettes[slot - owner.data.Palette.Length];
            bool ValidBlock(ushort block) => block > 0 && block < content.Blocks.Value.Blocks.Length;
            int3 Position(in BlockInstance block, int cell) => block.Origin + content.Orientations.Value.Rotate(content.Blocks.Value.Blocks[block.Block].Cells[cell], block.Orient);
            static bool Matches(in VoxelEdit edit, in BlockInstance instance) => instance.Block != 0 &&
                (edit.Block == 0 || (instance.Block == edit.Block && instance.Orient == edit.Orient && instance.Origin.Equals(edit.Cell) &&
                    (edit.Kind != VoxelEditKind.RemoveBlock || instance.Color == edit.Color)));
            int ResolveInstance(in VoxelEdit edit)
            {
                var cell = Read(edit.Cell);
                if (!cell.IsAir && cell.IsInstance && Matches(edit, Instance(cell.InstanceIndex))) return cell.InstanceIndex;
                if (origins.TryGetValue((edit.Cell, edit.Block, edit.Orient, edit.Color), out int slot) && Matches(edit, Instance(slot))) return slot;
                // Remove/HP operations carry block/orientation identity, so a destroyed root needs no whole-structure search.
                if (!ValidBlock(edit.Block) || edit.Orient >= 24) return -1;
                ref var def = ref content.Blocks.Value.Blocks[edit.Block];
                for (int i = 0; i < def.Cells.Length; i++)
                {
                    cell = Read(edit.Cell + content.Orientations.Value.Rotate(def.Cells[i], edit.Orient));
                    if (cell.IsAir || !cell.IsInstance) continue;
                    var instance = Instance(cell.InstanceIndex);
                    if (Matches(edit, instance)) return cell.InstanceIndex;
                }
                return -1;
            }
            public bool Add(in VoxelEdit edit)
            {
                if (math.any(edit.Cell < -1048576) || math.any(edit.Cell > 1048575)) return false;
                switch (edit.Kind)
                {
                    case VoxelEditKind.PlaceBlock: return Place(edit, false);
                    case VoxelEditKind.RestoreBlock: return Place(edit, true);
                    case VoxelEditKind.RemoveBlock: return Remove(edit);
                    case VoxelEditKind.SetCellHp:
                    {
                        var value = Read(edit.Cell);
                        if (value.IsAir || edit.Hp == 0) return false;
                        value.Hp = edit.Hp; cells[edit.Cell] = value; return true;
                    }
                    case VoxelEditKind.ClearCell:
                    {
                        var value = Read(edit.Cell); if (value.IsAir) return true;
                        if (value.IsInstance)
                        {
                            var instance = Instance(value.InstanceIndex);
                            if (instance.CellsAlive == 0) return false;
                            instance.CellsAlive--; instances[value.InstanceIndex] = instance;
                            origins[(instance.Origin, instance.Block, instance.Orient, instance.Color)] = value.InstanceIndex;
                        }
                        cells[edit.Cell] = default; return true;
                    }
                    case VoxelEditKind.SetFunctionHp:
                    {
                        int slot = ResolveInstance(edit); if (slot < 0 || !math.isfinite(edit.FunctionHp)) return false;
                        var instance = Instance(slot);
                        float max = content.Blocks.Value.Blocks[instance.Block].FunctionHp;
                        // Damage can leave negative HP on surviving non-functional instances.
                        // Preserve that exact state for undo, as VOXS does.
                        if (edit.FunctionHp > max) return false;
                        instance.FunctionHp = edit.FunctionHp;
                        bool destroyed = content.Blocks.Value.Blocks[instance.Block].Kind == Game.Data.Blocks.BlockKind.Functional && edit.FunctionHp <= 0;
                        instance.Flags = destroyed ? instance.Flags | BlockInstanceFlags.Destroyed : instance.Flags & ~BlockInstanceFlags.Destroyed;
                        instances[slot] = instance; return true;
                    }
                    default: return false;
                }
            }
            bool Place(in VoxelEdit edit, bool restore)
            {
                if (!ValidBlock(edit.Block) || edit.Orient >= 24) return false;
                ref var def = ref content.Blocks.Value.Blocks[edit.Block];
                if ((def.AllowedOrientations & (1u << edit.Orient)) == 0) return false;
                float functionHp = def.FunctionHp;
                if (restore)
                {
                    if (!damage.IsCreated || edit.DamageOffset < 0 || edit.DamageOffset > damage.Length - def.Cells.Length - 4) return false;
                    int offset = edit.DamageOffset + def.Cells.Length;
                    uint bits = (uint)(damage[offset] | damage[offset + 1] << 8 | damage[offset + 2] << 16 | damage[offset + 3] << 24);
                    functionHp = math.asfloat(bits);
                    if (def.RequiresInstance && (!math.isfinite(functionHp) || functionHp > def.FunctionHp)) return false;
                    if (!def.RequiresInstance && !float.IsNaN(functionHp)) return false;
                }
                int alive = 0;
                for (int i = 0; i < def.Cells.Length; i++)
                {
                    int3 p = edit.Cell + content.Orientations.Value.Rotate(def.Cells[i], edit.Orient);
                    if (math.any(p < -1048576) || math.any(p > 1048575)) return false;
                    if (restore && damage[edit.DamageOffset + i] == 0) continue;
                    if (!Read(p).IsAir) return false;
                    alive++;
                }
                if (alive == 0) return false;
                if (!def.RequiresInstance)
                {
                    var entry = new PaletteEntry { Block = edit.Block, Orient = edit.Orient, Color = edit.Color };
                    int found = 0, count = owner.data.Palette.Length + palettes.Count;
                    for (int i = 1; i < count; i++)
                    { var candidate = Palette(i); if (candidate.Block == entry.Block && candidate.Orient == entry.Orient && candidate.Color == entry.Color) { found = i; break; } }
                    if (found == 0)
                    { if (count > Cell.MaxPaletteIndex) return false; found = count; palettes.Add(entry); }
                    var value = Cell.Palette((ushort)found); if (restore) value.Hp = damage[edit.DamageOffset];
                    cells[edit.Cell] = value; return true;
                }
                int slot;
                if (released.Count != 0) { int last = released.Count - 1; slot = released[last]; released.RemoveAt(last); }
                else if (freeCursor != 0) slot = owner.data.FreeInstances[--freeCursor];
                else { if (instanceLength > Cell.MaxInstanceIndex) return false; slot = instanceLength++; }
                var instance = new BlockInstance { Block = edit.Block, Orient = edit.Orient, Color = edit.Color,
                    Origin = edit.Cell, FunctionHp = functionHp, CellsAlive = (ushort)alive };
                if (def.Kind == BlockKind.Functional && functionHp <= 0) instance.Flags |= BlockInstanceFlags.Destroyed;
                instances[slot] = instance; origins[(edit.Cell, edit.Block, edit.Orient, edit.Color)] = slot;
                for (int i = 0; i < def.Cells.Length; i++)
                {
                    byte hp = restore ? damage[edit.DamageOffset + i] : (byte)255;
                    if (hp != 0) cells[Position(instance, i)] = Cell.Instance(slot, hp, i == 0 ? (byte)CellFlags.InstanceRoot : (byte)0);
                }
                return true;
            }
            bool Remove(in VoxelEdit edit)
            {
                var value = Read(edit.Cell); int slot = ResolveInstance(edit);
                if (slot < 0)
                {
                    if (value.IsAir || value.IsInstance) return false;
                    var palette = Palette(value.PaletteIndex);
                    if (edit.Block != 0 && (palette.Block != edit.Block || palette.Orient != edit.Orient || palette.Color != edit.Color)) return false;
                    cells[edit.Cell] = default; return true;
                }
                var instance = Instance(slot); if (instance.Block == 0) return false;
                ref var def = ref content.Blocks.Value.Blocks[instance.Block];
                for (int i = 0; i < def.Cells.Length; i++)
                {
                    int3 p = Position(instance, i); var cell = Read(p);
                    if (!cell.IsAir && cell.IsInstance && cell.InstanceIndex == slot) cells[p] = default;
                }
                origins.Remove((instance.Origin, instance.Block, instance.Orient, instance.Color)); instances[slot] = default; released.Add(slot); return true;
            }
            public bool ValidateFinal()
            {
                // A zero-cell instance has no placed-block identity; callers remove it explicitly.
                foreach (var entry in instances) if (entry.Value.Block != 0 && entry.Value.CellsAlive == 0) return false;
                return true;
            }
            public void Commit(NativeList<int3> touched)
            {
                // Stable cell order also keeps storage allocation order independent of Dictionary enumeration.
                var positions = new List<int3>(cells.Keys); positions.Sort(Compare);
                var bricks = new HashSet<int3>();
                foreach (var position in positions) bricks.Add(BrickMath.BrickOf(position));
                var orderedBricks = new List<int3>(bricks); orderedBricks.Sort(Compare);
                owner.data.Palette.Capacity = math.max(owner.data.Palette.Capacity, owner.data.Palette.Length + palettes.Count);
                owner.data.Instances.Capacity = math.max(owner.data.Instances.Capacity, instanceLength);
                foreach (var entry in palettes) owner.data.Palette.Add(entry);
                owner.data.Instances.ResizeUninitialized(instanceLength);
                foreach (var entry in instances) owner.data.Instances[entry.Key] = entry.Value;
                owner.data.FreeInstances.ResizeUninitialized(freeCursor);
                foreach (int slot in released) owner.data.FreeInstances.Add(slot);
                foreach (var position in positions) owner.SetCell(position, cells[position]);
                if (instances.Count != 0 || palettes.Count != 0) owner.Changed();
                foreach (var brick in orderedBricks) touched.Add(brick);
            }
            static int Compare(int3 a, int3 b)
            { int c = a.z.CompareTo(b.z); if (c != 0) return c; c = a.y.CompareTo(b.y); return c != 0 ? c : a.x.CompareTo(b.x); }
        }
    }
}
