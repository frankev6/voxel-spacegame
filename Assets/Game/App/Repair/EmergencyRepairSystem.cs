using System.Collections.Generic;
using Game.App.Session;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Economy;
using Game.Simulation.Abilities;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.Replica;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.App.Repair
{
    /// <summary>ABL-006 emergency repairs, the decision (authority only, main thread; repairs are rare). Each tick it writes Blocked
    /// on every EmergencyRepair slot (no design to rebuild towards, nothing missing, or a pilot with no hull plating in the hold).
    /// When one activates (energy already granted by AbilitySystem) it plans the rebuild (RepairPlanner), takes the hull items a
    /// pilot pays with (one per HullCellsPerItem cells, drawn at random from the hold's hull stacks, weighted by count, seeded by
    /// tick and ship), heals every cell and live block by HealFraction (the restore channel's Heal), and pours the plan over the
    /// ability's duration: cells into surviving bricks as exact writes, cells of lost bricks as a GridBrickFillRequest, and each
    /// rebuilt block's function once all its cells are back. Guests receive the result as ordinary GridDelta state.
    /// A ship's design: its fleet record's committed design (players), else the immutable blueprint or source it spawned from.</summary>
    [UpdateInGroup(typeof(AbilityActivationGroup)), UpdateAfter(typeof(AbilitySystem))]
    public partial class EmergencyRepairSystem : SystemBase
    {
        sealed class Channel
        {
            public RepairPlan Plan; public int Next; public uint Start, End; public bool Notified;
        }
        readonly Dictionary<Entity, Channel> channels = new();
        readonly Dictionary<VoxelStructure, RepairPlanner.Design> designs = new();
        readonly List<ItemDefinition> hullItems = new();
        ContentCatalog hullCatalog;
        EntityQuery roots;
        const uint ReviveGraceTicks = 60;

        protected override void OnCreate()
        {
            roots = GetEntityQuery(ComponentType.ReadWrite<AbilitySlot>(), ComponentType.ReadOnly<AbilityState>(), ComponentType.ReadOnly<VoxelGrid>());
            RequireForUpdate<GameplayActive>(); RequireForUpdate<AbilityDefsRef>(); RequireForUpdate<SimClock>();
            RequireForUpdate<DamageStreams>(); RequireForUpdate<BlockDefsRef>(); RequireForUpdate<OrientationTablesRef>();
        }

        protected override void OnUpdate()
        {
            var em = EntityManager;
            var defs = SystemAPI.GetSingleton<AbilityDefsRef>().Value;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            em.CompleteDependencyBeforeRW<AbilitySlot>(); em.CompleteDependencyBeforeRO<BlockInstance>(); em.CompleteDependencyBeforeRO<VoxelGrid>();
            int hull = HullPlating(out var cargo);
            using var entities = roots.ToEntityArray(Allocator.Temp);
            foreach (var root in entities)
            {
                if (em.HasComponent<Hulk>(root) || em.HasComponent<GridSpawning>(root) && em.IsComponentEnabled<GridSpawning>(root)) continue;
                var slots = em.GetBuffer<AbilitySlot>(root);
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot.AbilityIndex == 0 || slot.AbilityIndex >= defs.Value.Abilities.Length) continue;
                    ref var def = ref defs.Value.Abilities[slot.AbilityIndex];
                    if (def.Kind != AbilityKind.EmergencyRepair) continue;
                    bool pays = def.HullCellsPerItem > 0 && em.HasComponent<Pilot>(root);
                    var design = DesignOf(root);
                    bool activated = (slot.Flags & AbilitySlotFlags.ActivatedThisTick) != 0;
                    slot.Blocked = (byte)(design == null || pays && hull == 0 || !channels.ContainsKey(root) && !activated && !Damaged(root, design) ? 1 : 0);
                    slots = em.GetBuffer<AbilitySlot>(root); slots[i] = slot;
                    if (activated && design != null && !channels.ContainsKey(root))
                        Begin(root, design, ref def, pays ? hull * def.HullCellsPerItem : int.MaxValue, pays ? cargo : null, tick);
                }
            }
            if (channels.Count == 0) return;
            em.CompleteAllTrackedJobs();
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            using var exact = new NativeList<ExactCellWrite>(256, Allocator.Temp);
            var done = new List<Entity>();
            foreach (var pair in channels)
            {
                var root = pair.Key; var channel = pair.Value;
                if (!em.Exists(root) || em.HasComponent<Hulk>(root) || !em.HasComponent<VoxelGrid>(root)) { done.Add(root); continue; }
                if (Pour(root, channel, tick, exact)) done.Add(root);
            }
            foreach (var root in done) channels.Remove(root);
            if (exact.Length > 0) streams.QueueExact(exact.AsArray());
        }

        // ------------------------------------------------------------------------------------------------------------- activation
        void Begin(Entity root, VoxelStructure source, ref AbilityDef def, int cellCap, InventoryModel cargo, uint tick)
        {
            var em = EntityManager;
            var blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; var orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value;
            var live = new LiveGrid(em, root);
            var plan = RepairPlanner.Plan(DesignCells(source), live, ref blocks.Value, ref orientations.Value, def.RepairFraction, cellCap);
            ulong id = em.HasComponent<PersistentId>(root) ? em.GetComponentData<PersistentId>(root).Value : 0;
            ulong pilot = em.HasComponent<Pilot>(root) && !em.HasComponent<PlayerShip>(root) ? id : 0;
            string paid = "";
            if (cargo != null && plan.Cells > 0) paid = Pay(cargo, (plan.Cells + def.HullCellsPerItem - 1) / def.HullCellsPerItem, StableHash.Combine(id, tick));
            if (def.HealFraction > 0)
            {
                var grid = em.GetComponentData<VoxelGrid>(root);
                SystemAPI.GetSingleton<DamageStreams>().Restores.PostFromMainThread(new CellRestoreRequest { Grid = root, Mode = CellRestoreMode.Heal,
                    CellMin = grid.CellMin, CellMax = grid.CellMax, HpFraction = def.HealFraction, Producer = ProducerKind.Repair, Sequence = tick });
            }
            if (plan.Steps.Count > 0) channels[root] = new Channel { Plan = plan, Start = tick, End = tick + math.max(1u, def.DurationTicks) };
            string text = plan.Steps.Count == 0 ? "Repairs: hull patched; nothing missing could be rebuilt."
                : "Repairs: " + (plan.Engines > 0 ? plan.Engines + (plan.Engines == 1 ? " engine, " : " engines, ") : "") +
                  (plan.Blocks > 0 ? plan.Blocks + (plan.Blocks == 1 ? " block, " : " blocks, ") : "") + plan.Cells + " cells" + paid + ".";
            if (em.HasComponent<Pilot>(root)) EventBus<EconomyNotice>.Publish(new EconomyNotice(EconomyNoticeKind.RepairDone, text, pilot));
            GameLog.Info("Repair", "ship " + id + ": " + text + " (" + plan.Missing + " missing)");
        }

        /// <summary>Takes `items` hull items, each drawn from the hull stacks weighted by count; returns ", used 3 Hull, 1 Hull Slope".</summary>
        string Pay(InventoryModel cargo, int items, ulong seed)
        {
            var random = Random.CreateFromIndex((uint)(seed ^ (seed >> 32)));
            var taken = new SortedDictionary<string, int>();
            for (int n = 0; n < items; n++)
            {
                int total = 0; foreach (var item in hullItems) total += cargo.CountOf(item.Id);
                if (total == 0) break;
                int pick = random.NextInt(total);
                foreach (var item in hullItems)
                {
                    int count = cargo.CountOf(item.Id);
                    if (pick >= count) { pick -= count; continue; }
                    if (cargo.TryTake(item, 1)) { string name = string.IsNullOrEmpty(item.DisplayName) ? item.IdString : item.DisplayName; taken[name] = (taken.TryGetValue(name, out int had) ? had : 0) + 1; }
                    break;
                }
            }
            if (taken.Count == 0) return "";
            var parts = new List<string>(); foreach (var pair in taken) parts.Add(pair.Value + " " + pair.Key);
            return ", used " + string.Join(", ", parts);
        }

        int HullPlating(out InventoryModel cargo)
        {
            cargo = null;
            if (!Game.Core.Services.TryGet<Game.Data.Economy.ICargo>(out var hold) || hold is not InventoryModel model || !Game.Core.Services.TryGet<ContentService>(out var content)) return 0;
            cargo = model;
            if (!ReferenceEquals(hullCatalog, content.Catalog))
            {
                hullCatalog = content.Catalog; hullItems.Clear();
                foreach (var block in content.Catalog.All<BlockDefinition>())
                    if (block.IdString.StartsWith("block_hull", System.StringComparison.Ordinal)) hullItems.Add(block);
            }
            int total = 0; foreach (var item in hullItems) total += model.CountOf(item.Id);
            return total;
        }

        // ---------------------------------------------------------------------------------------------------------------- channel
        /// <summary>Applies this tick's share of the plan. True when finished.</summary>
        bool Pour(Entity root, Channel channel, uint tick, NativeList<ExactCellWrite> exact)
        {
            var em = EntityManager; var steps = channel.Plan.Steps;
            uint span = channel.End - channel.Start;
            int due = tick >= channel.End ? steps.Count : (int)math.ceil(steps.Count * (double)(tick - channel.Start + 1) / span);
            var live = new LiveGrid(em, root);
            var palette = em.GetBuffer<PaletteEntry>(root);
            using var fill = new NativeList<GridBrickFillCell>(64, Allocator.Temp);
            bool revive = false;
            var blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; var orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value;
            while (channel.Next < due)
            {
                var step = steps[channel.Next];
                if (step.Type == RepairStep.Kind.Revive)
                {
                    var instances = em.GetBuffer<BlockInstance>(root);
                    if ((uint)step.Instance >= (uint)instances.Length) { channel.Next++; continue; }
                    var instance = instances[step.Instance];
                    ref var def = ref blocks.Value.Blocks[instance.Block];
                    int present = 0;
                    for (int c = 0; c < def.Cells.Length; c++)
                    {
                        var cell = live.CellAt(instance.Origin + orientations.Value.Rotate(def.Cells[c], instance.Orient));
                        if (cell.IsInstance && cell.InstanceIndex == step.Instance) present++;
                    }
                    if (present < def.Cells.Length && tick < channel.End + ReviveGraceTicks) break;   // its cells land first
                    if (present == def.Cells.Length)
                    {
                        exact.Add(new ExactCellWrite { Grid = root, Kind = ExactWriteKind.Instance, Instance = step.Instance,
                            FunctionHp = def.FunctionHp, CellsAlive = (ushort)present, Flags = BlockInstanceFlags.None });
                        revive |= def.Kind == BlockKind.Functional;
                    }
                    channel.Next++; continue;
                }
                channel.Next++;
                if (!live.CellAt(step.Cell).IsAir) continue;   // already back (or something else is there)
                Cell value;
                if (step.Type == RepairStep.Kind.InstanceCell) value = Cell.Instance(step.Instance);
                else
                {
                    int index = -1;
                    for (int p = 1; p < palette.Length; p++)
                        if (palette[p].Block == step.Palette.Block && palette[p].Orient == step.Palette.Orient && palette[p].Color == step.Palette.Color) { index = p; break; }
                    if (index < 0)
                    {
                        if (step.Palette.Block == 0 || palette.Length > Cell.MaxPaletteIndex) continue;
                        index = palette.Length; palette.Add(step.Palette);
                        palette = em.GetBuffer<PaletteEntry>(root);
                    }
                    value = Cell.Palette((ushort)index);
                }
                if (GridBrickFillApi.HasBrick(em, root, step.Cell))
                    exact.Add(new ExactCellWrite { Grid = root, Kind = ExactWriteKind.Cell, Cell = step.Cell, Value = value, Reason = CellRemovedReason.Destroyed });
                else fill.Add(new GridBrickFillCell { Cell = step.Cell, Value = value });
            }
            if (fill.Length > 0 || revive) GridBrickFillApi.Request(em, root, fill.AsArray(), revive);
            return channel.Next >= steps.Count;
        }

        // ---------------------------------------------------------------------------------------------------------------- designs
        VoxelStructure DesignOf(Entity root)
        {
            var em = EntityManager;
            if (em.HasComponent<PersistentId>(root) && Game.Core.Services.TryGet<GameSession>(out var session) && session.HasActiveSession)
            {
                var record = session.Fleet.Find(em.GetComponentData<PersistentId>(root).Value);
                if (record?.Design != null && record.Design.Data.IsCreated) return record.Design;
            }
            if (!em.HasComponent<GridSource>(root) || !SystemAPI.HasSingleton<VoxelStructureRegistryRef>()) return null;
            var source = em.GetComponentData<GridSource>(root);
            if ((source.Flags & GridSpawnFlags.WriteBack) != 0) return null;   // its source takes the damage; not a design
            var registry = SystemAPI.GetSingleton<VoxelStructureRegistryRef>().Value.Value;
            if (registry == null) return null;
            var structure = source.StructureHandle >= 0 ? registry.Get(source.StructureHandle) : registry.TryGetBlueprint(source.Blueprint, out var blueprint) ? blueprint : null;
            return structure != null && structure.Data.IsCreated ? structure : null;
        }

        /// <summary>Something to rebuild: fewer live cells than the design, or a block destroyed in place.</summary>
        bool Damaged(Entity root, VoxelStructure design)
        {
            var em = EntityManager;
            if (em.HasComponent<Game.Voxel.PhysicsBody.GridMass>(root) && em.GetComponentData<Game.Voxel.PhysicsBody.GridMass>(root).Data.CellCount < (uint)design.SolidCellCount) return true;
            foreach (var instance in em.GetBuffer<BlockInstance>(root, true))
                if (instance.Block != 0 && (instance.Flags & BlockInstanceFlags.Destroyed) != 0) return true;
            return false;
        }

        RepairPlanner.Design DesignCells(VoxelStructure structure)
        {
            if (designs.TryGetValue(structure, out var cached)) return cached;
            if (designs.Count > 64) designs.Clear();
            var design = RepairPlanner.FromStructure(structure);
            designs[structure] = design; return design;
        }

        /// <summary>The live grid read on the main thread, a brick at a time.</summary>
        sealed class LiveGrid : RepairPlanner.ILive
        {
            readonly EntityManager em; readonly Entity root; readonly VoxelGrid grid;
            readonly Dictionary<int3, Cell[]> bricks = new();
            public LiveGrid(EntityManager em, Entity root)
            { this.em = em; this.root = root; grid = em.GetComponentData<VoxelGrid>(root); }
            public Cell CellAt(int3 cell)
            {
                int3 coord = BrickMath.BrickOf(cell);
                if (!bricks.TryGetValue(coord, out var cells))
                {
                    int slot = BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims); cells = null;
                    if (slot >= 0)
                    {
                        var brick = em.GetBuffer<BrickSlot>(root, true)[slot].Brick;
                        if (brick != Entity.Null && em.Exists(brick))
                        {
                            using var read = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Temp);
                            ReplicaVoxelApi.ReadBrick(em, brick, read); cells = read.ToArray();
                        }
                    }
                    bricks[coord] = cells;
                }
                return cells == null ? default : cells[BrickMath.LocalIndex(BrickMath.LocalOf(cell))];
            }
            public int InstanceCount => em.GetBuffer<BlockInstance>(root, true).Length;
            public BlockInstance Instance(int index) => em.GetBuffer<BlockInstance>(root, true)[index];
        }
    }
}
