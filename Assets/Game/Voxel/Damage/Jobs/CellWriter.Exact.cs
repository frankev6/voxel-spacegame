using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    internal partial struct CellWriter
    {
        /// <summary>ADR-0117: sets one cell to a value decided elsewhere, with the same consequences as every other write here. A cell
        /// becoming air goes through <see cref="RemoveCell"/>; a placement marks the collider and neighbours like
        /// <see cref="PlaceCell"/>. Returns true when the cell changed; <paramref name="missing"/> when a solid value has no brick.</summary>
        public bool ApplyExactCell(int3 c, Cell value, CellRemovedReason reason, out CellRestored placed, out bool missing)
        {
            placed = default; missing = false;
            if (math.any(c < Access.Min) || math.any(c >= Access.Max)) { missing = !value.IsAir; return false; }
            int slot = Access.Slot(c); Entity brick = Access.Slots[slot].Brick;
            if (brick == Entity.Null) { missing = !value.IsAir; return false; }
            Access.TryGetCell(c, out var old, out _, out int local);   // old is air when the cell is empty
            if (old.Equals(value)) return false;
            if (value.IsAir) return RemoveCell(c, reason);
            bool wasAir = old.IsAir;
            bool sameBin = !wasAir && old.Ref == value.Ref && (old.Hp >> 6) == (value.Hp >> 6);
            if (!Access.Write(brick, local, value, sameBin)) return false;
            Changed = true;
            if (wasAir)
            {
                Touch(brick, slot, BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider); TouchNeighbours(c);
                var appearance = Access.Appearance(value);
                placed = new CellRestored { Grid = Access.Grid, Cell = c, Block = appearance.Block, Color = appearance.Color };
            }
            else if (!sameBin) Touch(brick, slot, BrickDirtyFlags.Mesh);
            return true;
        }

        /// <summary>ADR-0117: sets an instance's health to values decided elsewhere. When it stops working here (destroyed, or no cells
        /// left because they went to another body), its functional entity joins the destroy list, as <see cref="CheckInstance"/> does.</summary>
        public void ApplyExactInstance(int index, float functionHp, ushort cellsAlive, BlockInstanceFlags flags)
        {
            if ((uint)index >= (uint)Access.Instances.Length) return;
            var instance = Access.Instances[index];
            bool gone = (flags & BlockInstanceFlags.Destroyed) != 0 || cellsAlive == 0;
            if (gone && instance.Functional != Entity.Null)
            {
                DestroyList.Add(instance.Functional);
                instance.Functional = Entity.Null;   // queued once: later writes to this instance must not queue it again
            }
            else if (instance.FunctionHp == functionHp && instance.CellsAlive == cellsAlive && instance.Flags == flags) return;
            instance.FunctionHp = functionHp; instance.CellsAlive = cellsAlive; instance.Flags = flags;
            Access.Instances[index] = instance; Changed = true;
        }
    }
}
