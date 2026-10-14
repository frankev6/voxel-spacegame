using System;
using Game.Core;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.App.Services
{
    /// <summary>SPEC-40 §5.5: translates narrative hull effects to the damage owner's request channels.</summary>
    internal sealed class NarrativePlayerDamage : IDisposable
    {
        readonly World world;
        readonly EntityQuery players, streams;
        readonly NarrativeConfigDefinition config;
        uint sequence;
        public NarrativePlayerDamage(World world, NarrativeConfigDefinition config)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            players = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<VoxelGrid>());
            streams = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<DamageStreams>());
        }
        bool TryPlayer(string operation, out Entity player, out VoxelGrid grid)
        {
            player = Entity.Null; grid = default;
            if (!world.IsCreated || players.CalculateEntityCount() != 1 || streams.IsEmptyIgnoreFilter)
            {
                GameLog.ErrorOnce("Narrative.Player." + operation, "Narrative", operation + " requires a live player grid and damage streams.");
                return false;
            }
            player = players.GetSingletonEntity();
            grid = world.EntityManager.GetComponentData<VoxelGrid>(player);
            return true;
        }
        internal void Repair(int percent)
        {
            if (percent <= 0 || !TryPlayer("RepairPlayerShip", out var player, out var grid)) return;
            streams.GetSingleton<DamageStreams>().Restores.PostFromMainThread(new CellRestoreRequest
            {
                Grid = player, Mode = CellRestoreMode.Heal, CellMin = grid.CellMin, CellMax = grid.CellMax,
                HpFraction = math.min(percent, 100) / 100f, Producer = ProducerKind.MainThread, Sequence = sequence++
            });
        }
        internal void Damage(int amount, ulong campaignSeed, double universalTime)
        {
            if (amount <= 0 || !TryPlayer("DamagePlayerShip", out var player, out var grid)) return;
            var em = world.EntityManager;
            // Managed narrative effects run after the simulation. Complete only the buffers this read needs.
            em.CompleteDependencyBeforeRO<BrickSlot>(); em.CompleteDependencyBeforeRO<BrickSolidMask>();
            var slots = em.GetBuffer<BrickSlot>(player, true);
            var random = RngStreams.For(campaignSeed, RngPurpose.Narrative,
                unchecked((ulong)BitConverter.DoubleToInt64Bits(universalTime)));
            int seen = 0; int3 chosen = default;
            // Reservoir sampling visits canonical slot/cell order and chooses uniformly among current surface cells.
            // It uses actual live masks, so prior damage and detached sections cannot produce an air hit.
            for (int slot = 0; slot < slots.Length; slot++)
            {
                Entity brick = slots[slot].Brick;
                if (brick == Entity.Null || !em.HasBuffer<BrickSolidMask>(brick)) continue;
                var mask = em.GetBuffer<BrickSolidMask>(brick, true);
                int3 coordinate = grid.BrickMin + new int3(slot % grid.BrickDims.x,
                    slot / grid.BrickDims.x % grid.BrickDims.y, slot / (grid.BrickDims.x * grid.BrickDims.y));
                for (int word = 0; word < mask.Length; word++)
                {
                    ulong bits = mask[word].Bits;
                    while (bits != 0)
                    {
                        int bit = math.tzcnt(bits); bits &= bits - 1;
                        int3 cell = BrickMath.CellOf(coordinate, word * 64 + bit);
                        bool surface = !Solid(em, slots, grid, cell + new int3(1, 0, 0)) ||
                            !Solid(em, slots, grid, cell - new int3(1, 0, 0)) ||
                            !Solid(em, slots, grid, cell + new int3(0, 1, 0)) ||
                            !Solid(em, slots, grid, cell - new int3(0, 1, 0)) ||
                            !Solid(em, slots, grid, cell + new int3(0, 0, 1)) ||
                            !Solid(em, slots, grid, cell - new int3(0, 0, 1));
                        if (surface && random.NextInt(++seen) == 0) chosen = cell;
                    }
                }
            }
            if (seen == 0) return;
            streams.GetSingleton<DamageStreams>().Damage.PostFromMainThread(new DamageEvent
            {
                Grid = player, LocalPoint = BrickMath.CellCenter(chosen, grid.CellSize),
                Amount = (float)amount * config.HullDamagePerPoint, Radius = 1.5f * grid.CellSize,
                Type = DamageType.Kinetic, Producer = ProducerKind.MainThread, Sequence = sequence++
            });
        }
        static bool Solid(EntityManager em, DynamicBuffer<BrickSlot> slots, in VoxelGrid grid, int3 cell)
        {
            int slot = BrickMath.SlotIndex(BrickMath.BrickOf(cell), grid.BrickMin, grid.BrickDims);
            if (slot < 0 || slot >= slots.Length) return false;
            Entity brick = slots[slot].Brick;
            if (brick == Entity.Null || !em.HasBuffer<BrickSolidMask>(brick)) return false;
            var mask = em.GetBuffer<BrickSolidMask>(brick, true);
            int local = BrickMath.LocalIndex(BrickMath.LocalOf(cell));
            return (mask[local >> 6].Bits & (1ul << (local & 63))) != 0;
        }
        public void Dispose()
        {
            if (!world.IsCreated) return;
            players.Dispose(); streams.Dispose();
        }
    }
}
