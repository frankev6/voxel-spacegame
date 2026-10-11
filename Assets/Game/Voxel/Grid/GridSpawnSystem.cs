using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace Game.Voxel.Grid
{
    /// <summary>Budgeted, deterministic request conversion and entity creation (SPEC-10 §5.7). Managed init boundary; fill work is Burst.</summary>
    [UpdateInGroup(typeof(GameInitGroup))]
    public partial struct GridSpawnSystem : ISystem
    {
        Entity singletons;
        EntityQuery requests, spawning, registryQuery, despawns, rebuilds;
        EntityArchetype brickBase;
        int rendererWait;
        byte brickRendered;
        NativeList<RootComponentRegistration> rootRegistrations;
        NativeList<ProfileSet> profileSets;
        struct ProfileSet { public GridProfile Profile; public ComponentTypeSet Types; }
        static readonly ProfilerMarker PrototypesMarker = new ProfilerMarker("GridSpawnSystem.Prototypes");
        static readonly ProfilerMarker ConvertMarker = new ProfilerMarker("GridSpawnSystem.Convert");
        static readonly ProfilerMarker BricksMarker = new ProfilerMarker("GridSpawnSystem.CreateBricks");
        static readonly ProfilerMarker DespawnMarker = new ProfilerMarker("GridSpawnSystem.Despawn");
        static readonly ProfilerMarker RebuildMarker = new ProfilerMarker("GridSpawnSystem.Rebuild");
        static readonly ProfilerMarker FunctionalMarker = new ProfilerMarker("GridSpawnSystem.CreateFunctional");

        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;
            singletons = VoxelSingletons.CreateAll(em);
            rootRegistrations = new NativeList<RootComponentRegistration>(Allocator.Persistent);
            profileSets = new NativeList<ProfileSet>(Allocator.Persistent);
            var types = new List<ComponentType> { typeof(Brick), typeof(BrickCells), typeof(BrickSparseCells), typeof(BrickSolidMask), typeof(BrickDirty),
                typeof(LocalTransform), typeof(LocalToWorld), typeof(Parent), typeof(BrickLodDesired), typeof(BrickPropInstance), typeof(Prefab) };
            types.AddRange(DamageSetup.BrickComponents()); brickBase = em.CreateArchetype(types.ToArray());
            requests = state.GetEntityQuery(ComponentType.ReadOnly<GridSpawnRequest>());
            spawning = state.GetEntityQuery(ComponentType.ReadOnly<GridSpawning>(), ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<SpawnReadToken>());
            despawns = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<GridDespawnRequest>(), ComponentType.ReadOnly<VoxelGrid>() },
                Options = EntityQueryOptions.IncludeDisabledEntities
            });
            rebuilds = state.GetEntityQuery(ComponentType.ReadOnly<GridRebuildRequest>(), ComponentType.ReadOnly<VoxelGrid>());
            registryQuery = state.GetEntityQuery(ComponentType.ReadOnly<VoxelStructureRegistryRef>());
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<VoxelConfig>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate(registryQuery);
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (!registryQuery.IsEmptyIgnoreFilter)
            {
                var registry = registryQuery.GetSingleton<VoxelStructureRegistryRef>().Value.Value;
                if (registry != null) registry.CancelSpawnReads();
            }
            VoxelSingletons.DisposeAll(state.EntityManager, singletons);
            rootRegistrations.Dispose(); profileSets.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var registry = SystemAPI.GetSingleton<VoxelStructureRegistryRef>().Value.Value;
            registry.ReconcileReads(em);
            var config = SystemAPI.GetSingleton<VoxelConfig>(); var runtime = em.GetComponentData<VoxelRenderRuntime>(singletons);
            if (config.RequireRenderer != 0 && runtime.IsReady == 0)
            {
                if (++rendererWait == 300) GameLog.WarnOnce("Voxel.RendererWait", "Voxel", "spawn requests await renderer readiness after 300 frames");
                return;
            }
            state.CompleteDependency();
            var prototypes = em.GetComponentData<GridPrototypes>(singletons);
            using (PrototypesMarker.Auto()) PreparePrototypes(em, runtime, ref prototypes);
            var content = new VoxelContent { Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value, Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value };
            using (DespawnMarker.Auto()) Despawn(ref state, registry);
            using (RebuildMarker.Auto()) Rebuild(ref state, registry, content, runtime, ref prototypes);
            int budget = math.max(0, config.SpawnBricksPerFrame);
            var queue = em.GetComponentData<BrickMeshQueue>(singletons); queue.CompleteAccess();
            using (var roots = spawning.ToEntityArray(Allocator.Temp))
            {
                roots.Sort(new SerialOrder { Grids = state.GetComponentLookup<VoxelGrid>(true) });
                foreach (var root in roots)
                {
                    if (budget == 0) break;
                    Continue(ref state, registry, root, runtime, prototypes.Brick, ref queue, ref budget);
                }
            }
            using (ConvertMarker.Auto()) Convert(ref state, registry, content, runtime, ref prototypes, ref queue, ref budget);
            em.SetComponentData(singletons, prototypes);
        }

        void PreparePrototypes(EntityManager em, VoxelRenderRuntime runtime, ref GridPrototypes prototypes)
        {
            byte rendered = runtime.IsReady;
            if (!em.Exists(prototypes.Brick) || rendered != brickRendered)
            {
                if (em.Exists(prototypes.Brick)) em.DestroyEntity(prototypes.Brick);
                prototypes.Brick = em.CreateEntity(brickBase); brickRendered = rendered;
                em.SetComponentEnabled<BrickDirty>(prototypes.Brick, false);
                if (rendered != 0)
                {
                    AddRender(em, prototypes.Brick, runtime.BrickMaterial, runtime.PlaceholderMesh);
                    em.SetComponentEnabled<MaterialMeshInfo>(prototypes.Brick, false);
                }
            }
            CacheProfileSets(em);
            int registrations = em.GetBuffer<FunctionalComponentRegistration>(singletons).Length;
            if (prototypes.RegistrationCount != registrations)
            {
                ClearPrototypes(em, prototypes.FunctionalByMask); ClearPrototypes(em, prototypes.FunctionalByMaskNoRender);
                prototypes.RegistrationCount = registrations;
            }
        }
        void CacheProfileSets(EntityManager em)
        {
            var registrations = em.GetBuffer<RootComponentRegistration>(singletons);
            bool changed = registrations.Length != rootRegistrations.Length;
            for (int i = 0; !changed && i < registrations.Length; i++)
                changed = registrations[i].Profile != rootRegistrations[i].Profile || registrations[i].Type != rootRegistrations[i].Type;
            if (!changed) return;
            rootRegistrations.Clear(); rootRegistrations.AddRange(registrations.AsNativeArray()); profileSets.Clear();
            for (GridProfile profile = GridProfile.PlayerShip; profile <= GridProfile.Dummy; profile++)
            {
                var types = new List<ComponentType>();
                foreach (var r in rootRegistrations) if (r.Profile == profile && !types.Contains(r.Type)) types.Add(r.Type);
                for (int start = 0; start < types.Count; start += 15)
                {
                    var set = new FixedList128Bytes<ComponentType>();
                    for (int i = start; i < math.min(start + 15, types.Count); i++) set.Add(types[i]);
                    profileSets.Add(new ProfileSet { Profile = profile, Types = new ComponentTypeSet(set) });
                }
            }
        }
        static void ClearPrototypes(EntityManager em, NativeHashMap<ulong, Entity> map)
        {
            using var values = map.GetValueArray(Allocator.Temp);
            using var live = new NativeList<Entity>(values.Length, Allocator.Temp);
            foreach (var e in values) if (em.Exists(e)) live.Add(e);
            if (live.Length > 0) em.DestroyEntity(live.AsArray()); map.Clear();
        }
        static void AddRender(EntityManager em, Entity e, BatchMaterialID material, BatchMeshID mesh)
            => RenderMeshUtility.AddComponents(e, em, new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true), new MaterialMeshInfo(material, mesh));

        unsafe void Convert(ref SystemState state, VoxelStructureRegistry registry, VoxelContent content, VoxelRenderRuntime runtime,
            ref GridPrototypes prototypes, ref BrickMeshQueue queue, ref int budget)
        {
            var em = state.EntityManager;
            using var candidates = requests.ToEntityArray(Allocator.Temp); candidates.Sort(new EntityOrder());
            using var selected = new NativeList<Entity>(Allocator.Temp);
            using var data = new NativeList<RootFillData>(Allocator.TempJob);
            using var rejected = new NativeList<Entity>(Allocator.Temp);
            int available = budget;
            foreach (var entity in candidates)
            {
                if (selected.Length > 0 && available <= 0) break;
                var request = em.GetComponentData<GridSpawnRequest>(entity);
                int token = em.HasComponent<SpawnReadToken>(entity) ? em.GetComponentData<SpawnReadToken>(entity).Value : 0;
                VoxelStructure source = token != 0 ? registry.ReadSource(token) : null;
                string error = null;
                if ((request.StructureHandle >= 0) == !request.Blueprint.IsNone) error = "exactly one source is required";
                else if (source != null && !registry.MatchesRead(token, source, request.StructureHandle, request.Blueprint)) error = "source differs from pinned request";
                else if (source == null)
                {
                    if (request.StructureHandle >= 0) source = registry.Get(request.StructureHandle);
                    else registry.TryGetBlueprint(request.Blueprint, out source);
                }
                if (error == null && (source == null || !source.Data.IsCreated)) error = "source not found";
                if (error == null && source.Data.Bricks.Length == 0) error = "empty structure";
                int3 cellMin = default, cellMax = default, brickMin = default, dims = default;
                if (error == null)
                {
                    source.TryGetCellBounds(out cellMin, out cellMax);
                    brickMin = BrickMath.BrickOf(cellMin); dims = BrickMath.BrickOf(cellMax) - brickMin + 1;
                    if (math.any(dims <= 0) || (long)dims.x * dims.y * dims.z > 262144) error = "brick table exceeds 262144 slots";
                    if (!(source.Data.Header.Value.CellSize > 0) || !math.isfinite(source.Data.Header.Value.CellSize)) error = "invalid cell size";
                }
                if (error != null)
                {
                    registry.EndRead(token); rejected.Add(entity); GameLog.Error("Voxel", "spawn rejected: " + error); continue;
                }
                if (!request.Blueprint.IsNone && (request.Flags & GridSpawnFlags.WriteBack) != 0)
                { request.Flags &= ~GridSpawnFlags.WriteBack; GameLog.Warn("Voxel", "blueprint spawn WriteBack cleared"); }
                if (request.PersistentId == 0 && (request.Flags & (GridSpawnFlags.EditorPreview | GridSpawnFlags.Transient)) == 0)
                    GameLog.WarnOnce("Voxel.ZeroPersistentId", "Voxel", "spawn has no PersistentId and will not be saved");
                if (request.Profile == GridProfile.PlayerShip || request.Profile == GridProfile.NpcShip || request.Profile == GridProfile.SmallCraft || request.Profile == GridProfile.Dummy)
                    request.Flags |= GridSpawnFlags.PlanarInertia;
                if (token == 0) token = registry.PinSpawnRead(source, request.StructureHandle, request.Blueprint);
                registry.BindRead(token, entity);
                int anchor = -1;
                for (int i = 0; i < source.Data.Instances.Length; i++)
                {
                    var instance = source.Data.Instances[i];
                    if (instance.Block != 0 && instance.CellsAlive > 0 && content.Blocks.Value.Blocks[instance.Block].IsCore) { anchor = i; break; }
                }
                selected.Add(entity);
                data.Add(new RootFillData
                {
                    Entity = entity, Request = request, Token = token, Mass = source.GetMass(content),
                    Grid = new VoxelGrid { BrickMin = brickMin, BrickDims = dims, CellMin = cellMin, CellMax = cellMax,
                        CellSize = source.Data.Header.Value.CellSize, Revision = 1, AnchorInstance = anchor, Kind = request.Kind, Serial = prototypes.NextSerial++ },
                    Bricks = (BrickRecord*)source.Data.Bricks.GetUnsafeReadOnlyPtr(), BrickLength = source.BrickCount,
                    Palette = (PaletteEntry*)source.Data.Palette.GetUnsafeReadOnlyPtr(), PaletteLength = source.Data.Palette.Length,
                    Instances = (BlockInstance*)source.Data.Instances.GetUnsafeReadOnlyPtr(), InstanceLength = source.Data.Instances.Length
                });
                available -= math.min(available, source.Data.Bricks.Length);
            }
            if (rejected.Length > 0) em.DestroyEntity(rejected.AsArray());
            if (selected.Length == 0) return;
            var types = new List<ComponentType> { typeof(VoxelGrid), typeof(BrickSlot), typeof(PaletteEntry), typeof(BlockInstance),
                typeof(LocalTransform), typeof(LocalToWorld), typeof(LinkedEntityGroup), typeof(PersistentId), typeof(GridSource), typeof(GridSpawning),
                typeof(GridDespawnRequest), typeof(GridRebuilt), typeof(GridBodyInit), typeof(GridEditRequest), typeof(GridEditedBrick),
                typeof(GridSpawnBrick), typeof(GridMass), typeof(GridMassRecompute), typeof(GridColliderState), typeof(Dormant), typeof(SpawnReadToken) };
            types.AddRange(DamageSetup.GridRootComponents()); AddTypes(em, selected.AsArray(), types);
            for (GridProfile profile = GridProfile.PlayerShip; profile <= GridProfile.Dummy; profile++)
            {
                using var group = new NativeList<Entity>(Allocator.Temp);
                foreach (var d in data) if (d.Request.Profile == profile) group.Add(d.Entity);
                if (group.Length == 0) continue;
                foreach (var set in profileSets) if (set.Profile == profile) em.AddComponent(group.AsArray(), set.Types);
            }
            FillRoots(ref state, data.AsArray());
            em.RemoveComponent<GridSpawnRequest>(selected.AsArray());
            foreach (var d in data)
            {
                var source = registry.ReadSource(d.Token);
                CreateFunctional(ref state, registry, source, d.Entity, content, runtime, ref prototypes);
                Continue(ref state, registry, d.Entity, runtime, prototypes.Brick, ref queue, ref budget);
            }
        }

        static void FillRoots(ref SystemState state, NativeArray<RootFillData> data)
        {
            new RootFillJob
            {
                Data = data, Grids = state.GetComponentLookup<VoxelGrid>(), Tokens = state.GetComponentLookup<SpawnReadToken>(),
                Transforms = state.GetComponentLookup<LocalTransform>(), Ids = state.GetComponentLookup<PersistentId>(), Sources = state.GetComponentLookup<GridSource>(),
                Spawning = state.GetComponentLookup<GridSpawning>(), Bodies = state.GetComponentLookup<GridBodyInit>(), Masses = state.GetComponentLookup<GridMass>(),
                Recompute = state.GetComponentLookup<GridMassRecompute>(), Despawn = state.GetComponentLookup<GridDespawnRequest>(), Rebuilt = state.GetComponentLookup<GridRebuilt>(),
                Edit = state.GetComponentLookup<GridEditRequest>(), Dormant = state.GetComponentLookup<Dormant>(), Palettes = state.GetBufferLookup<PaletteEntry>(),
                Instances = state.GetBufferLookup<BlockInstance>(), Slots = state.GetBufferLookup<BrickSlot>(), Groups = state.GetBufferLookup<LinkedEntityGroup>(), Baselines = state.GetBufferLookup<GridSpawnBrick>()
            }.Schedule(data.Length, 1).Complete();
        }

        void Despawn(ref SystemState state, VoxelStructureRegistry registry)
        {
            var em = state.EntityManager;
            using var roots = despawns.ToEntityArray(Allocator.Temp);
            if (roots.Length == 0) return;
            em.CompleteAllTrackedJobs(); roots.Sort(new SerialOrder { Grids = state.GetComponentLookup<VoxelGrid>(true) });
            foreach (var root in roots)
            {
                if (em.HasComponent<RebuildReadToken>(root)) registry.EndRead(em.GetComponentData<RebuildReadToken>(root).Value);
                if (em.HasComponent<SpawnReadToken>(root)) registry.EndRead(em.GetComponentData<SpawnReadToken>(root).Value);
                GridStructureSync.WriteBackCore(em, root, registry, true);
            }
            em.DestroyEntity(roots);
        }

        unsafe void Rebuild(ref SystemState state, VoxelStructureRegistry registry, VoxelContent content, VoxelRenderRuntime runtime,
            ref GridPrototypes prototypes)
        {
            var em = state.EntityManager;
            using var roots = rebuilds.ToEntityArray(Allocator.Temp);
            if (roots.Length == 0) return;
            em.CompleteAllTrackedJobs(); roots.Sort(new SerialOrder { Grids = state.GetComponentLookup<VoxelGrid>(true) });
            using var selected = new NativeList<Entity>(Allocator.Temp);
            using var children = new NativeList<Entity>(Allocator.TempJob);
            using var data = new NativeList<RootFillData>(Allocator.TempJob);
            foreach (var root in roots)
            {
                var request = em.GetComponentData<GridRebuildRequest>(root);
                int token = em.HasComponent<RebuildReadToken>(root) ? em.GetComponentData<RebuildReadToken>(root).Value : 0;
                var source = token == 0 ? registry.Get(request.StructureHandle) : registry.ReadSource(token);
                bool valid = source != null && source.Data.IsCreated && source.BrickCount > 0;
                int3 lo = default, hi = default, min = default, dims = default;
                if (valid)
                {
                    source.TryGetCellBounds(out lo, out hi); min = BrickMath.BrickOf(lo); dims = BrickMath.BrickOf(hi) - min + 1;
                    valid = math.all(dims > 0) && (long)dims.x * dims.y * dims.z <= 262144 && math.isfinite(source.CellSize) && source.CellSize > 0
                        && (token == 0 || registry.MatchesRead(token, source, request.StructureHandle, default));
                }
                if (!valid) { registry.EndRead(token); GameLog.Error("Voxel", "rebuild rejected: invalid source"); continue; }
                if (token == 0) token = registry.PinSpawnRead(source, request.StructureHandle, default);
                if (em.HasComponent<SpawnReadToken>(root)) registry.EndRead(em.GetComponentData<SpawnReadToken>(root).Value);
                registry.BindRead(token, root);
                var grid = em.GetComponentData<VoxelGrid>(root);
                grid.BrickMin = min; grid.BrickDims = dims; grid.CellMin = lo; grid.CellMax = hi; grid.CellSize = source.CellSize;
                grid.Revision++; grid.AnchorInstance = -1;
                for (int i = 0; i < source.Data.Instances.Length; i++)
                {
                    var instance = source.Data.Instances[i];
                    if (instance.Block != 0 && instance.CellsAlive > 0 && content.Blocks.Value.Blocks[instance.Block].IsCore) { grid.AnchorInstance = i; break; }
                }
                new CollectGridChildrenJob
                {
                    Slots = em.GetBuffer<BrickSlot>(root).AsNativeArray(), Instances = em.GetBuffer<BlockInstance>(root).AsNativeArray(),
                    Storage = state.GetEntityStorageInfoLookup(), Children = children
                }.Run();
                selected.Add(root);
                data.Add(new RootFillData
                {
                    Entity = root, Grid = grid, Token = token, Mass = source.GetMass(content), Rebuild = 1,
                    Request = new GridSpawnRequest { StructureHandle = request.StructureHandle, Flags = request.Flags },
                    Bricks = (BrickRecord*)source.Data.Bricks.GetUnsafeReadOnlyPtr(), BrickLength = source.BrickCount,
                    Palette = (PaletteEntry*)source.Data.Palette.GetUnsafeReadOnlyPtr(), PaletteLength = source.Data.Palette.Length,
                    Instances = (BlockInstance*)source.Data.Instances.GetUnsafeReadOnlyPtr(), InstanceLength = source.Data.Instances.Length
                });
            }
            if (children.Length > 0) em.DestroyEntity(children.AsArray());
            if (selected.Length > 0)
            {
                em.AddComponent<SpawnReadToken>(selected.AsArray());
                FillRoots(ref state, data.AsArray());
                foreach (var d in data) CreateFunctional(ref state, registry, registry.ReadSource(d.Token), d.Entity, content, runtime, ref prototypes);
            }
            em.RemoveComponent<GridRebuildRequest>(roots); em.RemoveComponent<RebuildReadToken>(roots);
        }

        static void AddTypes(EntityManager em, NativeArray<Entity> entities, List<ComponentType> types)
        {
            for (int start = 0; start < types.Count; start += 15)
            {
                var set = new FixedList128Bytes<ComponentType>();
                for (int i = start; i < math.min(start + 15, types.Count); i++) set.Add(types[i]);
                em.AddComponent(entities, new ComponentTypeSet(set));
            }
        }

        void Continue(ref SystemState state, VoxelStructureRegistry registry, Entity root, VoxelRenderRuntime runtime,
            Entity prototype, ref BrickMeshQueue queue, ref int budget)
        {
            if (budget <= 0) return;
            using var marker = BricksMarker.Auto(); var em = state.EntityManager;
            int token = em.GetComponentData<SpawnReadToken>(root).Value;
            var source = registry.ReadSource(token);
            if (source == null) return; // Teardown cancellation is followed by scene destruction, never another source read.
            int start = em.GetComponentData<GridSpawning>(root).NextBrick;
            int count = math.min(budget, source.Data.Bricks.Length - start);
            var grid = em.GetComponentData<VoxelGrid>(root);
            bool render = runtime.IsReady != 0 && (em.GetComponentData<GridSource>(root).Flags & GridSpawnFlags.NoRender) == 0;
            using var entities = new NativeArray<Entity>(count, Allocator.TempJob);
            if (render) { em.Instantiate(prototype, entities); em.AddComponent<BrickRenderState>(entities); }
            else
            {
                em.CreateEntity(brickBase, entities); em.RemoveComponent<Prefab>(entities);
                // Enableable defaults from CreateEntity are on; batch disabling is performed below by the fill job.
            }
            new BrickFillJob
            {
                Entities = entities, Records = source.Data.Bricks.AsArray(), Pages = source.Data.CellPages.AsArray(), SparsePages = source.Data.SparsePages.AsArray(), Masks = source.Data.Masks.AsArray(),
                Root = root, Start = start, CellSize = grid.CellSize, Bricks = state.GetComponentLookup<Brick>(), Cells = state.GetBufferLookup<BrickCells>(), Sparse = state.GetBufferLookup<BrickSparseCells>(),
                SolidMasks = state.GetBufferLookup<BrickSolidMask>(), Transforms = state.GetComponentLookup<LocalTransform>(), Parents = state.GetComponentLookup<Parent>(),
                Dirty = state.GetComponentLookup<BrickDirty>()
            }.Schedule(count, 8).Complete();
            using var emptyIndices = new NativeArray<int>(0, Allocator.TempJob);
            new LinkCreatedJob
            {
                Entities = entities, Records = source.Data.Bricks.AsArray(), InstanceIndices = emptyIndices,
                Root = root, Grid = grid, Start = start, Groups = state.GetBufferLookup<LinkedEntityGroup>(), Slots = state.GetBufferLookup<BrickSlot>(),
                Instances = state.GetBufferLookup<BlockInstance>(), Spawning = state.GetComponentLookup<GridSpawning>()
            }.Run();
            if (render) new EnqueueSpawnMeshesJob
            {
                Entities = entities, Root = root, Damage = queue.Damage, Neighbour = queue.Neighbour, Spawn = queue.Spawn,
                Lod = queue.Lod, Heads = queue.Heads, Pending = queue.Pending
            }.Run();
            foreach (var hook in registry.SpawnHooks) hook.OnEntitiesCreated(em, root, entities);
            budget -= count;
            if (start + count == source.Data.Bricks.Length) { em.RemoveComponent<SpawnReadToken>(root); registry.EndRead(token); }
        }

        void CreateFunctional(ref SystemState state, VoxelStructureRegistry registry, VoxelStructure source, Entity root,
            VoxelContent content, VoxelRenderRuntime runtime, ref GridPrototypes prototypes)
        {
            using var marker = FunctionalMarker.Auto(); var em = state.EntityManager;
            var groups = new SortedDictionary<ulong, List<int>>();
            for (int i = 0; i < source.Data.Instances.Length; i++)
            {
                var instance = source.Data.Instances[i];
                if (instance.Block == 0 || instance.CellsAlive == 0) continue;
                ref var def = ref content.Blocks.Value.Blocks[instance.Block]; if (def.Kind != BlockKind.Functional) continue;
                if (!groups.TryGetValue(def.ModuleMask, out var indices)) groups.Add(def.ModuleMask, indices = new List<int>());
                indices.Add(i);
            }
            bool render = runtime.IsReady != 0 && (em.GetComponentData<GridSource>(root).Flags & GridSpawnFlags.NoRender) == 0;
            foreach (var group in groups)
            {
                var map = render ? prototypes.FunctionalByMask : prototypes.FunctionalByMaskNoRender;
                if (!map.TryGetValue(group.Key, out var prototype) || !em.Exists(prototype))
                {
                    var types = new List<ComponentType> { typeof(FunctionalBlock), typeof(LocalTransform), typeof(LocalToWorld), typeof(Parent), typeof(Prefab) };
                    foreach (var r in em.GetBuffer<FunctionalComponentRegistration>(singletons))
                        if ((r.Module == BlockModuleId.None || (group.Key & (1UL << (int)r.Module)) != 0) && !types.Contains(r.Type)) types.Add(r.Type);
                    prototype = em.CreateEntity(em.CreateArchetype(types.ToArray()));
                    if (render) AddRender(em, prototype, runtime.PropMaterial, runtime.PlaceholderMesh);
                    map[group.Key] = prototype;
                }
                using var indices = new NativeArray<int>(group.Value.ToArray(), Allocator.TempJob);
                using var entities = new NativeArray<Entity>(indices.Length, Allocator.TempJob); em.Instantiate(prototype, entities);
                using var emptyMeshes = new NativeArray<BatchMeshID>(0, Allocator.TempJob);
                using var emptyBounds = new NativeArray<AABB>(0, Allocator.TempJob);
                new FunctionalFillJob
                {
                    Entities = entities, Indices = indices, Instances = source.Data.Instances.AsArray(), Root = root,
                    CellSize = source.Data.Header.Value.CellSize, Blocks = content.Blocks, Orientations = content.Orientations,
                    Render = (byte)(render ? 1 : 0), Material = runtime.PropMaterial,
                    Meshes = render ? runtime.PropMeshIds : emptyMeshes, Bounds = render ? runtime.PropMeshBounds : emptyBounds,
                    Functional = state.GetComponentLookup<FunctionalBlock>(), Transforms = state.GetComponentLookup<LocalTransform>(), Parents = state.GetComponentLookup<Parent>(),
                    MeshInfo = state.GetComponentLookup<MaterialMeshInfo>(), RenderBounds = state.GetComponentLookup<RenderBounds>()
                }.Schedule(entities.Length, 8).Complete();
                new LinkCreatedJob
                {
                    Entities = entities, Records = source.Data.Bricks.AsArray(), InstanceIndices = indices, Root = root, Functional = 1,
                    Groups = state.GetBufferLookup<LinkedEntityGroup>(), Slots = state.GetBufferLookup<BrickSlot>(), Instances = state.GetBufferLookup<BlockInstance>(),
                    Spawning = state.GetComponentLookup<GridSpawning>()
                }.Run();
                foreach (var hook in registry.SpawnHooks) hook.OnEntitiesCreated(em, root, entities);
            }
        }
        struct EntityOrder : IComparer<Entity>
        {
            public int Compare(Entity a, Entity b) { int index = a.Index.CompareTo(b.Index); return index == 0 ? a.Version.CompareTo(b.Version) : index; }
        }
        struct SerialOrder : IComparer<Entity>
        {
            public ComponentLookup<VoxelGrid> Grids;
            public int Compare(Entity a, Entity b) => Grids[a].Serial.CompareTo(Grids[b].Serial);
        }
    }
}
