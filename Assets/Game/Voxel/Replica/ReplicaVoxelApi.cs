using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

namespace Game.Voxel.Replica
{
    /// <summary>One replicated cell write on a co-op guest: brick-local index and the host's exact cell value.</summary>
    public struct ReplicaCellWrite { public ushort Local; public Cell Value; }

    /// <summary>Co-op spike (ADR-0116 §7–8, SPEC-90 NET-007/008): the guest-side writer of replicated voxel state. On a guest
    /// DamageResolveSystem, IntegritySystem and SplitSystem are disabled, so this is the only code that writes cells, palettes,
    /// instance health and chunk bodies there. Main thread, outside the fixed step (GameInitGroup), so structural changes are direct.</summary>
    public static class ReplicaVoxelApi
    {
        /// <summary>A mutable structure carrying the host grid's exact palette and instance order, so later raw cell values mean the same.</summary>
        public static VoxelStructure CreateStructure(float cellSize, NativeArray<PaletteEntry> palette, NativeArray<BlockInstance> instances)
        {
            var structure = new VoxelStructure(cellSize);
            structure.CopyRuntimeMetadata(cellSize, palette, instances);
            return structure;
        }

        /// <summary>Appends or overwrites palette entries from <paramref name="start"/> (host palettes only grow, through Place restores).</summary>
        public static void SetPalette(EntityManager em, Entity root, int start, NativeArray<PaletteEntry> entries)
        {
            var palette = em.GetBuffer<PaletteEntry>(root);
            if (palette.Length < start + entries.Length) palette.Resize(start + entries.Length, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < entries.Length; i++) palette[start + i] = entries[i];
        }

        /// <summary>Copies host instance health. A newly destroyed instance loses its functional entity, as on the host.</summary>
        public static void SetInstance(EntityManager em, Entity root, int index, float functionHp, ushort cellsAlive, BlockInstanceFlags flags)
        {
            var instances = em.GetBuffer<BlockInstance>(root);
            if ((uint)index >= (uint)instances.Length) return;
            var instance = instances[index];
            bool destroyedNow = (flags & BlockInstanceFlags.Destroyed) != 0 && (instance.Flags & BlockInstanceFlags.Destroyed) == 0;
            instance.FunctionHp = functionHp; instance.CellsAlive = cellsAlive; instance.Flags = flags;
            Entity functional = instance.Functional;
            if (destroyedNow && functional != Entity.Null) instance.Functional = Entity.Null;
            instances[index] = instance;
            if (destroyedNow && functional != Entity.Null && em.Exists(functional))
            {
                RemoveFromGroup(em, root, functional);
                em.DestroyEntity(functional);
            }
        }

