using System;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Validation
{
    /// <summary>SPEC-51 §5.12: chiral footprint mirrors with canonical damage remapping.</summary>
    internal sealed class SymmetryMapper
    {
        readonly EditorContext context;
        internal SymmetryMapper(EditorContext context) => this.context = context ?? throw new ArgumentNullException(nameof(context));
        internal PlacementError Mirror(BlockRecord source, NativeArray<byte> damage, int axis, int plane,
            NativeList<byte> outputDamage, out BlockRecord result)
        {
            result = default;
            if ((uint)axis >= 3) throw new ArgumentOutOfRangeException(nameof(axis));
            if (source.Block == 0 || source.Block >= context.Footprints.BlockCount || source.Orient >= 24)
                return PlacementError.SymmetryUnsupported;
            ushort partner = context.Definitions.Value.Blocks[source.Block].MirrorIndex;
            if (partner == 0 || partner >= context.Footprints.BlockCount) return PlacementError.SymmetryUnsupported;
            byte orient = context.Orientations.Value.Mirror(source.Orient, axis);
            var from = context.Footprints.Get(source.Block, source.Orient); var to = context.Footprints.Get(partner, orient);
            if (from.Cells.Length != to.Cells.Length || from.Cells.Length == 0) return PlacementError.SymmetryUnsupported;
            int3 minimum = new int3(int.MaxValue);
            using var reflected = new NativeArray<int3>(from.Cells.Length, Allocator.Temp);
            var reflectedWrite = reflected;
            for (int i = 0; i < from.Cells.Length; i++)
            {
                var offset = from.Cells[i]; int3 cell = default;
                for (int a = 0; a < 3; a++)
                {
                    long coordinate = (long)source.Origin[a] + offset[a];
                    if (a == axis) coordinate = (long)plane - coordinate - 1;
                    if (coordinate < int.MinValue || coordinate > int.MaxValue) return PlacementError.SymmetryUnsupported;
                    cell[a] = (int)coordinate;
                }
                reflectedWrite[i] = cell; minimum = math.min(minimum, cell);
            }
            int3 origin = default;
            for (int a = 0; a < 3; a++)
            {
                long coordinate = (long)minimum[a] - to.Min[a];
                if (coordinate < int.MinValue || coordinate > int.MaxValue) return PlacementError.SymmetryUnsupported;
                origin[a] = (int)coordinate;
            }
            using var indices = new NativeHashMap<int3, int>(to.Cells.Length, Allocator.Temp);
            for (int i = 0; i < to.Cells.Length; i++) indices.Add(to.Cells[i], i);
            bool damaged = source.DamageOffset >= 0;
            if (source.DamageOffset < -1 || damaged && source.DamageOffset > damage.Length - from.Cells.Length - 4)
                throw new ArgumentException("EDT: mirror damage bytes are truncated.", nameof(damage));
            using var remapped = new NativeArray<byte>(damaged ? from.Cells.Length + 4 : 0, Allocator.Temp);
            var remappedWrite = remapped;
            for (int i = 0; i < reflected.Length; i++)
            {
                if (!indices.TryGetValue(reflected[i] - origin, out int target)) return PlacementError.SymmetryUnsupported;
                if (damaged) remappedWrite[target] = damage[source.DamageOffset + i];
            }
            result = source; result.Block = partner; result.Orient = orient; result.Origin = origin;
            result.DamageOffset = -1;
            if (damaged)
            {
                for (int i = 0; i < 4; i++) remappedWrite[from.Cells.Length + i] = damage[source.DamageOffset + from.Cells.Length + i];
                result.DamageOffset = outputDamage.Length; outputDamage.AddRange(remapped);
            }
            return PlacementError.None;
        }
        /// <summary>Appends one atomic symmetry group. On failure both caller-owned outputs retain their prior contents.</summary>
        internal PlacementError BuildCopies(BlockRecord source, NativeArray<byte> damage, SymmetrySettings settings,
            NativeList<BlockRecord> output, NativeList<byte> outputDamage)
        {
            int start = output.Length, bytes = outputDamage.Length;
            if (source.Block == 0 || source.Block >= context.Footprints.BlockCount || source.Orient >= 24)
                return PlacementError.SymmetryUnsupported;
            int enabled = (settings.MirrorX ? 1 : 0) | (settings.MirrorY ? 2 : 0) | (settings.MirrorZ ? 4 : 0);
            var first = source;
            if (source.DamageOffset >= 0)
            {
                int length = context.Footprints.Get(source.Block, source.Orient).Cells.Length + 4;
                if (source.DamageOffset > damage.Length - length) throw new ArgumentException("EDT: mirror damage bytes are truncated.", nameof(damage));
                using var copy = new NativeArray<byte>(length, Allocator.Temp);
                NativeArray<byte>.Copy(damage, source.DamageOffset, copy, 0, length);
                first.DamageOffset = outputDamage.Length; outputDamage.AddRange(copy);
            }
            else if (source.DamageOffset != -1) throw new ArgumentException("EDT: invalid mirror damage offset.", nameof(source));
            output.Add(first);
            // Identity, X, Y, Z, XY, XZ, YZ, XYZ. Masks 3 and 4 are intentionally swapped.
            for (int ordinal = 1; ordinal < 8; ordinal++)
            {
                int mask = ordinal == 3 ? 4 : ordinal == 4 ? 3 : ordinal;
                if ((mask & enabled) != mask) continue;
                int copyBytes = outputDamage.Length; var record = first;
                for (int axis = 0; axis < 3; axis++)
                {
                    if ((mask & (1 << axis)) == 0) continue;
                    int plane = axis == 0 ? settings.PlaneX2 : axis == 1 ? settings.PlaneY2 : settings.PlaneZ2;
                    var error = Mirror(record, outputDamage.AsArray(), axis, plane, outputDamage, out var mirrored);
                    if (error != PlacementError.None)
                    { output.ResizeUninitialized(start); outputDamage.ResizeUninitialized(bytes); return error; }
                    record = mirrored;
                }
                bool duplicate = false;
                for (int i = start; i < output.Length; i++)
                {
                    var previous = output[i];
                    if (previous.Block == record.Block && previous.Orient == record.Orient && previous.Origin.Equals(record.Origin))
                    { duplicate = true; break; }
                }
                if (duplicate) outputDamage.ResizeUninitialized(copyBytes); else output.Add(record);
            }
            return PlacementError.None;
        }
        internal PlacementError BuildRemovals(BlockRecord source, SymmetrySettings settings, VoxelStructure draft, NativeList<BlockRecord> output)
        {
            using var copies = new NativeList<BlockRecord>(Allocator.Temp); using var bytes = new NativeList<byte>(Allocator.Temp);
            source.DamageOffset = -1; var error = BuildCopies(source, default, settings, copies, bytes);
            if (error != PlacementError.None) return error;
            var reader = new DraftReader(draft, context);
            foreach (var copy in copies)
                foreach (var cell in context.Footprints.Get(copy.Block, copy.Orient).Cells)
                    if (reader.TryGetBlockAt(copy.Origin + cell, out var found) && found.Block == copy.Block && found.Orient == copy.Orient && found.Origin.Equals(copy.Origin))
                    { output.Add(found); break; } // Preserve the actual colour; the session captures actual damage.
            return PlacementError.None;
        }
        internal int DefaultPlane(int axis, BlockRecord? core)
        {
            if ((uint)axis >= 3) throw new ArgumentOutOfRangeException(nameof(axis));
            if (!core.HasValue) return 1;
            var record = core.Value; var footprint = context.Footprints.Get(record.Block, record.Orient);
            return checked(2 * record.Origin[axis] + footprint.Min[axis] + footprint.Max[axis] + 1);
        }
        internal void SetEnabled(ref SymmetrySettings settings, int axis, bool enabled, BlockRecord? core = null)
        {
            if ((uint)axis >= 3) throw new ArgumentOutOfRangeException(nameof(axis));
            bool previous = axis == 0 ? settings.MirrorX : axis == 1 ? settings.MirrorY : settings.MirrorZ;
            if (previous == enabled) return;
            int plane = enabled ? DefaultPlane(axis, core) : 0;
            if (axis == 0) { settings.MirrorX = enabled; if (enabled) settings.PlaneX2 = plane; }
            else if (axis == 1) { settings.MirrorY = enabled; if (enabled) settings.PlaneY2 = plane; }
            else { settings.MirrorZ = enabled; if (enabled) settings.PlaneZ2 = plane; }
            settings.Version++;
        }
        internal static void Nudge(ref SymmetrySettings settings, int axis, int halfCells)
        {
            if ((uint)axis >= 3) throw new ArgumentOutOfRangeException(nameof(axis));
            if (halfCells != -1 && halfCells != 1) throw new ArgumentOutOfRangeException(nameof(halfCells));
            if (axis == 0) settings.PlaneX2 = checked(settings.PlaneX2 + halfCells);
            else if (axis == 1) settings.PlaneY2 = checked(settings.PlaneY2 + halfCells);
            else settings.PlaneZ2 = checked(settings.PlaneZ2 + halfCells);
            settings.Version++;
        }
    }
}
