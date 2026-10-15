using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Net.Protocol;
using Game.Net.Replication.Grids;
using Game.Net.Transport;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.Replica;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Net.Replication
{
    /// <summary>Guest side of ADR-0116. Reliable messages are queued in stream order and applied at the start of the frame
    /// (GameInitGroup, before GridSpawnSystem); snapshots go straight into interpolation tracks. Main thread.</summary>
    public sealed class GuestReplicator : IDisposable
    {
        sealed class GuestGrid
        {
            public Entity Entity;
            public int Handle = -1;
            public VoxelStructure Structure;
            public GridSpawnHeader Header;
            public bool Spawned, Despawning;
        }

        readonly NetTransport transport;
        readonly Queue<(NetMessage Kind, byte[] Data)> queue = new();
        readonly Dictionary<ulong, GuestGrid> grids = new();
        readonly Dictionary<ulong, Entity> chunks = new();
        readonly Dictionary<ulong, ReplicaTrack> tracks = new();
        readonly List<GuestGrid> retiring = new();
        readonly NetReader reader = new();
        NativeArray<Cell> cells;
        NativeList<ReplicaCellWrite> writes;
        NativeList<CellRemoved> removedCells;
        double clockOffset; bool clockValid;
        public uint NewestHostTick { get; private set; }
        public double3 HostOrigin { get; private set; }
        public bool HasOrigin { get; private set; }
        public bool JoinComplete { get; private set; }
        public int PendingMessages => queue.Count;
        public int GridCount => grids.Count;
        public int ChunkCount => chunks.Count;
        public string LastHashReport { get; private set; } = "no hash check yet";
        public int HashMismatches { get; private set; }
        uint localTick;

        public GuestReplicator(NetTransport transport)
        {
            this.transport = transport;
            cells = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Persistent);
            writes = new NativeList<ReplicaCellWrite>(BrickMath.CellsPerBrick, Allocator.Persistent);
            removedCells = new NativeList<CellRemoved>(256, Allocator.Persistent);
            foreach (var kind in new[] { NetMessage.JoinBegin, NetMessage.JoinEnd, NetMessage.GridSpawn, NetMessage.GridDespawn, NetMessage.GridDelta,
                         NetMessage.GridState, NetMessage.ChunkSpawn, NetMessage.ChunkDespawn, NetMessage.HashReport })
            {
                var k = kind;
                transport.Register(k, (peer, r) => Enqueue(k, r));
            }
            transport.Register(NetMessage.Snapshot, (peer, r) => OnSnapshot(r));
        }

        void Enqueue(NetMessage kind, NetReader r)
        {
            var copy = new byte[r.Remaining];
            for (int i = 0; i < copy.Length; i++) copy[i] = r.U8();
            queue.Enqueue((kind, copy));
        }

        static double Now => UnityEngine.Time.realtimeSinceStartupAsDouble;

        // ---------------- snapshots ----------------

        void OnSnapshot(NetReader r)
        {
            uint tick = r.U32(); double3 origin = r.D3(); int count = r.U8();
            if (tick > NewestHostTick || !HasOrigin) { NewestHostTick = Math.Max(tick, NewestHostTick); HostOrigin = origin; HasOrigin = true; }
            double measured = tick - Now * 60;
            if (!clockValid) { clockOffset = measured; clockValid = true; }
            else if (measured > clockOffset + 30 || measured < clockOffset - 30) clockOffset = measured;   // host restart or long stall
            else clockOffset += (measured - clockOffset) * (measured > clockOffset ? .2 : .02);           // follow late packets quickly, early ones slowly
            for (int i = 0; i < count; i++)
            {
                var s = new ReplicaSample { Tick = tick, Scale = 1 };
                ulong id = r.U64(); byte flags = r.U8();
                s.Position = (double3)r.F3() + origin; s.Rotation = r.Quat(); s.Linear = r.F3(); s.Angular = r.F3();
                if ((flags & 1) != 0)
                {
                    s.HasCommand = 1;
                    s.Command = new ShipCommand { Thrust = r.F2(), Turn = r.F32(), AimWorld = r.F3(), AimValid = r.U8(), FireGroups = r.U32(), Flags = (ShipCommandFlags)r.U8() };
                    s.Command.AimWorld += (float3)origin;   // carried as universe so a rebase between samples is harmless
                }
                if ((flags & 2) != 0) { s.HasShield = 1; s.ShieldHp = r.F32(); s.ShieldMax = r.F32(); s.ShieldActive = r.U8(); }
                if ((flags & 4) != 0) s.Scale = r.F32();
                if (!tracks.TryGetValue(id, out var track)) tracks[id] = track = new ReplicaTrack();
                if (!track.Add(s)) transport.Stats.DroppedSnapshots++;
            }
        }

        /// <summary>Host tick the guest should display now: the estimated current host tick minus the interpolation delay.</summary>
        public double RenderTick => clockValid ? Now * 60 + clockOffset - NetProtocol.InterpolationDelayTicks : 0;
        public float SnapshotAgeMs => clockValid ? (float)((Now * 60 + clockOffset - NewestHostTick) / 60 * 1000) : 0;
        public bool TryGetTrack(ulong id, out ReplicaTrack track) => tracks.TryGetValue(id, out track);

        // ---------------- reliable apply ----------------

        /// <summary>ReplicaApplySystem: applies queued messages in order until one must wait for a grid that is still spawning.</summary>
        public void Apply(EntityManager em, VoxelStructureRegistry registry, byte localSlot)
        {
            localTick++;
            em.CompleteAllTrackedJobs();
            FinishSpawns(em);
            RetireDespawned(em, registry);
            removedCells.Clear();
            int budget = 2000;   // messages per frame; the join snapshot spreads over a few frames
            while (queue.Count > 0 && budget-- > 0)
            {
                var (kind, data) = queue.Peek();
                reader.Reset(data, 0, data.Length);
                try
                {
                    if (!ApplyOne(em, registry, localSlot, kind, reader)) break;
                }
                catch (Exception e) { GameLog.Exception(NetTransport.Category, e, "apply " + kind); transport.Stats.Desyncs++; }
                queue.Dequeue();
            }
            if (removedCells.Length > 0) ReplicaVoxelApi.EmitDebris(em, removedCells);
            transport.Stats.ReplicatedGrids = grids.Count; transport.Stats.ReplicatedChunks = chunks.Count;
            transport.Stats.SnapshotAgeMs = SnapshotAgeMs;
        }

        bool Ready(ulong id, out GuestGrid g) => grids.TryGetValue(id, out g) && g.Spawned;
        bool Waiting(ulong id) => grids.TryGetValue(id, out var g) && !g.Spawned;

        bool ApplyOne(EntityManager em, VoxelStructureRegistry registry, byte localSlot, NetMessage kind, NetReader r)
        {
            switch (kind)
            {
                case NetMessage.JoinBegin:
                    JoinComplete = false; GameLog.Info(NetTransport.Category, "join snapshot: " + (r.Remaining >= 6 ? "tick " + r.U32() + ", " + r.U16() + " grids" : "")); return true;
                case NetMessage.JoinEnd:
                    JoinComplete = true; GameLog.Info(NetTransport.Category, "join snapshot complete"); return true;
                case NetMessage.GridSpawn: Spawn(em, registry, localSlot, r.Inflated()); return true;
                case NetMessage.GridDespawn:
                {
                    ulong id = r.U64();
                    if (Waiting(id)) return false;
                    if (grids.Remove(id, out var g))
                    {
                        if (em.Exists(g.Entity) && em.HasComponent<GridDespawnRequest>(g.Entity)) GridSpawnApi.RequestDespawn(em, g.Entity);
                        g.Despawning = true; retiring.Add(g);
                    }
                    tracks.Remove(id);
                    return true;
                }
                case NetMessage.GridState:
                {
                    ulong id = r.U64(); var gridKind = (GridKind)r.U8(); bool hulk = r.Bool();
                    if (Waiting(id)) return false;
                    if (Ready(id, out var g) && em.Exists(g.Entity))
                    {
                        var grid = em.GetComponentData<VoxelGrid>(g.Entity); grid.Kind = gridKind; em.SetComponentData(g.Entity, grid);
                        if (hulk && !em.HasComponent<Hulk>(g.Entity)) em.AddComponent<Hulk>(g.Entity);
                    }
                    return true;
                }
                case NetMessage.GridDelta:
                {
                    var payload = r.U8() == 1 ? r.Inflated() : r;
                    ulong id = payload.U64();
                    if (Waiting(id)) return false;
                    if (!Ready(id, out var g) || !em.Exists(g.Entity)) { transport.Stats.Desyncs++; return true; }
                    ApplyDelta(em, g.Entity, payload);
                    return true;
                }
                case NetMessage.ChunkSpawn: SpawnChunk(em, r); return true;
                case NetMessage.ChunkDespawn:
                {
                    ulong id = r.U64();
                    if (chunks.Remove(id, out var entity)) ReplicaVoxelApi.DestroyChunk(em, entity);
                    tracks.Remove(id);
                    return true;
                }
                case NetMessage.HashReport: CompareHashes(em, r); return true;
            }
            return true;
        }

        void Spawn(EntityManager em, VoxelStructureRegistry registry, byte localSlot, NetReader body)
        {
            var header = GridCodec.ReadHeader(body);
            if (grids.TryGetValue(header.NetId, out var old))
            {
                // A respawn under the same id: drop the old replica first.
                grids.Remove(header.NetId);
                if (old.Spawned && em.Exists(old.Entity)) GridSpawnApi.RequestDespawn(em, old.Entity);
                old.Despawning = true; retiring.Add(old);
            }
            var structure = GridCodec.ReadStructure(body, header.CellSize);
            int handle = registry.Register(structure);
            bool mine = header.OwnerSlot == localSlot;
            var profile = header.Kind == GridKind.Hulk || header.Kind == GridKind.Asteroid ? GridProfile.None
                : header.Role == GridRoleHint.Ship ? (mine ? GridProfile.PlayerShip : GridProfile.Dummy)
                : header.Role == GridRoleHint.Station ? GridProfile.Station : GridProfile.None;
            var p = new GridSpawnParams
            {
                Pose = new RigidTransform(header.Rotation, header.Position), LinearVelocity = header.Linear,
                Kind = header.Kind, Motion = header.Motion == GridMotion.Static ? GridMotion.Static : GridMotion.Kinematic,
                Flags = header.Flags & ~GridSpawnFlags.WriteBack, Profile = profile, PersistentId = header.NetId
            };
            Entity entity;
            try { entity = GridSpawnApi.RequestSpawn(em, registry, handle, p); }
            catch { registry.Release(handle); structure.Dispose(); throw; }
            if (header.HasAllegiance != 0) em.AddComponentData(entity, header.Allegiance);
            em.AddComponentData(entity, new Replicated { NetId = header.NetId, OwnedLocally = (byte)(mine ? 1 : 0) });
            grids[header.NetId] = new GuestGrid { Entity = entity, Handle = handle, Structure = structure, Header = header };
        }

        void FinishSpawns(EntityManager em)
        {
            foreach (var pair in grids)
            {
                var g = pair.Value;
                if (g.Spawned) continue;
                if (!em.Exists(g.Entity))
                {
                    GameLog.Warn(NetTransport.Category, "replica spawn of grid " + pair.Key + " was rejected");
                    g.Spawned = true; transport.Stats.Desyncs++; continue;
                }
                if (!em.HasComponent<GridSpawning>(g.Entity) || em.IsComponentEnabled<GridSpawning>(g.Entity)) continue;
                g.Spawned = true;
                if (g.Header.Kind == GridKind.Hulk && !em.HasComponent<Hulk>(g.Entity)) em.AddComponent<Hulk>(g.Entity);
            }
        }

        void RetireDespawned(EntityManager em, VoxelStructureRegistry registry)
        {
            for (int i = retiring.Count - 1; i >= 0; i--)
            {
                var g = retiring[i];
                if (em.Exists(g.Entity))
                {
                    // Spawn finished after the despawn arrived: request it now.
                    if (em.HasComponent<GridSpawning>(g.Entity) && !em.IsComponentEnabled<GridSpawning>(g.Entity)
                        && em.HasComponent<GridDespawnRequest>(g.Entity) && !em.IsComponentEnabled<GridDespawnRequest>(g.Entity))
                        GridSpawnApi.RequestDespawn(em, g.Entity);
                    continue;
                }
                if (g.Handle >= 0) registry.Release(g.Handle);
                g.Structure?.Dispose();
                retiring.RemoveAt(i);
            }
        }

        void ApplyDelta(EntityManager em, Entity root, NetReader r)
        {
            int paletteStart = r.U16(), paletteCount = r.U16();
            if (paletteCount > 0)
            {
                using var entries = new NativeArray<PaletteEntry>(paletteCount, Allocator.Temp);
                var e = entries;
                for (int i = 0; i < paletteCount; i++) e[i] = GridCodec.ReadPaletteEntry(r);
                ReplicaVoxelApi.SetPalette(em, root, paletteStart, entries);
            }
            int instanceCount = r.U16();
            for (int i = 0; i < instanceCount; i++)
            {
                int index = r.U16(); float hp = r.F32(); ushort alive = r.U16(); var flags = (BlockInstanceFlags)r.U8();
                ReplicaVoxelApi.SetInstance(em, root, index, hp, alive, flags);
            }
            uint ops = r.U32();
            for (uint o = 0; o < ops; o++)
            {
                byte op = r.U8(); int3 coord;
                writes.Clear();
                if (op == 0)
                {
                    coord = r.I3();
                    for (int i = 0; i < BrickMath.CellsPerBrick; i++) writes.Add(new ReplicaCellWrite { Local = (ushort)i });
                }
                else if (op == 1)
                {
                    coord = GridCodec.ReadBrickCells(r, cells);
                    for (int i = 0; i < BrickMath.CellsPerBrick; i++) writes.Add(new ReplicaCellWrite { Local = (ushort)i, Value = cells[i] });
                }
                else
                {
                    coord = r.I3(); int n = r.U16();
                    for (int i = 0; i < n; i++) writes.Add(new ReplicaCellWrite { Local = r.U16(), Value = GridCodec.ReadCell(r) });
                }
                if (!ReplicaVoxelApi.WriteBrick(em, root, coord, writes.AsArray(), removedCells) && op != 0) transport.Stats.Desyncs++;
            }
        }

        void SpawnChunk(EntityManager em, NetReader r)
        {
            ulong id = r.U64(); float cellSize = r.F32(); float3 localOrigin = r.F3();
            var pose = new LocalTransform { Position = r.F3(), Rotation = r.Quat(), Scale = r.F32() };
            float3 linear = r.F3(); uint end = r.U32(), dissolve = r.U32();
            int n = r.U16();
            using var list = new NativeArray<ChunkCell>(n, Allocator.Temp);
            var l = list;
            for (int i = 0; i < n; i++) l[i] = new ChunkCell { X = r.U8(), Y = r.U8(), Z = r.U8(), Orient = r.U8(), Block = r.U16(), Color = r.U8() };
            if (chunks.Remove(id, out var old)) ReplicaVoxelApi.DestroyChunk(em, old);
            if (n == 0) return;
            uint tick = localTick;
            var entity = ReplicaVoxelApi.CreateChunk(em, cellSize, localOrigin, list, pose, tick + end, tick + dissolve, tick);
            em.AddComponentData(entity, new Replicated { NetId = id });
            if (em.HasComponent<Unity.Physics.PhysicsVelocity>(entity)) em.SetComponentData(entity, new Unity.Physics.PhysicsVelocity { Linear = linear });
            chunks[id] = entity;
        }

        void CompareHashes(EntityManager em, NetReader r)
        {
            uint tick = r.U32(); int count = r.U16(); int checkedGrids = 0, mismatched = 0;
            var bad = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                ulong id = r.U64(); ulong hash = r.U64();
                if (!Ready(id, out var g) || !em.Exists(g.Entity)) continue;
                checkedGrids++;
                ulong mine = ReplicaVoxelApi.HashCells(em, g.Entity);
                if (mine != hash) { mismatched++; if (bad.Length < 200) bad.Append(' ').Append(id); }
            }
            HashMismatches += mismatched; transport.Stats.Desyncs += mismatched;
            LastHashReport = "host tick " + tick + ": " + checkedGrids + " grids checked, " + mismatched + " mismatched" + (mismatched > 0 ? " (" + bad + ")" : "");
            if (mismatched > 0) GameLog.Warn(NetTransport.Category, "cell hash mismatch: " + LastHashReport);
            else GameLog.Info(NetTransport.Category, "cell hash check: " + LastHashReport);
        }

        public bool TryGetEntity(ulong id, out Entity entity)
        {
            entity = Entity.Null;
            if (grids.TryGetValue(id, out var g) && g.Spawned) { entity = g.Entity; return true; }
            return chunks.TryGetValue(id, out entity);
        }

        /// <summary>Forgets every replica (the World scene teardown already destroyed the entities).</summary>
        public void Reset(VoxelStructureRegistry registry)
        {
            foreach (var g in grids.Values) { if (g.Handle >= 0) registry?.Release(g.Handle); g.Structure?.Dispose(); }
            foreach (var g in retiring) { if (g.Handle >= 0) registry?.Release(g.Handle); g.Structure?.Dispose(); }
            grids.Clear(); retiring.Clear(); chunks.Clear(); tracks.Clear(); queue.Clear();
            clockValid = false; NewestHostTick = 0; HasOrigin = false; JoinComplete = false; HashMismatches = 0;
        }

        public void Dispose()
        {
            if (cells.IsCreated) cells.Dispose();
            if (writes.IsCreated) writes.Dispose();
            if (removedCells.IsCreated) removedCells.Dispose();
        }
    }
}
