using System;
using System.Collections.Generic;
using Game.Core.Components;
using Game.Data.Blocks;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Voxel.Damage
{
    /// <summary>DMG-004 fixtures built through the production structure and grid spawn path.</summary>
    internal sealed class TestGridFactory : IDisposable
    {
        public readonly VoxelTestContent Content = new VoxelTestContent(includeExplosive: true);
        public readonly World World = new World("Damage grid fixtures");
        readonly BlueprintLibrary library;
        readonly VoxelStructureRegistry registry;
        readonly SystemHandle spawn;
        readonly List<VoxelStructure> sources = new List<VoxelStructure>();
        public EntityManager EM => World.EntityManager;
        public TestGridFactory()
        {
            library = new BlueprintLibrary(Content.Service.Catalog, Content.Content);
            registry = VoxelStructureRegistry.CreateRuntime(library);
            Content.Service.CreateSingletons(EM);
            EM.CreateSingleton(new VoxelStructureRegistryRef { Value = registry });
            EM.CreateSingleton(new VoxelConfig { SpawnBricksPerFrame = 1024, DefaultCellSize = .5f });
            EM.CreateEntity(typeof(GameplayActive)); spawn = World.CreateSystem<GridSpawnSystem>();
        }
        VoxelStructure Source() { var s = new VoxelStructure(); sources.Add(s); return s; }
        public VoxelStructure Box(int3 min, int3 max, bool hollow = false)
        {
            var s = Source(); Fill(s, min, max, hollow); return s;
        }
        void Fill(VoxelStructure s, int3 min, int3 max, bool hollow)
        {
            ushort palette = s.FindOrAddPalette(new PaletteEntry { Block = Content.Index("block_hull"), Color = 3 });
            for (int z = min.z; z <= max.z; z++) for (int y = min.y; y <= max.y; y++) for (int x = min.x; x <= max.x; x++)
            {
                int3 c = new int3(x, y, z);
                if (!hollow || math.any(c == min) || math.any(c == max)) s.SetCell(c, Cell.Palette(palette));
            }
        }
        public VoxelStructure Bridge()
        {
            var s = Box(new int3(-3, -1, -1), new int3(-1, 1, 1));
            Fill(s, new int3(1, -1, -1), new int3(3, 1, 1), false);
            Fill(s, new int3(0), new int3(0), false); return s;
        }
        public VoxelStructure CoreAndReactor()
        {
            var s = Box(new int3(-4, -1, -4), new int3(4, -1, 4));
            Place(s, "block_core_cross", new int3(0)); Place(s, "block_test_reactor", new int3(3, 0, 0)); return s;
        }
        public void Place(VoxelStructure s, string id, int3 origin)
        {
            var result = s.TryPlaceBlock(new BlockPlacement { Block = Content.Index(id), Origin = origin }, Content.Content);
            if (result != PlaceResult.Ok) throw new InvalidOperationException(result.ToString());
        }
        public Entity Spawn(VoxelStructure s)
        {
            var root = GridSpawnApi.RequestSpawn(EM, registry, registry.Register(s), new GridSpawnParams
            { PersistentId = (ulong)sources.Count, Pose = RigidTransform.identity, Flags = GridSpawnFlags.NoRender, Motion = GridMotion.Dynamic });
            spawn.Update(World.Unmanaged); return root;
        }
        public CellWriter Writer(Entity root)
        {
            ref var state = ref World.Unmanaged.ResolveSystemStateRef(spawn);
            var w = new CellWriter
            {
                Access = new GridCellAccess
                {
                    Grid = root, Definition = EM.GetComponentData<VoxelGrid>(root), Slots = EM.GetBuffer<BrickSlot>(root),
                    Palette = EM.GetBuffer<PaletteEntry>(root), Instances = EM.GetBuffer<BlockInstance>(root), Blocks = Content.Service.BlockDefs,
                    Dense = state.GetBufferLookup<BrickCells>(), Sparse = state.GetBufferLookup<BrickSparseCells>(), Masks = state.GetBufferLookup<BrickSolidMask>(),
                    Bricks = state.GetComponentLookup<Brick>(), Dirty = state.GetComponentLookup<BrickDirty>()
                },
                Config = DamageConfig.Default, Transform = LocalTransform.Identity,
                Mass = new Unity.Physics.PhysicsMass { Transform = RigidTransform.identity }, Tick = 7,
                Row = new DamageTypeRow { KickMin = 6, KickMax = 18 }
            };
            w.Allocate(Allocator.TempJob); return w;
        }
        public void CleanDirty(Entity root)
        {
            foreach (var slot in EM.GetBuffer<BrickSlot>(root)) if (slot.Brick != Entity.Null)
            { EM.SetComponentData(slot.Brick, new BrickDirty()); EM.SetComponentEnabled<BrickDirty>(slot.Brick, false); }
        }
        public void Dispose()
        {
            World.Dispose(); UnityEngine.Object.DestroyImmediate(registry);
            foreach (var s in sources) s.Dispose(); library.Dispose(); Content.Dispose();
        }
    }
}
