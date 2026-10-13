using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Interaction;
using Game.Simulation.Spatial;
using Game.Voxel.PhysicsBody;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Collider = Unity.Physics.Collider;

namespace Game.Galaxy.Streaming
{
    internal static class AnchorBuilder
    {
        internal static void Create(World world, GalaxyDef galaxy, StarSystemContent content, double time, double3 origin,
            Dictionary<ulong, LoadedBody> loaded, Dictionary<float, BlobAssetReference<Collider>> colliders)
        {
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            var groups = new SortedDictionary<int, List<BodySpec>>();
            foreach (var spec in content.Bodies)
            {
                if ((spec.State & BodyStateFlags.Destroyed) != 0) continue;
                var template = galaxy.BodyTemplates[spec.TemplateIndex];
                bool orbit = spec.Orbit.Mode != OrbitMode.Fixed || spec.ParentOrbit.Mode != OrbitMode.Fixed;
                bool sphere = template.HasCollider && (template.Role == BodyRole.Star || template.Role == BodyRole.Planet || template.Role == BodyRole.Moon);
                bool interact = template.Interaction != InteractionKind.None && template.StructureSource == StructureSource.None;
                int key = (orbit ? 1 : 0) | (sphere ? 2 : 0) | (interact ? 4 : 0);
                if (!groups.TryGetValue(key, out var entries)) { entries = new List<BodySpec>(); groups.Add(key, entries); }
                entries.Add(spec);
            }
            using var all = new NativeList<Entity>(Allocator.Temp);
            try
            {
                foreach (var group in groups)
                {
                    bool orbit = (group.Key & 1) != 0, sphere = (group.Key & 2) != 0, interact = (group.Key & 4) != 0;
                    var types = new List<ComponentType> { typeof(StarSystemBody), typeof(UniversePosition), typeof(LocalTransform), typeof(LocalToWorld) };
                    if (orbit) { types.Add(typeof(BodyOrbit)); types.Add(typeof(UniversePositionIsAnalytic)); }
                    if (sphere) { types.Add(typeof(PhysicsCollider)); types.Add(typeof(PhysicsWorldIndex)); types.Add(typeof(SpatialBody)); }
                    if (interact) types.Add(typeof(Interactable));
                    using var entities = new NativeArray<Entity>(group.Value.Count, Allocator.TempJob);
                    using var specs = new NativeArray<BodySpec>(group.Value.ToArray(), Allocator.TempJob);
                    em.CreateEntity(em.CreateArchetype(types.ToArray()), entities); all.AddRange(entities);
                    var flags = (orbit ? BodyFlags.OnRails : BodyFlags.None) | (sphere ? BodyFlags.HasCollider : BodyFlags.None);
                    world.GetOrCreateSystemManaged<AnchorFillSystem>().Fill(entities, specs, time, origin, flags, orbit);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        var spec = specs[i]; var entity = entities[i]; var template = galaxy.BodyTemplates[spec.TemplateIndex];
                        if (sphere)
                        {
                            if (!colliders.TryGetValue(spec.RadiusM, out var blob))
                            {
                                blob = SphereCollider.Create(new SphereGeometry { Center = float3.zero, Radius = spec.RadiusM }, PhysicsLayers.Celestial, PhysicsLayers.CelestialMaterial);
                                try { colliders.Add(spec.RadiusM, blob); }
                                catch { blob.Dispose(); throw; }
                            }
                            em.SetComponentData(entity, new PhysicsCollider { Value = blob });
                            em.SetComponentData(entity, new SpatialBody { Radius = spec.RadiusM });
                        }
                        if (interact) em.SetComponentData(entity, new Interactable { Kind = template.Interaction,
                            TemplateIndex = spec.TemplateIndex, DistanceM = template.InteractionDistanceM, Body = spec.Id });
                        loaded.Add(spec.Id.LocalKey, new LoadedBody { Anchor = entity, StructureHandle = -1 });
                    }
                }
                em.AddSharedComponent(all.AsArray(), new StarSystemMember { Id = content.Id });
                // TODO(SYS-012): registered visual children join this T1 batch; no synthetic meshes or second rendering owner.
            }
            catch
            {
                em.DestroyEntity(all.AsArray()); loaded.Clear();
                // Collider ownership stays with the loader, which retires its cache even on failed entry.
                throw;
            }
        }
    }
}
