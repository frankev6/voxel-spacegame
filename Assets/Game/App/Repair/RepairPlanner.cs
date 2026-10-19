using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.App.Repair
{
    /// <summary>ABL-006: one rebuild step, in the order the channel applies them. A cell step writes a design cell back (a palette cell
    /// by its design palette entry, an instance cell by instance index); a revive step restores that instance's function once its
    /// cells are all present.</summary>
    internal struct RepairStep
    {
        public enum Kind : byte { Palette = 0, InstanceCell = 1, Revive = 2 }
        public Kind Type;
        public int3 Cell;
        public PaletteEntry Palette;   // Palette
        public int Instance;           // InstanceCell, Revive
    }

    internal sealed class RepairPlan
    {
        public readonly List<RepairStep> Steps = new();
        public int Cells;        // cells rebuilt (and destroyed-in-place blocks' cells revived): what the hull items pay for
        public int Missing;      // design cells missing when planned, the base of the repair fraction
        public int Engines, Blocks;
    }

    /// <summary>ABL-006 planner, pure and deterministic (main thread, at activation). Missing = design cells that are air on the live
    /// grid, plus the cells of functional blocks destroyed in place. The budget is ceil(fraction × missing) cells, capped by what the
    /// pilot's hull items pay for. Order: missing engines (facing the direction with the fewest live engines first, then instance index;
    /// the first engine is allowed past the fraction), then power, shields, weapons, other functional blocks and multi-cell blocks,
    /// then hull cells breadth-first outward from what is attached. Every rebuilt cell touches a live or earlier rebuilt cell: a block
    /// cut off from the ship brings the shortest path of missing hull cells to it. The core is never rebuilt.</summary>
    internal static class RepairPlanner
    {
        internal const int SearchLimit = 20000;
        static readonly int3[] Faces = { new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1) };

        /// <summary>The live grid as the planner sees it.</summary>
        internal interface ILive
        {
            Cell CellAt(int3 cell);
            int InstanceCount { get; }
            BlockInstance Instance(int index);
        }

        internal readonly struct Design
        {
            public readonly IReadOnlyList<(int3 Cell, Cell Value)> Cells;   // every solid design cell
            public readonly IReadOnlyList<PaletteEntry> Palette;
            public readonly IReadOnlyList<BlockInstance> Instances;
            public Design(IReadOnlyList<(int3, Cell)> cells, IReadOnlyList<PaletteEntry> palette, IReadOnlyList<BlockInstance> instances)
            { Cells = cells; Palette = palette; Instances = instances; }
        }

        /// <summary>Every solid cell of a structure with its palette and instances.</summary>
        internal static Design FromStructure(VoxelStructure structure)
        {
            var cells = new List<(int3, Cell)>();
            foreach (var record in structure.Data.Bricks)
                for (int i = 0; i < BrickMath.CellsPerBrick; i++)
                {
                    int3 cell = BrickMath.CellOf(record.Coord, i); var value = structure.GetCell(cell);
                    if (!value.IsAir) cells.Add((cell, value));
                }
            var palette = new List<PaletteEntry>(); foreach (var entry in structure.Data.Palette) palette.Add(entry);
            var instances = new List<BlockInstance>(); foreach (var instance in structure.Data.Instances) instances.Add(instance);
            return new Design(cells, palette, instances);
        }

        /// <param name="cellCap">Most cells the pilot can pay for; int.MaxValue for AI ships.</param>
        internal static RepairPlan Plan(Design design, ILive live, ref BlockDefsBlob blocks, ref OrientationTablesBlob orientations,
            float fraction, int cellCap)
        {
            var plan = new RepairPlan();
            var missing = new Dictionary<int3, Cell>();
            foreach (var (cell, value) in design.Cells) if (live.CellAt(cell).IsAir) missing[cell] = value;
            // Instance units: a design instance the live grid still has (same block, origin and orientation) that lost cells or function.
            var units = new List<(int Class, float Order, int Index, List<int3> Cells, bool Revive, int Cost)>();
            var instanceCells = new HashSet<int3>();
            var liveThrust = new float[6];
            for (int i = 0; i < live.InstanceCount; i++)
            {
                var instance = live.Instance(i);
                if (instance.Block == 0 || instance.Block >= blocks.Blocks.Length || (instance.Flags & BlockInstanceFlags.Destroyed) != 0 || instance.CellsAlive == 0) continue;
                ref var def = ref blocks.Blocks[instance.Block];
                if (def.HasModule(BlockModuleId.Thruster)) liveThrust[Axis(orientations.Rotate(new float3(0, 0, -1), instance.Orient))] += 1;
            }
            int destroyedInPlace = 0;
            for (int i = 0; i < design.Instances.Count && i < live.InstanceCount; i++)
            {
                var want = design.Instances[i]; var have = live.Instance(i);
                if (want.Block == 0 || want.Block >= blocks.Blocks.Length || have.Block != want.Block || !have.Origin.Equals(want.Origin) || have.Orient != want.Orient) continue;
                ref var def = ref blocks.Blocks[want.Block];
                var footprint = new List<int3>(); var lost = new List<int3>();
                for (int c = 0; c < def.Cells.Length; c++)
                {
                    int3 cell = want.Origin + orientations.Rotate(def.Cells[c], want.Orient);
                    footprint.Add(cell); instanceCells.Add(cell);
                    if (missing.ContainsKey(cell)) lost.Add(cell);
                }
                bool functional = def.Kind == BlockKind.Functional;
                bool dead = (have.Flags & BlockInstanceFlags.Destroyed) != 0 || have.CellsAlive < footprint.Count || functional && have.FunctionHp <= 0;
                if (lost.Count == 0 && !dead) continue;
                if (def.IsCore) continue;   // a lost core is death; never rebuilt
                int cost = lost.Count;
                if (functional && lost.Count == 0) { cost = footprint.Count; destroyedInPlace += footprint.Count; }   // rebuilt in place
                int unitClass = def.HasModule(BlockModuleId.Thruster) ? 0 : def.HasModule(BlockModuleId.PowerSource) || def.HasModule(BlockModuleId.PowerStorage) ? 1 :
                    def.HasModule(BlockModuleId.ShieldEmitter) ? 2 : def.HasModule(BlockModuleId.WeaponMount) ? 3 : functional ? 4 : 5;
                float order = unitClass == 0 ? liveThrust[Axis(orientations.Rotate(new float3(0, 0, -1), want.Orient))] : 0;
                units.Add((unitClass, order, i, lost, functional || dead, cost));
            }
            plan.Missing = missing.Count + destroyedInPlace;
            if (plan.Missing == 0) return plan;
            int budget = (int)math.ceil(math.saturate(fraction) * plan.Missing);
            units.Sort((a, b) => a.Class != b.Class ? a.Class.CompareTo(b.Class) : a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Index.CompareTo(b.Index));
            var planned = new HashSet<int3>();
            bool Solid(int3 c) => planned.Contains(c) || !live.CellAt(c).IsAir;
            bool Touches(int3 c) { foreach (var f in Faces) if (Solid(c + f)) return true; return false; }
            void AddCell(int3 cell, Cell value)
            {
                if (!planned.Add(cell)) return;
                if (value.IsInstance) plan.Steps.Add(new RepairStep { Type = RepairStep.Kind.InstanceCell, Cell = cell, Instance = value.InstanceIndex });
                else plan.Steps.Add(new RepairStep { Type = RepairStep.Kind.Palette, Cell = cell,
                    Palette = value.PaletteIndex < design.Palette.Count ? design.Palette[value.PaletteIndex] : default });
            }
            bool firstEngine = true; int spent = 0;
            foreach (var unit in units)
            {
                var bridge = unit.Cells.Count == 0 ? new List<int3>() : Bridge(unit.Cells, missing, instanceCells, planned, Solid, Touches);
                if (bridge == null) continue;   // nothing connects it to the ship
                int cost = unit.Cost + bridge.Count;
                bool allowed = spent + cost <= budget || unit.Class == 0 && firstEngine;
                if (!allowed || spent + cost > cellCap) { if (unit.Class == 0) firstEngine = false; continue; }
                foreach (var cell in bridge) AddCell(cell, missing[cell]);
                // the unit's own cells, attached ones first so each touches something solid when it lands
                var pending = new List<int3>(unit.Cells);
                while (pending.Count > 0)
                {
                    int at = pending.FindIndex(Touches); if (at < 0) at = 0;
                    var cell = pending[at]; pending.RemoveAt(at); AddCell(cell, missing[cell]);
                }
                if (unit.Revive) plan.Steps.Add(new RepairStep { Type = RepairStep.Kind.Revive, Instance = unit.Index });
                spent += cost; plan.Cells += cost;
                if (unit.Class == 0) { plan.Engines++; firstEngine = false; } else plan.Blocks++;
            }
            // hull: breadth-first outward from what is attached, in coordinate order within each layer
            var frontier = new List<int3>();
            foreach (var pair in missing) if (!pair.Value.IsInstance && !instanceCells.Contains(pair.Key) && !planned.Contains(pair.Key) && Touches(pair.Key)) frontier.Add(pair.Key);
            frontier.Sort(Compare);
            var queued = new HashSet<int3>(frontier);
            for (int head = 0; head < frontier.Count && spent < budget && spent < cellCap; head++)
            {
                var cell = frontier[head]; AddCell(cell, missing[cell]); spent++; plan.Cells++;
                var next = new List<int3>();
                foreach (var f in Faces)
                {
                    var n = cell + f;
                    if (missing.TryGetValue(n, out var value) && !value.IsInstance && !instanceCells.Contains(n) && !planned.Contains(n) && queued.Add(n)) next.Add(n);
                }
                next.Sort(Compare); frontier.AddRange(next);
            }
            return plan;
        }

        /// <summary>The shortest path of missing palette cells joining `unit` to the solid ship, unit-side first reversed so it lands
        /// from the ship outwards; empty when the unit already touches it; null when none is found within the search limit.</summary>
        static List<int3> Bridge(List<int3> unit, Dictionary<int3, Cell> missing, HashSet<int3> instanceCells, HashSet<int3> planned,
            Func<int3, bool> solid, Func<int3, bool> touches)
        {
            foreach (var cell in unit) if (touches(cell)) return new List<int3>();
            var from = new Dictionary<int3, int3>(); var queue = new Queue<int3>(); var start = new HashSet<int3>(unit);
            var seeds = new List<int3>(unit); seeds.Sort(Compare);
            foreach (var cell in seeds) { queue.Enqueue(cell); from[cell] = cell; }
            while (queue.Count > 0 && from.Count < SearchLimit)
            {
                var cell = queue.Dequeue();
                foreach (var f in Faces)
                {
                    var n = cell + f;
                    if (from.ContainsKey(n) || !missing.TryGetValue(n, out var value) || value.IsInstance || instanceCells.Contains(n) || planned.Contains(n)) continue;
                    from[n] = cell;
                    if (touches(n))
                    {
                        var path = new List<int3>();
                        for (var at = n; !start.Contains(at); at = from[at]) path.Add(at);
                        return path;   // ship side first
                    }
                    queue.Enqueue(n);
                }
            }
            return null;
        }

        static int Axis(float3 d)
        {
            var a = math.abs(d);
            if (a.x >= a.y && a.x >= a.z) return d.x >= 0 ? 0 : 1;
            if (a.y >= a.z) return d.y >= 0 ? 2 : 3;
            return d.z >= 0 ? 4 : 5;
        }
        static int Compare(int3 a, int3 b) => a.z != b.z ? a.z.CompareTo(b.z) : a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x);
    }
}
