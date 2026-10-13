using System;
using System.Collections.Generic;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Streaming
{
    /// <summary>Ordered, bounded T2 transitions; retired grids keep their sources until despawn completes.</summary>
    internal sealed class T2Streamer : IDisposable
    {
        readonly EntityManager em;
        readonly GalaxyService galaxy;
        readonly BodyGridSpawner spawner;
        readonly StarSystemDelta delta;
        readonly Dictionary<ulong, LoadedBody> bodies;
        readonly Dictionary<ulong, BodyGridSpawner.Lease> active = new();
        readonly List<(ulong Key, BodyGridSpawner.Lease Lease)> retired = new();
        readonly HashSet<ulong> retiring = new();
        bool closing;
        internal T2Streamer(EntityManager em, GalaxyService galaxy, BodyGridSpawner spawner,
            StarSystemDelta delta, Dictionary<ulong, LoadedBody> bodies)
        { this.em = em; this.galaxy = galaxy; this.spawner = spawner; this.delta = delta; this.bodies = bodies; }

        internal void Tick(StarSystemContent content, double3 positionM, double3 origin, double time, in StreamingConfig config)
        {
            DrainRetired(); if (closing) return;
            int spawned = 0;
            foreach (var spec in content.Bodies)
            {
                ulong key = spec.Id.LocalKey;
                if (!bodies.TryGetValue(key, out var body) || body.WantsGrid == 0 || !em.Exists(body.Anchor)) continue;
                var template = galaxy.GetBodyTemplate(spec.TemplateIndex);
                bool destroyed = (spec.State & BodyStateFlags.Destroyed) != 0;
                if (destroyed && !delta.TryGetStructure(key, out _)) continue;
                double distance = math.max(0, math.distance(positionM, OrbitMath.Position(spec, time)) - spec.RadiusM);
                if (active.TryGetValue(key, out var lease))
                {
                    if (!em.Exists(lease.Root) || distance > template.T2LoadDistanceM * config.T2UnloadFactor)
                    { Retire(key, lease); active.Remove(key); ClearGrid(ref body); bodies[key] = body; }
                    else if (em.HasComponent<GridSpawning>(lease.Root) && !em.IsComponentEnabled<GridSpawning>(lease.Root))
                    {
                        var anchor = em.GetComponentData<StarSystemBody>(body.Anchor);
                        anchor.Flags &= ~BodyFlags.GridPending; em.SetComponentData(body.Anchor, anchor);
                    }
                    continue;
                }
                if (retiring.Contains(key) || distance >= template.T2LoadDistanceM || spawned >= config.MaxGridSpawnsPerFrame) continue;
                lease = spawner.Spawn(spec, template, body.Anchor, delta, origin, time, checked((uint)config.PersistDamageMaxCells));
                active.Add(key, lease); spawned++;
                body.Grid = lease.Root; body.StructureHandle = lease.Handle; body.GridRequested = 1;
                body.GridPersistentId = em.GetComponentData<GridSpawnRequest>(lease.Root).PersistentId;
                var state = em.GetComponentData<StarSystemBody>(body.Anchor);
                state.Grid = lease.Root; state.Flags |= BodyFlags.HasGrid | BodyFlags.GridPending;
                em.SetComponentData(body.Anchor, state); bodies[key] = body;
            }
        }
        internal void Flush()
        {
            foreach (var lease in active.Values) spawner.Capture(lease, delta);
            foreach (var entry in retired) spawner.Capture(entry.Lease, delta);
        }
        void Retire(ulong key, BodyGridSpawner.Lease lease)
        {
            spawner.Capture(lease, delta);
            if (em.Exists(lease.Root))
            {
                if (em.HasComponent<GridDespawnRequest>(lease.Root)) GridSpawnApi.RequestDespawn(em, lease.Root);
                else em.DestroyEntity(lease.Root); // Unconverted request: spawn owner reconciles its read token next update.
            }
            retired.Add((key, lease)); retiring.Add(key);
        }
        void ClearGrid(ref LoadedBody body)
        {
            body.Grid = Entity.Null; body.GridRequested = 0; body.StructureHandle = -1; body.GridPersistentId = 0;
            if (!em.Exists(body.Anchor)) return;
            var state = em.GetComponentData<StarSystemBody>(body.Anchor);
            state.Grid = Entity.Null; state.Flags &= ~(BodyFlags.HasGrid | BodyFlags.GridPending);
            em.SetComponentData(body.Anchor, state);
        }
        internal void BeginRetirement()
        {
            if (closing) return;
            foreach (var pair in active) Retire(pair.Key, pair.Value);
            active.Clear(); closing = true;
        }
        internal bool DrainRetired()
        {
            for (int i = retired.Count - 1; i >= 0; i--)
                if (spawner.Release(retired[i].Lease)) { retiring.Remove(retired[i].Key); retired.RemoveAt(i); }
            return closing && retired.Count == 0;
        }
        public void Dispose()
        {
            BeginRetirement();
            if (!DrainRetired()) throw new InvalidOperationException("T2 sources still have live grids or pending spawn reads.");
            spawner.Dispose();
        }
    }
}