        /// <summary>Applies one brick's replicated writes. Returns false when the brick does not exist here (a desync). Removed cells are
        /// appended to <paramref name="removed"/> for debris, VFX and audio; emptied bricks are destroyed like the host's resolver does.</summary>
        public static bool WriteBrick(EntityManager em, Entity root, int3 coord, NativeArray<ReplicaCellWrite> writes, NativeList<CellRemoved> removed, bool quiet = false)
        {
            var grid = em.GetComponentData<VoxelGrid>(root);
            int slotIndex = BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims);
            var slots = em.GetBuffer<BrickSlot>(root);
            if (slotIndex < 0 || slotIndex >= slots.Length) return false;
            Entity entity = slots[slotIndex].Brick;
            if (entity == Entity.Null || !em.Exists(entity)) return false;
            var palette = em.GetBuffer<PaletteEntry>(root, true);
            var instances = em.GetBuffer<BlockInstance>(root, true);
            var transform = em.GetComponentData<LocalTransform>(root);
            float3 velocity = em.HasComponent<PhysicsVelocity>(root) ? em.GetComponentData<PhysicsVelocity>(root).Linear : float3.zero;
            var brick = em.GetComponentData<Brick>(entity);
            var dirty = em.GetComponentData<BrickDirty>(entity);
            var dense = em.GetBuffer<BrickCells>(entity); var sparse = em.GetBuffer<BrickSparseCells>(entity); var mask = em.GetBuffer<BrickSolidMask>(entity);
            bool changed = false; BlockFaceMask neighbours = default;
            for (int i = 0; i < writes.Length; i++)
            {
                int local = writes[i].Local; if (local >= BrickMath.CellsPerBrick) continue;
                var old = BrickWrite.Get(dense, sparse, local);
                if (!BrickWrite.Set(ref brick, dense, sparse, mask, ref dirty, local, writes[i].Value, grid.Revision + 1)) continue;
                changed = true;
                if (!old.IsAir && writes[i].Value.IsAir && removed.IsCreated && !quiet)
                {
                    var paint = old.IsInstance && old.InstanceIndex < instances.Length
                        ? new PaletteEntry { Block = instances[old.InstanceIndex].Block, Orient = instances[old.InstanceIndex].Orient, Color = instances[old.InstanceIndex].Color }
                        : old.PaletteIndex < palette.Length ? palette[old.PaletteIndex] : default;
                    int3 cell = BrickMath.CellOf(coord, local);
                    removed.Add(new CellRemoved { Grid = root, Cell = cell, Block = paint.Block, Color = paint.Color, Orient = paint.Orient,
                        InstanceIndex = old.IsInstance ? old.InstanceIndex : -1, Reason = CellRemovedReason.Destroyed,
                        WorldPos = transform.TransformPoint(((float3)cell + .5f) * grid.CellSize), WorldVel = velocity });
                }
                neighbours |= BrickMath.BorderFaces(local);
            }
            if (!changed) return true;
            grid.Revision++; em.SetComponentData(root, grid);
            if (brick.SolidCount == 0)
            {
                slots = em.GetBuffer<BrickSlot>(root); slots[slotIndex] = default;
                RemoveFromGroup(em, root, entity);
                em.DestroyEntity(entity);
            }
            else
            {
                em.SetComponentData(entity, brick); em.SetComponentData(entity, dirty); em.SetComponentEnabled<BrickDirty>(entity, true);
            }
            TouchNeighbours(em, root, grid, coord, neighbours);
            return true;
        }

        static void TouchNeighbours(EntityManager em, Entity root, VoxelGrid grid, int3 coord, BlockFaceMask faces)
        {
            if (faces == 0) return;
            var slots = em.GetBuffer<BrickSlot>(root);
            for (int face = 0; face < 6; face++)
            {
                if (((int)faces & (1 << face)) == 0) continue;
                int3 other = coord + BlockFaceDirection(face);
                int index = BrickMath.SlotIndex(other, grid.BrickMin, grid.BrickDims);
                if (index < 0 || index >= slots.Length) continue;
                Entity entity = slots[index].Brick; if (entity == Entity.Null || !em.Exists(entity)) continue;
                var dirty = em.GetComponentData<BrickDirty>(entity); dirty.Flags |= BrickDirtyFlags.Mesh;
                em.SetComponentData(entity, dirty); em.SetComponentEnabled<BrickDirty>(entity, true);
            }
        }

        // BlockFaceMask bits 0..5: PosX, NegX, PosY, NegY, PosZ, NegZ.
        static int3 BlockFaceDirection(int face) => face switch
        { 0 => new int3(1, 0, 0), 1 => new int3(-1, 0, 0), 2 => new int3(0, 1, 0), 3 => new int3(0, -1, 0), 4 => new int3(0, 0, 1), _ => new int3(0, 0, -1) };

        static void RemoveFromGroup(EntityManager em, Entity root, Entity member)
        {
            if (!em.HasBuffer<LinkedEntityGroup>(root)) return;
            var group = em.GetBuffer<LinkedEntityGroup>(root);
            for (int i = group.Length - 1; i > 0; i--) if (group[i].Value == member) { group.RemoveAt(i); break; }
        }

