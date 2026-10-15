using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    internal partial struct CellWriter
    {
        /// <summary>SPEC-13 §5.3.1: one heal visit; the request's instance set survives budget continuations.</summary>
        public bool HealCell(int3 position, float fraction, ref NativeHashSet<int> healedInstances)
        {
            if (!Access.TryGetCell(position, out var cell, out var brick, out int local)) return false;
            bool changed = false;
            if (cell.IsInstance && healedInstances.Add(cell.InstanceIndex))
            {
                var instance = Access.Instances[cell.InstanceIndex];
                if (instance.Block != 0 && instance.CellsAlive > 0 && (instance.Flags & BlockInstanceFlags.Destroyed) == 0)
                {
                    float maximum = Access.Blocks.Value.Blocks[instance.Block].FunctionHp;
                    float hp = math.min(maximum, instance.FunctionHp + fraction * maximum);
                    changed = hp != instance.FunctionHp; instance.FunctionHp = hp;
                    Access.Instances[cell.InstanceIndex] = instance;
                }
            }
            byte restored = (byte)math.min(255, cell.Hp + (int)math.ceil(fraction * 255));
            if (restored != cell.Hp)
            {
                bool sameBin = (restored >> 6) == (cell.Hp >> 6); cell.Hp = restored;
                changed |= Access.Write(brick, local, cell, sameBin);
                if (!sameBin) Touch(brick, Access.Slot(position), BrickDirtyFlags.Mesh);
            }
            Changed |= changed; return changed;
        }

        /// <summary>SPEC-13 §5.3.1: palette-only placement never creates structural ECS entities.</summary>
        public bool PlaceCell(in CellRestoreRequest request, out CellRestored restored)
        {
            restored = default; int3 position = request.CellMin;
            if (request.Block == 0 || request.Block >= Access.Blocks.Value.Blocks.Length ||
                math.any(position < Access.Min) || math.any(position >= Access.Max)) return false;
            ref var definition = ref Access.Blocks.Value.Blocks[request.Block];
            if (definition.RequiresInstance || request.Orient >= 24 ||
                (definition.AllowedOrientations & (1u << request.Orient)) == 0) return false;
            int slot = Access.Slot(position); Entity brick = Access.Slots[slot].Brick;
            if (brick == Entity.Null || Access.TryGetCell(position, out _, out _, out _)) return false;
            ushort palette = 0;
            for (int i = 1; i < Access.Palette.Length; i++)
            {
                var entry = Access.Palette[i];
                if (entry.Block == request.Block && entry.Orient == request.Orient && entry.Color == request.Color)
                { palette = (ushort)i; break; }
            }
            if (palette == 0)
            {
                if (Access.Palette.Length > Cell.MaxPaletteIndex) return false;
                palette = (ushort)Access.Palette.Length;
                Access.Palette.Add(new PaletteEntry { Block = request.Block, Orient = request.Orient, Color = request.Color });
            }
            if (!Access.Write(brick, BrickMath.LocalIndex(BrickMath.LocalOf(position)), Cell.Palette(palette))) return false;
            Changed = true; Touch(brick, slot, BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider);
            TouchNeighbours(position);
            restored = new CellRestored { Grid = Access.Grid, Cell = position, Block = request.Block, Color = request.Color };
            return true;
        }
    }
}
