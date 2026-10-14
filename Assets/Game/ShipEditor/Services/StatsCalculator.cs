using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.ShipEditor.Data;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §5.8: canonical count-driven totals and exact incremental mass moments.
    /// No UI dependency: the App presenter formats numerical rows into its existing view models.</summary>
    public sealed class StatsCalculator
    {
        readonly EditorContext context;
        long massMilli, momentX, momentY, momentZ;
        internal StatsCalculator(EditorContext context) => this.context = context;
        internal void AddInitial(BlockRecord record) => Add(record, 1);
        internal void CopyFrom(StatsCalculator source)
        { massMilli = source.massMilli; momentX = source.momentX; momentY = source.momentY; momentZ = source.momentZ; }
        internal void Apply(DiffCommand command, bool forward)
        {
            for (int i = 0; i < command.Ops.Length; i++)
            {
                var op = command.Ops[forward ? i : command.Ops.Length - 1 - i];
                Add(op.Record, (op.Kind == DiffOpKind.Add) == forward ? 1 : -1);
            }
        }
        void Add(BlockRecord record, int sign)
        {
            var contribution = BlockStatMath.Of(context.Catalog.GetBlock(record.Block), record.Orient);
            var footprint = context.Footprints.Get(record.Block, record.Orient);
            long mass = checked((long)Math.Round((double)contribution.Mass * 1000, MidpointRounding.ToEven)) * sign;
            checked
            {
                massMilli += mass;
                momentX += mass * (2L * record.Origin.x + footprint.Min.x + footprint.Max.x + 1);
                momentY += mass * (2L * record.Origin.y + footprint.Min.y + footprint.Max.y + 1);
                momentZ += mass * (2L * record.Origin.z + footprint.Min.z + footprint.Max.z + 1);
            }
        }
        internal StatTotals Calculate(BlockCountTables counts, VoxelStructure source)
        {
            var total = new StatTotals { Cells = source.SolidCellCount };
            var pairs = counts.Orientations;
            // Hash-map order may change after remove/undo. Fixed key order makes every sum bit-identical.
            using var keys = pairs.GetKeyArray(Allocator.Temp); keys.Sort();
            foreach (int key in keys)
            {
                int block = key / 24; byte orient = (byte)(key % 24); int count = pairs[key];
                var value = BlockStatMath.Of(context.Catalog.GetBlock((ushort)block), orient);
                total.Blocks += count; total.MassKg += count * (double)value.Mass;
                total.ForwardThrustN += count * (double)value.Thrust.z;
                total.PowerGen += count * (double)value.PowerGeneration; total.PowerUse += count * (double)value.PowerDraw;
                total.PowerStorage += count * (double)value.PowerStorage; total.Dps += count * (double)value.SustainedDps;
                total.ShieldCapacity += count * (double)value.ShieldCapacity; total.ShieldRegen += count * (double)value.ShieldRegen;
                total.CargoUnits += count * (double)value.CargoUnits; total.Cost += count * (long)context.Items.PriceOf(block);
            }
            total.CenterOfMassCells = massMilli == 0 ? double3.zero : new double3(momentX, momentY, momentZ) / (2.0 * massMilli);
            return total;
        }

        /// <summary>Numerical row values are in the named display units; Delta = draft − original.
        /// DeltaSign is the improvement sign, independent of formatting or colour.</summary>
        public readonly struct Row
        {
            public readonly string Id, Label, Unit;
            public readonly double Value, Delta;
            public readonly int Decimals, DeltaSign;
            internal Row(string id, string label, string unit, int decimals, double original, double draft, int improvementDirection)
            {
                Id = id; Label = label; Unit = unit; Decimals = decimals; Value = draft; Delta = draft - original;
                DeltaSign = Math.Sign(Delta) * improvementDirection;
            }
        }
        /// <summary>Fill the eleven rows specified by §5.8 into caller-owned storage.
        /// Core centres come from the caller's current original/draft core lookup; use grid origin when absent.</summary>
        public static void GetRows(in StatTotals original, in StatTotals draft, double3 originalCoreCells, double3 draftCoreCells,
            double cellSize, List<Row> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            output.Add(new Row("mass", "Mass", "t", 1, original.MassKg / 1000, draft.MassKg / 1000, -1));
            output.Add(new Row("blocks", "Blocks", "", 0, original.Blocks, draft.Blocks, 0));
            output.Add(new Row("forward-thrust", "Forward thrust", "kN", 1, original.ForwardThrustN / 1000, draft.ForwardThrustN / 1000, 1));
            output.Add(new Row("thrust-to-weight", "T/W", "", 2, original.ThrustToWeight, draft.ThrustToWeight, 1));
            output.Add(new Row("acceleration", "Acceleration", "m/s²", 1,
                original.MassKg > 0 ? original.ForwardThrustN / original.MassKg : 0,
                draft.MassKg > 0 ? draft.ForwardThrustN / draft.MassKg : 0, 1));
            output.Add(new Row("power-net", "Power net", "E/s", 1, original.PowerGen - original.PowerUse, draft.PowerGen - draft.PowerUse, 1));
            output.Add(new Row("power-storage", "Power storage", "E", 1, original.PowerStorage, draft.PowerStorage, 1));
            output.Add(new Row("dps", "DPS", "HP/s", 1, original.Dps, draft.Dps, 1));
            output.Add(new Row("shield", "Shield", "HP", 1, original.ShieldCapacity, draft.ShieldCapacity, 1));
            output.Add(new Row("cost", "Cost", "credits", 0, original.Cost, draft.Cost, -1));
            output.Add(new Row("com-offset", "CoM offset", "m", 1,
                math.length(original.CenterOfMassCells - originalCoreCells) * cellSize,
                math.length(draft.CenterOfMassCells - draftCoreCells) * cellSize, -1));
        }
    }
}
