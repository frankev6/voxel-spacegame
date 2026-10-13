using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.App.Spawning
{
    /// <summary>SPEC-01 §5.7: main-thread ship requests, requester state, profile selection and persistent root lookup.</summary>
    public sealed class ShipSpawnService : IShipSpawner, IGridSpawnHook, IDisposable
    {
        readonly World world;
        readonly VoxelStructureRegistry registry;
        readonly ContentCatalog catalog;
        readonly IPersistentIdAllocator ids;
        readonly GalaxyService galaxy;
        readonly EntityQuery gameplay;
        readonly Dictionary<ulong, Entity> roots = new();
        readonly Dictionary<ulong, int> handles = new();
        bool disposed;
        public ShipSpawnService(World world, VoxelStructureRegistry registry, ContentCatalog catalog, IPersistentIdAllocator ids, GalaxyService galaxy)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            if (!world.IsCreated) throw new ArgumentException("World must be alive.", nameof(world));
            this.registry = registry != null ? registry : throw new ArgumentNullException(nameof(registry));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.ids = ids ?? throw new ArgumentNullException(nameof(ids));
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            gameplay = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameplayActive>());
            registry.AddSpawnHook(this);
        }
        public PersistentId Spawn(in ShipSpawnArgs args) => SpawnCore(args, galaxy.State.CurrentStarSystem);
        public PersistentId Spawn(ContentId blueprint, float3 position, quaternion rotation, Allegiance allegiance, bool withAi)
            => Spawn(new ShipSpawnArgs { Blueprint = blueprint, Position = position, Rotation = rotation, Allegiance = allegiance, WithAi = withAi });
        // Fleet records retain ownership of their existing registry borrow through scene exit.
        internal PersistentId SpawnRegistered(in ShipSpawnArgs args, int handle)
        {
            if (handle < 0 || args.Structure == null || !ReferenceEquals(registry.Get(handle), args.Structure))
                throw new ArgumentException("The registered handle must refer to the supplied structure.", nameof(handle));
            return SpawnCore(args, galaxy.State.CurrentStarSystem, handle);
        }
        PersistentId SpawnCore(in ShipSpawnArgs args, StarSystemId starSystem, int registeredHandle = -1)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ShipSpawnService));
            if (!world.IsCreated || gameplay.IsEmptyIgnoreFilter) throw new InvalidOperationException("Ship spawning requires GameplayActive in a live World.");
            ShipBlueprintDefinition blueprint = null;
            if (args.Structure == null && !catalog.TryGet<ShipBlueprintDefinition>(args.Blueprint, out blueprint))
            { GameLog.Error(LogCategories.Content, "Unknown ship blueprint '" + args.Blueprint.Value + "'."); return default; }
            if (!args.IsPlayer && !starSystem.IsValid) throw new InvalidOperationException("NPC spawning requires the actual current or supplied star system.");
            ulong value = args.PersistentId == 0 ? ids.Allocate() : args.PersistentId;
            if (value == 0) throw new InvalidOperationException("Persistent id allocator returned zero.");
            if (TryGetRoot(new PersistentId(value), out _)) throw new InvalidOperationException("A ship already uses persistent id " + value + ".");
            var p = new GridSpawnParams
            {
                Pose = new RigidTransform(Planar.YawRotation(Planar.YawFromForward(math.mul(args.Rotation, new float3(0, 0, 1)))), args.Position),
                LinearVelocity = args.LinearVelocity, Kind = GridKind.Ship, Motion = GridMotion.Dynamic,
                Flags = args.IsPlayer ? GridSpawnFlags.WriteBack : 0,
                Profile = args.IsPlayer ? GridProfile.PlayerShip : args.WithAi ? GridProfile.NpcShip : GridProfile.Dummy,
                PersistentId = value
            };
            var em = world.EntityManager; Entity root = Entity.Null; int handle = -1;
            try
            {
                if (args.Structure != null)
                {
                    handle = registeredHandle >= 0 ? registeredHandle : registry.Register(args.Structure);
                    root = GridSpawnApi.RequestSpawn(em, registry, handle, p);
                }
                else root = GridSpawnApi.RequestSpawnBlueprint(em, blueprint.Id.Hash, p);
                em.AddComponentData(root, args.Allegiance);
                if (!args.IsPlayer) em.AddSharedComponent(root, new StarSystemMember { Id = starSystem });
                if (args.WithAi) em.AddComponentData(root, new AiBrain { ProfileIndex = AiSetup.ProfileIndexOf(catalog, args.AiProfile) });
                roots.Add(value, root); if (handle >= 0 && registeredHandle < 0) handles.Add(value, handle);
                return new PersistentId(value);
            }
            catch
            {
                if (root != Entity.Null && em.Exists(root)) em.DestroyEntity(root);
                if (handle >= 0 && registeredHandle < 0) registry.Release(handle);
                // The grid owner reconciles a cancelled request's read lease on its next update.
                throw;
            }
        }
        public bool TryGetRoot(PersistentId id, out Entity root)
        {
            root = Entity.Null;
            if (disposed || id.IsNone) return false;
            if (roots.TryGetValue(id.Value, out root) && world.IsCreated && world.EntityManager.Exists(root)) return true;
            roots.Remove(id.Value);
            if (handles.Remove(id.Value, out int handle)) registry.Release(handle);
            root = Entity.Null; return false;
        }
        public bool Despawn(PersistentId id)
        {
            if (!TryGetRoot(id, out var root)) return false;
            var em = world.EntityManager;
            // The canonical despawn API acts on converted grids; it cannot cancel an unconverted request.
            if (!em.HasComponent<GridDespawnRequest>(root)) return false;
            GridSpawnApi.RequestDespawn(em, root); return true;
        }
        void IGridSpawnHook.OnEntitiesCreated(EntityManager em, Entity root, NativeArray<Entity> created)
        {
            if (disposed || !em.HasComponent<PersistentId>(root)) return;
            var id = em.GetComponentData<PersistentId>(root);
            if (!id.IsNone && roots.ContainsKey(id.Value)) roots[id.Value] = root;
        }
        PersistentIdValue IShipSpawner.Spawn(in GalaxyShipSpawnArgs args)
        {
            var ship = new ShipSpawnArgs { Blueprint = args.Blueprint, Position = args.Position, Rotation = args.Rotation,
                LinearVelocity = args.LinearVelocity, Allegiance = args.Allegiance, WithAi = args.WithAi, AiProfile = args.AiProfile };
            return new PersistentIdValue(SpawnCore(ship, args.StarSystem).Value);
        }
        bool IShipSpawner.TryGetRoot(ulong persistentId, out Entity root) => TryGetRoot(new PersistentId(persistentId), out root);
        bool IShipSpawner.Despawn(ulong persistentId) => Despawn(new PersistentId(persistentId));
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; registry.RemoveSpawnHook(this);
            foreach (var handle in handles.Values) registry.Release(handle);
            handles.Clear(); roots.Clear(); if (world.IsCreated) gameplay.Dispose();
        }
    }
}