        /// <summary>Debris sprays and break sounds for replicated removals, through the same streams and caps as the host's EmitDebrisJob.</summary>
        public static void EmitDebris(EntityManager em, NativeList<CellRemoved> removed)
        {
            if (removed.Length == 0) return;
            using var configs = em.CreateEntityQuery(ComponentType.ReadOnly<DamageConfig>());
            using var blocks = em.CreateEntityQuery(ComponentType.ReadOnly<BlockDefsRef>());
            using var fxQuery = em.CreateEntityQuery(ComponentType.ReadWrite<FxEventQueue>());
            using var sfxQuery = em.CreateEntityQuery(ComponentType.ReadWrite<SfxEventQueue>());
            if (configs.IsEmptyIgnoreFilter || blocks.IsEmptyIgnoreFilter || fxQuery.IsEmptyIgnoreFilter) return;
            em.CompleteAllTrackedJobs();
            var config = configs.GetSingleton<DamageConfig>(); var defs = blocks.GetSingleton<BlockDefsRef>().Value;
            var fx = fxQuery.GetSingleton<FxEventQueue>();
            if (config.MaxDebrisParticlesPerTick > 0)
            {
                int stride = (removed.Length - 1) / config.MaxDebrisParticlesPerTick + 1;
                float scale = math.pow(stride, 1f / 3);
                var random = Unity.Mathematics.Random.CreateFromIndex((uint)removed.Length * 7919u + 17u);
                for (int i = 0; i < removed.Length; i += stride)
                {
                    var cell = removed[i];
                    if (!em.HasComponent<VoxelGrid>(cell.Grid) || cell.Block >= defs.Value.Blocks.Length) continue;
                    // The host's kick depends on the damage event; guests use a plausible outward spray instead.
                    float3 kick = random.NextFloat3Direction() * random.NextFloat(2f, 6f);
                    fx.Queue.Enqueue(new FxEvent { Kind = FxKind.Debris, Flags = FxEventFlags.PaletteColor, Pos = cell.WorldPos, Dir = math.normalizesafe(kick),
                        Vel = cell.WorldVel + kick, Scale = em.GetComponentData<VoxelGrid>(cell.Grid).CellSize * scale,
                        Color = (uint)(cell.Color | (defs.Value.Blocks[cell.Block].MaterialLayer << 8)) });
                }
            }
            if (config.CueCellBreak.Value != 0 && !sfxQuery.IsEmptyIgnoreFilter)
            {
                var sfx = sfxQuery.GetSingleton<SfxEventQueue>();
                sfx.Queue.Enqueue(new SfxEvent { Cue = config.CueCellBreak, Pos = removed[0].WorldPos, Volume = math.clamp(.2f + .1f * math.log2(removed.Length), .2f, 1) });
            }
        }

