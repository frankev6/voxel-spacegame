using System.Collections.Generic;
using Game.Core;
using Game.Data.Blocks;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Voxel.Grid
{
    public partial struct GridSpawnSystem
    {
        /// <summary>ABL-006: runs <see cref="GridBrickFillRequest"/>s in entity order. A lost brick is recreated holding its requested
        /// cells (sparse up to SparseCellStorage.Limit, else dense), linked to the root, given to the spawn hooks, and queued for
        /// collider and mesh (with its neighbours); the root's revision and mass are refreshed. Then, if asked, the functional
        /// entities of live instances that have none are created.</summary>
        void Fill(ref SystemState state, VoxelStructureRegistry registry, VoxelContent content, VoxelRenderRuntime runtime,
            ref GridPrototypes prototypes, ref BrickMeshQueue meshes)
        {
            var em = state.EntityManager;
            using var requests = fills.ToEntityArray(Allocator.Temp);
            if (requests.Length == 0) return;
            em.CompleteAllTrackedJobs();
            requests.Sort(new EntityOrder());
            var colliders = em.GetComponentData<ColliderDirtyQueue>(singletons); colliders.CompleteAccess();
            foreach (var request in requests)
            {
                var order = em.GetComponentData<GridBrickFillRequest>(request); var root = order.Grid;
                if (!em.Exists(root) || !em.HasComponent<VoxelGrid>(root) || !em.HasBuffer<BrickSlot>(root) ||
                    em.HasComponent<GridSpawning>(root) && em.IsComponentEnabled<GridSpawning>(root))
                { em.DestroyEntity(request); continue; }
                using var cells = em.GetBuffer<GridBrickFillCell>(request).ToNativeArray(Allocator.Temp);
                var grid = em.GetComponentData<VoxelGrid>(root);
                var byBrick = new SortedDictionary<long, List<GridBrickFillCell>>();
                int dropped = 0;
                foreach (var cell in cells)
                {
                    int3 coord = BrickMath.BrickOf(cell.Cell);
                    int slot = BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims);
                    if (slot < 0 || cell.Value.IsAir) { dropped++; continue; }
                    var existing = em.GetBuffer<BrickSlot>(root)[slot].Brick;
                    if (existing != Entity.Null && em.Exists(existing)) { dropped++; continue; }
                    if (!byBrick.TryGetValue(slot, out var list)) byBrick.Add(slot, list = new List<GridBrickFillCell>());
                    list.Add(cell);
                }
                if (dropped > 0) GameLog.WarnOnce("Voxel.FillDropped", "Voxel", "brick fill dropped cells whose brick exists or lies outside the grid");
                bool render = runtime.IsReady != 0 && (em.GetComponentData<GridSource>(root).Flags & GridSpawnFlags.NoRender) == 0;
                if (byBrick.Count > 0)
                {
                    using var created = new NativeArray<Entity>(byBrick.Count, Allocator.Temp);
                    if (render) { em.Instantiate(prototypes.Brick, created); em.AddComponent<BrickRenderState>(created); }
                    else { em.CreateEntity(brickBase, created); em.RemoveComponent<Prefab>(created); }
                    grid.Revision++; em.SetComponentData(root, grid);
                    int n = 0; var coords = new List<int3>();
                    foreach (var pair in byBrick)
                    {
                        var entity = created[n++]; var list = pair.Value;
                        int3 coord = BrickMath.BrickOf(list[0].Cell); coords.Add(coord);
                        var unique = new SortedDictionary<int, Cell>();
                        foreach (var cell in list) unique[BrickMath.LocalIndex(BrickMath.LocalOf(cell.Cell))] = cell.Value;
                        em.SetComponentData(entity, new Brick { Grid = root, Coord = coord, SolidCount = (ushort)unique.Count, ChangedRevision = grid.Revision });
                        var dense = em.GetBuffer<BrickCells>(entity); var sparse = em.GetBuffer<BrickSparseCells>(entity);
                        dense.Clear(); sparse.Clear();
                        if (unique.Count <= SparseCellStorage.Limit)
                            foreach (var cell in unique) sparse.Add(new BrickSparseCells { Local = (ushort)cell.Key, Value = cell.Value });
                        else
                        {
                            dense.ResizeUninitialized(BrickMath.CellsPerBrick);
                            for (int i = 0; i < dense.Length; i++) dense[i] = default;
                            foreach (var cell in unique) dense[cell.Key] = new BrickCells { Value = cell.Value };
                        }
                        var mask = em.GetBuffer<BrickSolidMask>(entity); mask.ResizeUninitialized(BrickMath.MaskWords);
                        for (int i = 0; i < mask.Length; i++) mask[i] = default;
                        foreach (var cell in unique) { var word = mask[cell.Key >> 6]; word.Bits |= 1UL << (cell.Key & 63); mask[cell.Key >> 6] = word; }
                        em.SetComponentData(entity, LocalTransform.FromPositionRotationScale((float3)coord * (BrickMath.Size * grid.CellSize), quaternion.identity, grid.CellSize));
                        em.SetComponentData(entity, new Parent { Value = root });
                        em.SetComponentEnabled<BrickDirty>(entity, false);
                        var rootSlots = em.GetBuffer<BrickSlot>(root); rootSlots[(int)pair.Key] = new BrickSlot { Brick = entity };
                        em.GetBuffer<LinkedEntityGroup>(root).Add(new LinkedEntityGroup { Value = entity });
                    }
                    foreach (var hook in registry.SpawnHooks) hook.OnEntitiesCreated(em, root, created);
                    var slots = em.GetBuffer<BrickSlot>(root);
                    for (int i = 0; i < coords.Count; i++)
                    {
                        colliders.Enqueue(grid.Serial, root, created[i], coords[i], ColliderPriority.Damage);
                        if (!render) continue;
                        meshes.Enqueue(created[i], root, MeshPriority.Damage);
                        for (int face = 0; face < 6; face++)
                        {
                            int neighbour = BrickMath.SlotIndex(coords[i] + BrickMath.FaceDirection((BlockFace)face), grid.BrickMin, grid.BrickDims);
                            if (neighbour >= 0 && slots[neighbour].Brick != Entity.Null) meshes.Enqueue(slots[neighbour].Brick, root, MeshPriority.Damage);
                        }
                    }
                    if (em.HasComponent<GridMassRecompute>(root)) em.SetComponentEnabled<GridMassRecompute>(root, true);
                }
                if (order.ReviveFunctional != 0 && em.HasBuffer<BlockInstance>(root))
                {
                    using var instances = em.GetBuffer<BlockInstance>(root, true).ToNativeArray(Allocator.TempJob);
                    bool waiting = false;
                    foreach (var instance in instances)
                        waiting |= instance.Block != 0 && instance.CellsAlive > 0 && (instance.Flags & BlockInstanceFlags.Destroyed) == 0 &&
                            instance.Functional == Entity.Null && content.Blocks.Value.Blocks[instance.Block].Kind == BlockKind.Functional;
                    if (waiting)
                    {
                        using var records = new NativeArray<BrickRecord>(0, Allocator.TempJob);
                        CreateFunctional(ref state, registry, instances, records, grid.CellSize, root, content, runtime, ref prototypes);
                    }
                    else if (++order.Attempts < 8)
                    {   // keep asking for a few frames: the revive's instance write may not have landed yet
                        em.GetBuffer<GridBrickFillCell>(request).Clear(); em.SetComponentData(request, order); continue;
                    }
                }
                em.DestroyEntity(request);
            }
        }
    }
}
