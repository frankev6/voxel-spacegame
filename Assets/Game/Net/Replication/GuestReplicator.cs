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
    /// (GameInitGroup, before GridSpawnSystem); snapshots go straight into interpolation tracks. Cell and instance changes are only
    /// translated here: they are queued as exact writes and applied in the next tick by the same code as the host's damage
    /// (ADR-0117). Main thread.</summary>
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
        NativeList<ExactCellWrite> exact, instanceWrites;   // this frame's decoded writes, queued to DamageStreams after the apply loop
        EntityQuery streamsQuery;
        double clockOffset; bool clockValid;
        public uint NewestHostTick { get; private set; }
        public double3 HostOrigin { get; private set; }
        public bool HasOrigin { get; private set; }
        public bool JoinComplete { get; private set; }
        public int PendingMessages => queue.Count;
        public int GridCount => grids.Count;
        public int ChunkCount => chunks.Count;
        public string LastHashReport { get; private set; } = "no hash check yet";
        public string AppliedSummary { get; private set; } = "";
        int lastMissing;
        public int HashMismatches { get; private set; }
        public OwnShipPredictor Predictor { get; } = new();
        uint localTick;
        readonly double[] kindMs = new double[256];
        readonly int[] kindCount = new int[256];
        static double Ms(long start) => (NetStats.Stamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        public GuestReplicator(NetTransport transport)
        {
            this.transport = transport;
            cells = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Persistent);
            exact = new NativeList<ExactCellWrite>(BrickMath.CellsPerBrick, Allocator.Persistent);
            instanceWrites = new NativeList<ExactCellWrite>(64, Allocator.Persistent);
            foreach (var kind in new[] { NetMessage.JoinBegin, NetMessage.JoinEnd, NetMessage.GridSpawn, NetMessage.GridDespawn, NetMessage.GridDelta,
                         NetMessage.GridState, NetMessage.AbilityState, NetMessage.GridSalvage, NetMessage.PilotStatus, NetMessage.ChunkSpawn, NetMessage.ChunkDespawn, NetMessage.HashReport })
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
                if ((flags & 8) != 0) { s.HasWeapons = 1; s.Target = r.U64(); s.Autofire = r.U8(); s.AimMode = r.U8(); }
                if ((flags & 16) != 0) { s.HasAck = 1; s.Ack = r.U32(); }
                if ((flags & 64) != 0) { s.AimAnchor = r.U64(); s.AimLocal = r.F3(); }
                int plumes = (flags & 32) != 0 ? r.U8() : 0;
                if (!tracks.TryGetValue(id, out var track)) tracks[id] = track = new ReplicaTrack();
                bool fresh = track.Add(s);
                if (!fresh) transport.Stats.DroppedSnapshots++;
                if ((flags & 32) != 0)
                {
                    if (fresh) { track.PlumeCount = Math.Min(plumes, ReplicaTrack.MaxPlumes); track.PlumeVersion++; }
                    for (int k = 0; k < plumes; k++)
                    {
                        ushort instance = r.U16(); byte level = r.U8();
                        if (fresh && k < ReplicaTrack.MaxPlumes) { track.PlumeInstances[k] = instance; track.PlumeLevels[k] = level; }
                    }
                }
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
            exact.Clear();
            int budget = 2000;   // messages per frame; the join snapshot spreads over a few frames
            long frameStart = NetStats.Stamp();
            Array.Clear(kindMs, 0, kindMs.Length); Array.Clear(kindCount, 0, kindCount.Length);
            while (queue.Count > 0 && budget-- > 0)
            {
                var (kind, data) = queue.Peek();
                reader.Reset(data, 0, data.Length);
                long start = NetStats.Stamp();
                try
                {
                    if (!ApplyOne(em, registry, localSlot, kind, reader)) break;
                }
                catch (Exception e) { GameLog.Exception(NetTransport.Category, e, "apply " + kind); transport.Stats.Desyncs++; }
                kindMs[(byte)kind] += Ms(start); kindCount[(byte)kind]++;
                queue.Dequeue();
            }
            var streams = Streams(em);
            if (exact.Length > 0) streams.QueueExact(exact.AsArray());
            // Writes the shared applier could not place (no grid or brick here) are desyncs too.
            int missing = streams.ExactMissing; transport.Stats.Desyncs += missing - lastMissing; lastMissing = missing;
            var totals = streams.Totals;
            AppliedSummary = "applied by the shared cell writer: " + totals.CellsDestroyed + " cells destroyed, " + totals.CellsDetached + " detached, "
                + totals.CellsPlaced + " placed, " + totals.DebrisParticles + " debris, " + missing + " writes missing";
            double total = Ms(frameStart);
            if (total > 8)
            {
                var text = new System.Text.StringBuilder("slow apply ").Append(total.ToString("0.0")).Append(" ms:");
                for (int k = 0; k < kindMs.Length; k++)
                    if (kindCount[k] > 0) text.Append(' ').Append((NetMessage)k).Append(' ').Append(kindMs[k].ToString("0.0")).Append(" ms x").Append(kindCount[k]);
                text.Append(", ").Append(exact.Length).Append(" exact writes queued");
                GameLog.Info(NetTransport.Category, text.ToString());
            }
            transport.Stats.ReplicatedGrids = grids.Count; transport.Stats.ReplicatedChunks = chunks.Count;
            transport.Stats.SnapshotAgeMs = SnapshotAgeMs;
        }

        void QueueHulk(EntityManager em, Entity root, HulkInfo info, bool blast)
        {
            using var clocks = em.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
            info.Tick = clocks.IsEmptyIgnoreFilter ? localTick : clocks.GetSingleton<SimClock>().Tick;   // this machine's tick, as HulkInfo.Tick means
            Streams(em).QueueHulk(new HulkTransition { Grid = root, Info = info, Blast = (byte)(blast ? 1 : 0) });
        }

        DamageStreams Streams(EntityManager em)
        {
            if (streamsQuery == default) streamsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<DamageStreams>());
            return streamsQuery.GetSingleton<DamageStreams>();
        }
        // Writes decoded this frame or queued and not yet applied by a tick: reads of cells, and despawns of the grids they target, wait.
        bool WritesPending(EntityManager em) => exact.Length > 0 || Streams(em).ExactPending;

        bool Ready(ulong id, out GuestGrid g) => grids.TryGetValue(id, out g) && g.Spawned;
        bool Waiting(ulong id) => grids.TryGetValue(id, out var g) && !g.Spawned;

        bool ApplyOne(EntityManager em, VoxelStructureRegistry registry, byte localSlot, NetMessage kind, NetReader r)
        {
            switch (kind)
            {
                case NetMessage.JoinBegin:
                    ReplicaJobs.WarmUp();
                    JoinComplete = false; GameLog.Info(NetTransport.Category, "join snapshot: " + (r.Remaining >= 6 ? "tick " + r.U32() + ", " + r.U16() + " grids" : "")); return true;
                case NetMessage.JoinEnd:
                    JoinComplete = true; GameLog.Info(NetTransport.Category, "join snapshot complete"); return true;
                case NetMessage.GridSpawn: Spawn(em, registry, localSlot, r.Inflated()); return true;
                case NetMessage.GridDespawn:
                {
                    ulong id = r.U64();
                    if (Waiting(id) || grids.ContainsKey(id) && WritesPending(em)) return false;
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
                    ulong id = r.U64(); var gridKind = (GridKind)r.U8(); bool hulk = GridCodec.ReadHulk(r, out var info);
                    if (Waiting(id)) return false;
                    if (Ready(id, out var g) && em.Exists(g.Entity))
                    {
                        // A hulk transition is applied by HulkTransitionSystem as on the host: components, kind, breach blast and sound.
                        if (hulk) { if (!em.HasComponent<Hulk>(g.Entity)) QueueHulk(em, g.Entity, info, info.Cause == HulkCause.CoreLost); }
                        else { var grid = em.GetComponentData<VoxelGrid>(g.Entity); grid.Kind = gridKind; em.SetComponentData(g.Entity, grid); }
                    }
                    return true;
                }
                case NetMessage.AbilityState:
                {
                    // NET-016: the host's slot decisions, applied in the next tick by AbilitySlotApplySystem (ADR-0117).
                    ulong id = AbilityCodec.ReadId(r);
                    if (Waiting(id)) return false;
                    if (Ready(id, out var g) && em.Exists(g.Entity) && em.HasBuffer<Game.Simulation.Abilities.AbilitySlot>(g.Entity)) AbilityCodec.Read(em, g.Entity, r);
                    return true;
                }
                case NetMessage.GridSalvage:
                {
                    // NET-017: the host's salvage decision as state; SalvageMarkSystem, SalvagePullSystem apply it as on the host.
                    ulong id = r.U64(), collectorId = r.U64();
                    if (Waiting(id) || Waiting(collectorId)) return false;
                    if (Ready(id, out var g) && em.Exists(g.Entity) && !em.HasComponent<Game.Galaxy.Salvage.SalvageCollected>(g.Entity)
                        && Ready(collectorId, out var c) && em.Exists(c.Entity))
                        em.AddComponentData(g.Entity, new Game.Galaxy.Salvage.SalvageCollected { Collector = c.Entity });
                    return true;
                }
                case NetMessage.PilotStatus:
                {
                    // NET-017: the host's combat status for a pilot, rebased onto this clock; PlayerCombatStatusSyncSystem reads it.
                    ulong id = r.U64(); ushort hostiles = r.U16(); uint ago = r.U32();
                    if (Waiting(id)) return false;
                    if (Ready(id, out var g) && em.Exists(g.Entity) && em.HasComponent<Game.Simulation.Ai.PilotCombatStatus>(g.Entity))
                    {
                        using var clocks = em.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
                        uint now = clocks.IsEmptyIgnoreFilter ? localTick : clocks.GetSingleton<SimClock>().Tick;
                        uint damaged = ago == uint.MaxValue || ago >= now ? 0 : now - ago;   // before this world began counts as never
                        em.SetComponentData(g.Entity, new Game.Simulation.Ai.PilotCombatStatus { HostilesEngaging = hostiles, LastDamagedTick = damaged });
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
                case NetMessage.HashReport:
                    if (WritesPending(em)) return false;   // compare after the cells before it are applied
                    CompareHashes(em, r); return true;
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
                // NET-011: the guest's own ship is a local dynamic body flown by its own input; everything else is kinematic.
                Kind = header.Kind, Motion = header.Motion == GridMotion.Static ? GridMotion.Static : mine && header.Kind == GridKind.Ship ? GridMotion.Dynamic : GridMotion.Kinematic,
                Flags = header.Flags & ~GridSpawnFlags.WriteBack, Profile = profile, PersistentId = header.NetId
            };
            Entity entity;
            try { entity = GridSpawnApi.RequestSpawn(em, registry, handle, p); }
            catch { registry.Release(handle); structure.Dispose(); throw; }
            if (header.HasAllegiance != 0) em.AddComponentData(entity, header.Allegiance);
            if (!string.IsNullOrEmpty(header.Pilot)) em.AddComponentData(entity, Game.Core.Net.PilotName.From(header.Pilot));
            em.AddComponentData(entity, new Replicated { NetId = header.NetId, OwnedLocally = (byte)(mine ? 1 : 0) });
            // ADR-0117: every player's ship is a pilot here too (the own one is also PlayerShip), so per-pilot systems serve it alike.
            if (header.OwnerSlot != 255 && header.Kind == GridKind.Ship) em.AddComponentData(entity, new Pilot { Slot = header.OwnerSlot });
            if (mine) Predictor.Reset();
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
                if (g.Header.Blueprint != 0 && em.HasComponent<GridSource>(g.Entity))
                {
                    // Names only: the replica's cells come from its own structure, never from the blueprint.
                    var source = em.GetComponentData<GridSource>(g.Entity); source.Blueprint = new ContentHash(g.Header.Blueprint); em.SetComponentData(g.Entity, source);
                }
                // Already a hulk on the host (a split piece, or one that died before this guest joined): the same transition, no blast.
                if (g.Header.IsHulk != 0 && !em.HasComponent<Hulk>(g.Entity)) QueueHulk(em, g.Entity, g.Header.Hulk, false);
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

        /// <summary>Translates one GridDelta into exact writes for <see cref="ExactCellApplySystem"/>: the cells first, then the instance
        /// health that follows from them. A brick's removals are DetachedBody when the host flagged it quiet (its cells left as a hulk or
        /// chunk body, which arrive as their own spawns), so no debris is sprayed for them.</summary>
        void ApplyDelta(EntityManager em, Entity root, NetReader r)
        {
            int paletteStart = r.U16(), paletteCount = r.U16();
            if (paletteCount > 0)
            {
                using var entries = new NativeArray<PaletteEntry>(paletteCount, Allocator.Temp);
                var e = entries;
                for (int i = 0; i < paletteCount; i++) e[i] = GridCodec.ReadPaletteEntry(r);
                ReplicaVoxelApi.SetPalette(em, root, paletteStart, entries);   // replicated state with no consequences; cells refer to it later
            }
            instanceWrites.Clear();
            int instanceCount = r.U16();
            for (int i = 0; i < instanceCount; i++)
            {
                int index = r.U16(); float hp = r.F32(); ushort alive = r.U16(); var flags = (BlockInstanceFlags)r.U8();
                instanceWrites.Add(new ExactCellWrite { Grid = root, Kind = ExactWriteKind.Instance, Instance = index, FunctionHp = hp, CellsAlive = alive, Flags = flags });
            }
            uint ops = r.U32();
            for (uint o = 0; o < ops; o++)
            {
                byte flags = r.U8(); bool quiet = (flags & 0x80) != 0; byte op = (byte)(flags & 0x7F); int3 coord;
                var reason = quiet ? CellRemovedReason.DetachedBody : CellRemovedReason.Destroyed;
                if (op == 0)
                {
                    coord = r.I3();
                    for (int i = 0; i < BrickMath.CellsPerBrick; i++) Add(root, BrickMath.CellOf(coord, i), default, reason);
                }
                else if (op == 1)
                {
                    coord = GridCodec.ReadBrickCells(r, cells);
                    for (int i = 0; i < BrickMath.CellsPerBrick; i++) Add(root, BrickMath.CellOf(coord, i), cells[i], reason);
                }
                else
                {
                    coord = r.I3(); int n = r.U16();
                    for (int i = 0; i < n; i++) { int local = r.U16(); Add(root, BrickMath.CellOf(coord, local), GridCodec.ReadCell(r), reason); }
                }
            }
            exact.AddRange(instanceWrites.AsArray());
        }

        void Add(Entity root, int3 cell, Cell value, CellRemovedReason reason)
            => exact.Add(new ExactCellWrite { Grid = root, Kind = ExactWriteKind.Cell, Cell = cell, Value = value, Reason = reason });

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
            // Presentation fades chunks against the local SimClock, so lifetimes are re-based onto it.
            using var clocks = em.CreateEntityQuery(ComponentType.ReadOnly<SimClock>());
            uint tick = clocks.IsEmptyIgnoreFilter ? localTick : clocks.GetSingleton<SimClock>().Tick;
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
            Predictor.Reset();
        }

        public void Dispose()
        {
            if (cells.IsCreated) cells.Dispose();
            if (exact.IsCreated) exact.Dispose();
            if (instanceWrites.IsCreated) instanceWrites.Dispose();
        }
    }
}
