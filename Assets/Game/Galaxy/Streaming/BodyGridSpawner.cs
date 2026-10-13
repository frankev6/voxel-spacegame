using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Interaction;
using Game.Galaxy.Services;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Streaming
{
    /// <summary>Owns procedural source caches and per-grid borrows for one loaded system.</summary>
    internal sealed class BodyGridSpawner : IDisposable
    {
        internal sealed class Lease
        {
            internal Entity Root;
            internal VoxelStructure Source;
            internal int Handle;
            internal bool OwnsSource;
        }
        readonly EntityManager em;
        readonly VoxelStructureRegistry registry;
        readonly BlueprintLibrary blueprints;
        readonly ContentCatalog catalog;
        readonly VoxelContent content;
        readonly IPersistentIdAllocator ids;
        readonly ProceduralShapeBuilder shapes;
        readonly Dictionary<(ushort, ulong), VoxelStructure> cachedShapes = new();
        readonly List<Lease> leases = new();
        internal BodyGridSpawner(EntityManager em, VoxelStructureRegistry registry, BlueprintLibrary blueprints,
            ContentCatalog catalog, in VoxelContent content, IPersistentIdAllocator ids)
        {
            this.em = em; this.registry = registry; this.blueprints = blueprints;
            this.catalog = catalog; this.content = content; this.ids = ids;
            shapes = new ProceduralShapeBuilder(catalog, content);
        }
        internal Lease Spawn(in BodySpec spec, BodyTemplateDefinition template, Entity anchor,
            StarSystemDelta delta, double3 origin, double time, uint persistCellLimit)
        {
            VoxelStructure source; bool owns = false;
            bool persisted = delta.TryGetStructure(spec.Id.LocalKey, out var saved);
            if (persisted) source = saved.Structure;
            else if (template.StructureSource == StructureSource.Blueprint)
            {
                if (template.Blueprint == null || !blueprints.TryGet(template.Blueprint.Id.Hash, out source))
                    throw new InvalidOperationException("Body blueprint is unavailable: " + template.IdString);
            }
            else if (template.StructureSource == StructureSource.ProceduralShape)
            {
                var key = (spec.TemplateIndex, spec.Seed);
                if (!cachedShapes.TryGetValue(key, out source))
                { source = shapes.Build(template.Shape, spec.Seed); cachedShapes.Add(key, source); }
            }
            else throw new InvalidOperationException("Voxel body has no structure source: " + template.IdString);
            bool persist = persisted || template.Destructible && source.SolidCellCount <= persistCellLimit;
            if (persist && !persisted) { source = source.Clone(); owns = true; }
            Lease lease = null;
            try
            {
            bool armed = false;
            foreach (var instance in source.Data.Instances)
                if (instance.Block != 0 && instance.CellsAlive > 0 && (instance.Flags & BlockInstanceFlags.Destroyed) == 0 &&
                    content.Blocks.Value.Blocks[instance.Block].HasModule(BlockModuleId.WeaponMount))
                { armed = true; break; }
            bool hulk = persisted && (spec.State & BodyStateFlags.Destroyed) != 0;
            ulong id = ids.Allocate();
            double3 position = OrbitMath.Position(spec, time);
            bool borrowBlueprint = !persist && template.StructureSource == StructureSource.Blueprint;
            lease = new Lease { Source = source, Handle = -1, OwnsSource = owns };
            if (!borrowBlueprint) lease.Handle = registry.Register(source);
            leases.Add(lease); // Keep ownership visible even if later component construction fails.
            var spawn = new GridSpawnParams
            {
                PersistentId = id, Pose = new RigidTransform(quaternion.RotateY(spec.YawRad), (float3)(position - origin)),
                Kind = hulk ? GridKind.Hulk : GridKind.Station, Motion = GridMotion.Static,
                Profile = armed && !hulk ? GridProfile.Station : GridProfile.None,
                Flags = persist ? GridSpawnFlags.WriteBack : GridSpawnFlags.None
            };
            lease.Root = borrowBlueprint
                ? GridSpawnApi.RequestSpawnBlueprint(em, template.Blueprint.Id.Hash, spawn)
                : GridSpawnApi.RequestSpawn(em, registry, lease.Handle, spawn);
            em.AddSharedComponent(lease.Root, new StarSystemMember { Id = spec.Id.System });
            em.AddComponentData(lease.Root, new UniversePosition { Value = position });
            if (spec.Orbit.Mode != OrbitMode.Fixed || spec.ParentOrbit.Mode != OrbitMode.Fixed)
            {
                em.AddComponentData(lease.Root, new BodyOrbit { Orbit = spec.Orbit, ParentOrbit = spec.ParentOrbit });
                em.AddComponent<UniversePositionIsAnalytic>(lease.Root);
            }
            em.AddComponentData(lease.Root, new BodyGridOwner { Body = spec.Id, Anchor = anchor, PersistDamage = (byte)(persist ? 1 : 0) });
            em.AddComponentData(lease.Root, new BodyGridInitialCells { Cells = checked((uint)source.SolidCellCount) });
            if (template.Interaction != InteractionKind.None)
                em.AddComponentData(lease.Root, new Interactable { Body = spec.Id, TemplateIndex = spec.TemplateIndex,
                    Kind = template.Interaction, DistanceM = template.InteractionDistanceM });
            byte faction = Game.Core.Services.TryGet<IStarSystemOwnership>(out var ownership)
                ? ownership.GetOwnerFaction(spec.Id.System) : (byte)255;
            em.AddComponentData(lease.Root, new Allegiance { Faction = faction });
            if (armed && !hulk) em.AddComponentData(lease.Root, new AiBrain { ProfileIndex = AiSetup.ProfileIndexOf(catalog, new ContentId("ai_turret")) });
            if (!template.Destructible) em.AddComponent<DamageImmune>(lease.Root);
            if (hulk)
            {
                em.AddComponent<Hulk>(lease.Root);
                em.AddComponentData(lease.Root, new HulkInfo { Cause = HulkCause.CoreLost, OriginPersistentId = id, FormerKind = GridKind.Station });
            }
            return lease;
            }
            catch
            {
                if (lease != null)
                {
                    if (em.Exists(lease.Root)) em.DestroyEntity(lease.Root);
                    registry.ReconcileAbandonedReads(em);
                    registry.Release(lease.Handle); leases.Remove(lease);
                }
                if (owns && source.Data.IsCreated) source.Dispose();
                throw;
            }
        }
        internal void Capture(Lease lease, StarSystemDelta delta)
        {
            // Even an unfinished grid must not lose its writable source when it is unloaded.
            // The spawn/despawn owner can finish writeback into this same delta-owned clone.
            if (lease.OwnsSource && em.Exists(lease.Root) && em.HasComponent<BodyGridOwner>(lease.Root))
            {
                var owner = em.GetComponentData<BodyGridOwner>(lease.Root);
                if (owner.PersistDamage != 0)
                {
                    uint revision = em.HasComponent<VoxelGrid>(lease.Root) ? em.GetComponentData<VoxelGrid>(lease.Root).Revision : 0;
                    delta.SetStructure(owner.Body.LocalKey, revision, lease.Source);
                    lease.OwnsSource = false;
                }
            }
            if (DamageCapture.Capture(em, lease.Root, registry, delta)) lease.OwnsSource = false;
        }
        internal bool Release(Lease lease)
        {
            // Despawn processing owns read-token cancellation. Never dispose a source while that job still reads it.
            if (em.Exists(lease.Root) && (em.HasComponent<VoxelGrid>(lease.Root) || em.HasComponent<GridSpawnRequest>(lease.Root))) return false;
            if (lease.Handle >= 0)
            {
                if (lease.Source.IsPinned) return false;
                registry.Release(lease.Handle);
            }
            if (lease.OwnsSource) lease.Source.Dispose();
            leases.Remove(lease); return true;
        }
        public void Dispose()
        {
            for (int i = leases.Count - 1; i >= 0; i--)
                if (!Release(leases[i])) throw new InvalidOperationException("Body sources must outlive their grids and spawn readers.");
            foreach (var source in cachedShapes.Values) source.Dispose();
            cachedShapes.Clear();
        }
    }
}
