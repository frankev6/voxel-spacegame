using System;
using System.Collections.Generic;
using Game.Core;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Streaming
{
    /// <summary>Owns generated asteroid sources until their grid requests and read leases have retired.</summary>
    internal sealed class AsteroidGridSpawner : IDisposable
    {
        internal sealed class Lease
        {
            internal Entity Root;
            internal VoxelStructure Source;
            internal int Handle = -1;
            internal ulong BodyKey;
        }
        readonly EntityManager em;
        readonly VoxelStructureRegistry registry;
        readonly IPersistentIdAllocator ids;
        readonly Dictionary<ulong, LoadedBody> bodies;
        readonly List<Lease> leases = new();
        internal AsteroidGridSpawner(EntityManager em, VoxelStructureRegistry registry, IPersistentIdAllocator ids,
            Dictionary<ulong, LoadedBody> bodies)
        { this.em = em; this.registry = registry; this.ids = ids; this.bodies = bodies; }

        // Consumes source even on failure. Asteroid damage is never written back or transferred to a delta.
        internal Lease Spawn(in AsteroidSpawnSpec spec, ushort template, VoxelStructure source, double3 origin)
        {
            var lease = new Lease { Source = source, BodyKey = spec.Id.LocalKey };
            try
            {
                lease.Handle = registry.Register(source);
                lease.Root = GridSpawnApi.RequestSpawn(em, registry, lease.Handle, new GridSpawnParams
                {
                    PersistentId = ids.Allocate(),
                    Pose = new RigidTransform(quaternion.RotateY(spec.YawRad), (float3)(spec.PositionM - origin)),
                    Kind = GridKind.Asteroid, Motion = GridMotion.Dynamic, Profile = GridProfile.None, Flags = GridSpawnFlags.None
                });
                em.AddSharedComponent(lease.Root, new StarSystemMember { Id = spec.Id.System });
                em.AddComponentData(lease.Root, new UniversePosition { Value = spec.PositionM });
                // A T3 asteroid's dynamic grid is also its body anchor; no second transform can drift from physics.
                em.AddComponentData(lease.Root, new StarSystemBody { Id = spec.Id, TemplateIndex = template,
                    Flags = BodyFlags.Asteroid | BodyFlags.HasGrid, RadiusM = spec.RadiusM, Grid = lease.Root });
                em.AddComponentData(lease.Root, new BodyGridOwner { Body = spec.Id, Anchor = lease.Root });
                em.AddComponentData(lease.Root, new BodyGridInitialCells { Cells = checked((uint)source.SolidCellCount) });
                bodies.Add(spec.Id.LocalKey, new LoadedBody { Anchor = lease.Root, Grid = lease.Root,
                    GridPersistentId = em.GetComponentData<GridSpawnRequest>(lease.Root).PersistentId,
                    StructureHandle = lease.Handle, GridRequested = 1 });
                leases.Add(lease);
                return lease;
            }
            catch
            {
                if (em.Exists(lease.Root)) em.DestroyEntity(lease.Root);
                if (bodies.TryGetValue(lease.BodyKey, out var body) && body.Grid == lease.Root) bodies.Remove(lease.BodyKey);
                registry.ReconcileAbandonedReads(em);
                if (lease.Handle >= 0) registry.Release(lease.Handle);
                source.Dispose();
                throw;
            }
        }
        internal void Retire(Lease lease)
        {
            if (bodies.TryGetValue(lease.BodyKey, out var body) && body.Grid == lease.Root) bodies.Remove(lease.BodyKey);
            if (!em.Exists(lease.Root)) return;
            if (em.HasComponent<GridDespawnRequest>(lease.Root)) GridSpawnApi.RequestDespawn(em, lease.Root);
            else em.DestroyEntity(lease.Root);
        }
        internal bool TryRelease(Lease lease)
        {
            if (em.Exists(lease.Root) && (em.HasComponent<VoxelGrid>(lease.Root) || em.HasComponent<GridSpawnRequest>(lease.Root))) return false;
            if (lease.Source.IsPinned) return false;
            registry.Release(lease.Handle);
            lease.Source.Dispose(); leases.Remove(lease);
            return true;
        }
        public void Dispose()
        {
            for (int i = leases.Count - 1; i >= 0; i--)
                if (!TryRelease(leases[i])) throw new InvalidOperationException("Asteroid sources still have live grids or readers.");
        }
    }
}