        /// <summary>Host: bricks whose cells left this tick only as detached bodies (hulks and chunks), which guests receive as their own
        /// grids or chunk bodies and so must not also spray as debris. Call after the tick's structural sync, before the next tick.</summary>
        public static unsafe void CollectQuietBricks(EntityManager em, Dictionary<(Entity Grid, int3 Brick), bool> quiet)
        {
            quiet.Clear();
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<DamageStreams>());
            if (query.IsEmptyIgnoreFilter) return;
            em.CompleteAllTrackedJobs();
            var data = query.GetSingleton<DamageStreams>().Data;
            if (data == null) return;
            var removed = data->Removed;
            for (int i = 0; i < removed.Length; i++)
            {
                var cell = removed[i];
                var key = (cell.Grid, BrickMath.BrickOf(cell.Cell));
                bool body = cell.Reason == CellRemovedReason.DetachedBody;
                quiet[key] = quiet.TryGetValue(key, out bool all) ? all && body : body;
            }
        }

        /// <summary>Reads one brick's 4096 cells (air for a missing brick). Host capture and hash checks use it.</summary>
        public static bool ReadBrick(EntityManager em, Entity brick, NativeArray<Cell> cells)
        {
            if (brick == Entity.Null || !em.Exists(brick) || !em.HasBuffer<BrickCells>(brick))
            { for (int i = 0; i < cells.Length; i++) cells[i] = default; return false; }
            var dense = em.GetBuffer<BrickCells>(brick, true);
            if (dense.Length != 0) { cells.CopyFrom(dense.Reinterpret<Cell>().AsNativeArray()); return true; }
            for (int i = 0; i < cells.Length; i++) cells[i] = default;
            foreach (var entry in em.GetBuffer<BrickSparseCells>(brick, true)) cells[entry.Local] = entry.Value;
            return true;
        }

        /// <summary>Creates a kinematic chunk body on a guest from the host's cells and pose. Its motion comes from snapshots.</summary>
        public static Entity CreateChunk(EntityManager em, float cellSize, float3 localOrigin, NativeArray<ChunkCell> cells, LocalTransform pose, uint endTick, uint dissolveStartTick, uint tick)
        {
            using var configs = em.CreateEntityQuery(ComponentType.ReadOnly<DamageConfig>());
            int maxBoxes = configs.IsEmptyIgnoreFilter ? 16 : configs.GetSingleton<DamageConfig>().ChunkMaxBoxes;
            var blob = ChunkBoxBuilder.CreateCollider(cells, cellSize, localOrigin, maxBoxes);
            var entity = em.CreateEntity(typeof(LocalTransform), typeof(LocalToWorld), typeof(PhysicsVelocity),
                typeof(PhysicsMass), typeof(PhysicsCollider), typeof(PhysicsWorldIndex), typeof(PhysicsGraphicalSmoothing),
                typeof(PhysicsGraphicalInterpolationBuffer), typeof(ChunkBody), typeof(ChunkContactState), typeof(ChunkCell), typeof(ChunkColliderBlob),
                typeof(ChunkDissolve));
            em.SetSharedComponent(entity, GridBodyFactory.WorldIndex());
            em.SetComponentData(entity, pose);
            em.SetComponentData(entity, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
            em.SetComponentData(entity, new PhysicsCollider { Value = blob }); em.SetComponentData(entity, new ChunkColliderBlob { Value = blob });
            em.SetComponentData(entity, new PhysicsGraphicalSmoothing { ApplySmoothing = 1 });
            em.SetComponentData(entity, new PhysicsGraphicalInterpolationBuffer { PreviousTransform = new RigidTransform(pose.Rotation, pose.Position) });
            // Slot 255 marks a replica: the host-side pool logic never runs on guests, and presentation treats it as any chunk.
            em.SetComponentData(entity, new ChunkBody { SpawnTick = tick, EndTick = endTick, DissolveStartTick = dissolveStartTick, CellSize = cellSize,
                LocalOrigin = localOrigin, CellCount = (ushort)cells.Length, Slot = 255, Live = 1 });
            em.GetBuffer<ChunkCell>(entity).CopyFrom(cells);
            return entity;
        }

        /// <summary>Removes a replica chunk and frees its collider after the physics world stops referencing it.</summary>
        public static void DestroyChunk(EntityManager em, Entity entity)
        {
            if (!em.Exists(entity) || !em.HasComponent<ChunkBody>(entity)) return;
            var owner = em.GetComponentData<ChunkColliderBlob>(entity);
            if (owner.Value.IsCreated) DeferredRetire(em, owner.Value);
            em.DestroyEntity(entity);
        }

        static void DeferredRetire(EntityManager em, BlobAssetReference<Collider> blob)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<DeferredDisposalQueue>());
            if (query.IsEmptyIgnoreFilter) return;   // leaks one small blob at shutdown at worst
            em.CompleteAllTrackedJobs();
            var queue = query.GetSingletonRW<DeferredDisposalQueue>();
            DeferredDisposal.RetireBlob(ref queue.ValueRW, blob);
        }

        /// <summary>Order-independent hash of a grid's solid cells by brick coordinate, for host/guest comparison (SPEC-90 NET-007).</summary>
        public static ulong HashCells(EntityManager em, Entity root)
        {
            var slots = em.GetBuffer<BrickSlot>(root, true);
            var cells = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Temp);
            ulong total = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                Entity entity = slots[s].Brick; if (entity == Entity.Null || !em.Exists(entity)) continue;
                var coord = em.GetComponentData<Brick>(entity).Coord;
                ReadBrick(em, entity, cells);
                ulong h = Game.Core.StableHash.Combine((ulong)(uint)coord.x, Game.Core.StableHash.Combine((ulong)(uint)coord.y, (ulong)(uint)coord.z));
                for (int i = 0; i < cells.Length; i++)
                {
                    var c = cells[i]; if (c.IsAir) continue;
                    h = Game.Core.StableHash.Combine(h, ((ulong)(uint)i << 32) | ((ulong)c.Ref << 16) | ((ulong)c.Hp << 8) | c.Flags);
                }
                total += h;   // sum: independent of slot order, which differs when guest bounds are tighter
            }
            cells.Dispose();
            return total;
        }
    }
}
