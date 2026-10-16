using System;
using System.Collections.Generic;
using Game.Core.Components;
using Game.Net.Protocol;
using Game.Net.Replication.Grids;
using Game.Net.Transport;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Net.Replication
{
    /// <summary>Host side of ADR-0116: after each tick's structural sync it diffs every changed grid against what guests already have
    /// and streams spawns, despawns, brick deltas and chunk bodies on the reliable channel; every second tick it sends transforms.
    /// One shared stream: a joining guest gets the current state, then the same deltas as everyone else. Main thread.</summary>
    public sealed class HostReplicator : IDisposable
    {
        sealed class HostGrid
        {
            public Entity Entity;
            public uint Revision;
            public int Palette;
            public GridKind Kind;
            public readonly HashSet<int3> Live = new();
            public readonly Dictionary<int3, NativeArray<Cell>> Shadow = new();   // persistent; freed by DisposeShadows
            public float[] InstanceHp = Array.Empty<float>();
            public ushort[] InstanceCells = Array.Empty<ushort>();
            public byte[] InstanceFlags = Array.Empty<byte>();
            public bool Moving;
            public ulong AbilitySignature;   // decided slot state last broadcast (NET-016)
            public bool SalvageSent;         // SalvageCollected broadcast (NET-017)
            public void DisposeShadows() { foreach (var cells in Shadow.Values) cells.Dispose(); Shadow.Clear(); }
        }
        struct ChunkSlot { public ulong NetId; public Entity Entity; }

        readonly NetTransport transport;
        readonly Dictionary<ulong, HostGrid> grids = new();
        readonly List<ulong> order = new();
        readonly HashSet<ulong> seen = new();
        readonly Dictionary<(Entity Grid, int3 Brick), bool> quietBricks = new();
        readonly List<ulong> removed = new();
        readonly ChunkSlot[] chunks = new ChunkSlot[256];
        readonly NetWriter body = new(64 * 1024), message = new(64 * 1024), packet = new(NetProtocol.UnreliablePacketBytes);
        readonly Dictionary<NetPeer, Dictionary<ulong, float>> priorities = new();
        readonly List<(ulong Id, float Priority)> ranked = new();
        NativeArray<Cell> scratch;
        NativeList<Cell> sparse;   // expanded sparse bricks for the diff job
        readonly List<(Entity Entity, int3 Coord)> candidates = new();
        EntityQuery gridQuery, chunkQuery;
        bool queriesCreated, tracking;
        public int TrackedGrids => grids.Count;
        public int LiveChunks { get { int n = 0; foreach (var c in chunks) if (c.NetId != 0) n++; return n; } }

        public HostReplicator(NetTransport transport)
        {
            this.transport = transport;
            scratch = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Persistent);
            sparse = new NativeList<Cell>(0, Allocator.Persistent);
            Game.Voxel.Replica.ReplicaJobs.WarmUp();
        }

        void EnsureQueries(EntityManager em)
        {
            if (queriesCreated) return;
            gridQuery = em.CreateEntityQuery(ComponentType.ReadOnly<VoxelGrid>(), ComponentType.ReadOnly<PersistentId>(),
                ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<BrickSlot>());
            chunkQuery = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<ChunkBody>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
            queriesCreated = true;
        }

        bool AnyPeer(NetPeerPhase atLeast)
        {
            foreach (var peer in transport.Peers) if (peer.Phase >= atLeast && peer.Phase != NetPeerPhase.Closing) return true;
            return false;
        }

        void Broadcast(NetMessage kind, NetWriter payload)
        {
            foreach (var peer in transport.Peers) if (peer.Phase == NetPeerPhase.InWorld) transport.SendReliable(peer, kind, payload);
        }

        /// <summary>HostCaptureSystem: once per fixed tick, after GameplayEndCommandBufferSystem.</summary>
        public void Capture(EntityManager em, uint tick)
        {
            if (!AnyPeer(NetPeerPhase.Ready)) { if (tracking) Reset(); return; }
            tracking = true;
            EnsureQueries(em);
            CompleteReads(em);
            seen.Clear();

            using (var roots = gridQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var root in roots)
                {
                    if (em.HasComponent<GridSpawning>(root) && em.IsComponentEnabled<GridSpawning>(root)) continue;
                    ulong id = em.GetComponentData<PersistentId>(root).Value;
                    if (id == 0 || NetIds.IsChunk(id)) continue;
                    if (em.HasComponent<GridSource>(root) && (em.GetComponentData<GridSource>(root).Flags & GridSpawnFlags.EditorPreview) != 0) continue;
                    if (!seen.Add(id)) continue;
                    if (!grids.TryGetValue(id, out var g) || g.Entity != root)
                    {
                        if (g != null) SendDespawn(id);
                        g = Track(em, root);
                        grids[id] = g; order.Add(id);
                        body.Clear(); GridCodec.WriteGrid(em, root, Header(em, root, id), body, scratch);
                        message.Clear(); message.Deflated(body); Broadcast(NetMessage.GridSpawn, message);
                        SendAbilities(em, root, id, g, tick);
                        continue;
                    }
                    var grid = em.GetComponentData<VoxelGrid>(root);
                    if (grid.Kind != g.Kind)
                    {
                        g.Kind = grid.Kind;
                        bool hulk = em.HasComponent<Hulk>(root) && em.HasComponent<HulkInfo>(root);
                        message.Clear(); message.U64(id); message.U8((byte)grid.Kind);
                        GridCodec.WriteHulk(message, hulk, hulk ? em.GetComponentData<HulkInfo>(root) : default);
                        Broadcast(NetMessage.GridState, message);
                    }
                    if (grid.Revision != g.Revision) Delta(em, root, id, g, grid);
                    SendAbilities(em, root, id, g, tick);
                    SendSalvage(em, root, id, g);
                }
            }
            removed.Clear();
            foreach (var id in order) if (!seen.Contains(id)) removed.Add(id);
            foreach (var id in removed) SendDespawn(id);
            CaptureChunks(em, tick);
            foreach (var peer in transport.Peers) if (peer.Phase == NetPeerPhase.Ready) SendJoin(em, peer, tick);
            quietBricks.Clear();   // gathered tick by tick since the last capture (TapRemovals)
        }

        /// <summary>NET-016: a ship's ability slots whenever a decision changed them, after its cells, in the same reliable stream.</summary>
        void SendAbilities(EntityManager em, Entity root, ulong id, HostGrid g, uint tick)
        {
            message.Clear();
            if (!AbilityCodec.Write(em, root, id, tick, message, out ulong signature) || signature == g.AbilitySignature) return;
            g.AbilitySignature = signature;
            Broadcast(NetMessage.AbilityState, message);
        }

        /// <summary>NET-017: the authority's salvage decision, so every machine pulls the wreck into the same ship.</summary>
        void SendSalvage(EntityManager em, Entity root, ulong id, HostGrid g)
        {
            if (g.SalvageSent || !WriteSalvage(em, root, id, message)) return;
            g.SalvageSent = true;
            Broadcast(NetMessage.GridSalvage, message);
        }

        static bool WriteSalvage(EntityManager em, Entity root, ulong id, NetWriter w)
        {
            w.Clear();
            if (!em.HasComponent<Game.Galaxy.Salvage.SalvageCollected>(root)) return false;
            var collector = em.GetComponentData<Game.Galaxy.Salvage.SalvageCollected>(root).Collector;
            w.U64(id); w.U64(em.Exists(collector) && em.HasComponent<PersistentId>(collector) ? em.GetComponentData<PersistentId>(collector).Value : 0);
            return true;
        }

        /// <summary>Completes only the jobs writing what capture, snapshots and hashes read. Completing every tracked job here also
        /// waited for in-flight mesh and collider builds, which never touch these.</summary>
        public static void CompleteReads(EntityManager em)
        {
            em.CompleteDependencyBeforeRO<VoxelGrid>(); em.CompleteDependencyBeforeRO<PersistentId>(); em.CompleteDependencyBeforeRO<LocalTransform>();
            em.CompleteDependencyBeforeRO<BrickSlot>(); em.CompleteDependencyBeforeRO<Brick>(); em.CompleteDependencyBeforeRO<BrickCells>();
            em.CompleteDependencyBeforeRO<BrickSparseCells>(); em.CompleteDependencyBeforeRO<PaletteEntry>(); em.CompleteDependencyBeforeRO<BlockInstance>();
            em.CompleteDependencyBeforeRO<GridSpawning>(); em.CompleteDependencyBeforeRO<GridSource>(); em.CompleteDependencyBeforeRO<PhysicsVelocity>();
            em.CompleteDependencyBeforeRO<PhysicsMass>(); em.CompleteDependencyBeforeRO<Allegiance>(); em.CompleteDependencyBeforeRO<ShipCommand>();
            em.CompleteDependencyBeforeRO<ChunkBody>(); em.CompleteDependencyBeforeRO<ChunkCell>(); em.CompleteDependencyBeforeRO<ShieldState>();
            em.CompleteDependencyBeforeRO<Game.Simulation.Weapons.WeaponControl>(); em.CompleteDependencyBeforeRO<Game.Simulation.Weapons.ShipTarget>();
            em.CompleteDependencyBeforeRO<RemotePilotInput>(); em.CompleteDependencyBeforeRO<Game.Simulation.Flight.FlightThruster>();
            em.CompleteDependencyBeforeRO<Game.Simulation.Flight.ThrusterState>();
            em.CompleteDependencyBeforeRO<Game.Simulation.Abilities.AbilitySlot>(); em.CompleteDependencyBeforeRO<ShieldControl>();
            em.CompleteDependencyBeforeRO<Game.Galaxy.Salvage.SalvageCollected>();
        }

        /// <summary>HostRemovalTapSystem, every fixed tick after the resolver: removal reasons only live for their own tick, and a frame
        /// can run several ticks before the next capture.</summary>
        public void TapRemovals(EntityManager em)
        {
            if (!tracking) return;
            Game.Voxel.Replica.ReplicaVoxelApi.CollectQuietBricks(em, quietBricks, accumulate: true);
        }

        void SendDespawn(ulong id)
        {
            if (grids.Remove(id, out var g)) g.DisposeShadows();
            order.Remove(id);
            message.Clear(); message.U64(id); Broadcast(NetMessage.GridDespawn, message);
        }

        HostGrid Track(EntityManager em, Entity root)
        {
            var grid = em.GetComponentData<VoxelGrid>(root);
            var g = new HostGrid { Entity = root, Revision = grid.Revision, Kind = grid.Kind, Palette = em.GetBuffer<PaletteEntry>(root, true).Length,
                Moving = em.HasComponent<PhysicsVelocity>(root) };
            foreach (var slot in em.GetBuffer<BrickSlot>(root, true))
                if (slot.Brick != Entity.Null && em.Exists(slot.Brick)) g.Live.Add(em.GetComponentData<Brick>(slot.Brick).Coord);
            var instances = em.GetBuffer<BlockInstance>(root, true);
            g.InstanceHp = new float[instances.Length]; g.InstanceCells = new ushort[instances.Length]; g.InstanceFlags = new byte[instances.Length];
            for (int i = 0; i < instances.Length; i++)
            { g.InstanceHp[i] = instances[i].FunctionHp; g.InstanceCells[i] = instances[i].CellsAlive; g.InstanceFlags[i] = (byte)instances[i].Flags; }
            return g;
        }

        GridSpawnHeader Header(EntityManager em, Entity root, ulong id)
        {
            var grid = em.GetComponentData<VoxelGrid>(root);
            var transform = em.GetComponentData<LocalTransform>(root);
            byte owner = 255;
            if (em.HasComponent<PlayerShip>(root)) owner = 0;
            else if (em.HasComponent<RemotePilot>(root)) owner = em.GetComponentData<RemotePilot>(root).Slot;
            var role = grid.Kind == GridKind.Ship ? GridRoleHint.Ship : grid.Kind == GridKind.Station && em.HasComponent<ShipCommand>(root) ? GridRoleHint.Station : GridRoleHint.None;
            bool moving = em.HasComponent<PhysicsVelocity>(root);
            string pilot = em.HasComponent<Game.Core.Net.PilotName>(root) ? em.GetComponentData<Game.Core.Net.PilotName>(root).Value.ToString()
                : owner == 0 ? transport.LocalName : "";
            var flags = em.HasComponent<GridSource>(root) ? em.GetComponentData<GridSource>(root).Flags & (GridSpawnFlags.NoRender | GridSpawnFlags.PlanarInertia) : 0;
            return new GridSpawnHeader
            {
                NetId = id, OwnerSlot = owner, Kind = grid.Kind, Motion = moving ? GridMotion.Kinematic : GridMotion.Static, Role = role,
                HasAllegiance = (byte)(em.HasComponent<Allegiance>(root) ? 1 : 0),
                Allegiance = em.HasComponent<Allegiance>(root) ? em.GetComponentData<Allegiance>(root) : Allegiance.None,
                Flags = flags, CellSize = grid.CellSize, Blueprint = em.HasComponent<GridSource>(root) ? em.GetComponentData<GridSource>(root).Blueprint.Value : 0,
                Position = transform.Position, Rotation = transform.Rotation,
                Linear = moving ? em.GetComponentData<PhysicsVelocity>(root).Linear : float3.zero,
                AngularWorld = GridCodec.AngularWorld(em, root, transform.Rotation), Pilot = pilot,
                IsHulk = (byte)(em.HasComponent<Hulk>(root) && em.HasComponent<HulkInfo>(root) ? 1 : 0),
                Hulk = em.HasComponent<HulkInfo>(root) ? em.GetComponentData<HulkInfo>(root) : default
            };
        }

        void Delta(EntityManager em, Entity root, ulong id, HostGrid g, VoxelGrid grid)
        {
            body.Clear(); body.U64(id);
            // Palette growth (Place restores).
            var palette = em.GetBuffer<PaletteEntry>(root, true);
            if (palette.Length > g.Palette)
            {
                body.U16((ushort)g.Palette); body.U16((ushort)(palette.Length - g.Palette));
                for (int i = g.Palette; i < palette.Length; i++) GridCodec.WritePaletteEntry(body, palette[i]);
                g.Palette = palette.Length;
            }
            else { body.U16(0); body.U16(0); }
            // Instance health: functional entities on guests follow the host's destroyed flags.
            var instances = em.GetBuffer<BlockInstance>(root, true);
            int countAt = body.Length; body.U16(0); ushort changed = 0;
            int n = math.min(instances.Length, g.InstanceHp.Length);
            for (int i = 0; i < n; i++)
            {
                var b = instances[i];
                if (b.FunctionHp == g.InstanceHp[i] && b.CellsAlive == g.InstanceCells[i] && (byte)b.Flags == g.InstanceFlags[i]) continue;
                g.InstanceHp[i] = b.FunctionHp; g.InstanceCells[i] = b.CellsAlive; g.InstanceFlags[i] = (byte)b.Flags;
                body.U16((ushort)i); body.F32(b.FunctionHp); body.U16(b.CellsAlive); body.U8((byte)b.Flags); changed++;
            }
            body.Data[countAt] = (byte)changed; body.Data[countAt + 1] = (byte)(changed >> 8);
            // Bricks: changed since the last capture, new, or gone. Changed bricks are diffed against their shadows in one Burst job.
            int opsAt = body.ReserveU32(); uint ops = 0;
            var slots = em.GetBuffer<BrickSlot>(root, true);
            candidates.Clear(); int sparseCount = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                Entity entity = slots[s].Brick;
                if (entity == Entity.Null || !em.Exists(entity)) continue;
                var brick = em.GetComponentData<Brick>(entity);
                if (g.Live.Contains(brick.Coord) && brick.ChangedRevision <= g.Revision) continue;
                candidates.Add((entity, brick.Coord));
                if (Game.Voxel.Replica.ReplicaJobs.IsSparse(em, entity)) sparseCount++;
            }
            if (candidates.Count > 0)
            {
                int count = candidates.Count;
                var current = new NativeArray<Game.Voxel.Replica.BrickView>(count, Allocator.TempJob);
                var shadows = new NativeArray<Game.Voxel.Replica.BrickView>(count, Allocator.TempJob);
                var counts = new NativeArray<int>(count, Allocator.TempJob);
                var changedCells = new NativeArray<ushort>(count * BrickMath.CellsPerBrick, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sparse.Clear(); sparse.Resize(sparseCount * BrickMath.CellsPerBrick, NativeArrayOptions.UninitializedMemory);
                int next = 0;
                for (int k = 0; k < count; k++)
                {
                    var (entity, coord) = candidates[k];
                    current[k] = Game.Voxel.Replica.ReplicaJobs.View(em, entity, sparse, ref next);
                    shadows[k] = g.Live.Contains(coord) && g.Shadow.TryGetValue(coord, out var shadow) ? Game.Voxel.Replica.ReplicaJobs.ViewOf(shadow, coord) : default;
                }
                Game.Voxel.Replica.ReplicaJobs.Diff(current, shadows, counts, changedCells);
                for (int k = 0; k < count; k++)
                {
                    var coord = candidates[k].Coord; int diffs = counts[k];
                    if (diffs == 0) continue;
                    if (diffs > 0 && diffs <= BrickMath.CellsPerBrick / 4)
                    {
                        var shadow = g.Shadow[coord];
                        body.U8((byte)(2 | Quiet(root, coord))); body.I3(coord); body.U16((ushort)diffs);
                        for (int d = 0, at = k * BrickMath.CellsPerBrick; d < diffs; d++)
                        {
                            int i = changedCells[at + d]; var cell = Game.Voxel.Replica.ReplicaJobs.Get(current[k], i);
                            body.U16((ushort)i); GridCodec.WriteCell(body, cell); shadow[i] = cell;
                        }
                        ops++; continue;
                    }
                    Game.Voxel.Replica.ReplicaJobs.CopyTo(current[k], scratch);
                    body.U8((byte)(1 | Quiet(root, coord))); GridCodec.WriteBrickCells(body, coord, scratch); ops++;
                    if (!g.Shadow.TryGetValue(coord, out var copy)) { copy = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Persistent); g.Shadow[coord] = copy; }
                    scratch.CopyTo(copy);
                    g.Live.Add(coord);
                }
                current.Dispose(); shadows.Dispose(); counts.Dispose(); changedCells.Dispose();
            }
            if (g.Live.Count > 0)
            {
                List<int3> gone = null;
                foreach (var coord in g.Live)
                {
                    int index = BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims);
                    Entity entity = index >= 0 && index < slots.Length ? slots[index].Brick : Entity.Null;
                    if (entity == Entity.Null || !em.Exists(entity)) (gone ??= new List<int3>()).Add(coord);
                }
                if (gone != null)
                    foreach (var coord in gone)
                    {
                        body.U8((byte)(0 | Quiet(root, coord))); body.I3(coord); ops++; g.Live.Remove(coord);
                        if (g.Shadow.Remove(coord, out var dead)) dead.Dispose();
                    }
            }
            body.PatchU32(opsAt, ops);
            g.Revision = grid.Revision;
            if (ops == 0 && changed == 0 && palette.Length == g.Palette && body.Length <= 20) return;
            message.Clear();
            if (body.Length > 1024) { message.U8(1); message.Deflated(body); }
            else { message.U8(0); message.Bytes(body.Data, 0, body.Length); }
            Broadcast(NetMessage.GridDelta, message);
        }

        /// <summary>Op flag 0x80: this brick's cells left as detached bodies only; guests skip debris for them.</summary>
        byte Quiet(Entity root, int3 coord) => quietBricks.TryGetValue((root, coord), out bool quiet) && quiet ? (byte)0x80 : (byte)0;

        void CaptureChunks(EntityManager em, uint tick)
        {
            using var entities = chunkQuery.ToEntityArray(Allocator.Temp);
            Span<bool> live = stackalloc bool[256];
            foreach (var entity in entities)
            {
                var body = em.GetComponentData<ChunkBody>(entity);
                if (body.Live == 0 || em.HasComponent<Disabled>(entity) || body.Slot == 255) continue;
                live[body.Slot] = true;
                ulong id = NetIds.Chunk(body.Slot, body.SpawnTick);
                ref var slot = ref chunks[body.Slot];
                if (slot.NetId == id && slot.Entity == entity) continue;
                if (slot.NetId != 0) SendChunkDespawn(ref slot);
                slot = new ChunkSlot { NetId = id, Entity = entity };
                message.Clear(); WriteChunk(em, entity, id, body, tick, message); Broadcast(NetMessage.ChunkSpawn, message);
            }
            for (int s = 0; s < chunks.Length; s++) if (!live[s] && chunks[s].NetId != 0) SendChunkDespawn(ref chunks[s]);
        }

        void SendChunkDespawn(ref ChunkSlot slot)
        {
            message.Clear(); message.U64(slot.NetId); Broadcast(NetMessage.ChunkDespawn, message);
            slot = default;
        }

        static void WriteChunk(EntityManager em, Entity entity, ulong id, ChunkBody body, uint tick, NetWriter w)
        {
            var pose = em.GetComponentData<LocalTransform>(entity);
            w.U64(id); w.F32(body.CellSize); w.F3(body.LocalOrigin); w.F3(pose.Position); w.Quat(pose.Rotation); w.F32(pose.Scale);
            w.F3(em.GetComponentData<PhysicsVelocity>(entity).Linear);
            // Lifetimes relative to now: guests run their own tick counter.
            w.U32(body.EndTick > tick ? body.EndTick - tick : 0); w.U32(body.DissolveStartTick > tick ? body.DissolveStartTick - tick : 0);
            var cells = em.GetBuffer<ChunkCell>(entity, true);
            w.U16((ushort)cells.Length);
            foreach (var c in cells) { w.U8(c.X); w.U8(c.Y); w.U8(c.Z); w.U8(c.Orient); w.U16(c.Block); w.U8(c.Color); }
        }

        void SendJoin(EntityManager em, NetPeer peer, uint tick)
        {
            message.Clear(); message.U32(tick); message.U16((ushort)grids.Count);
            transport.SendReliable(peer, NetMessage.JoinBegin, message);
            foreach (var id in order)
            {
                var g = grids[id];
                if (!em.Exists(g.Entity)) continue;
                body.Clear(); GridCodec.WriteGrid(em, g.Entity, Header(em, g.Entity, id), body, scratch);
                message.Clear(); message.Deflated(body); transport.SendReliable(peer, NetMessage.GridSpawn, message);
                message.Clear();
                if (AbilityCodec.Write(em, g.Entity, id, tick, message, out _)) transport.SendReliable(peer, NetMessage.AbilityState, message);
                if (WriteSalvage(em, g.Entity, id, message)) transport.SendReliable(peer, NetMessage.GridSalvage, message);
            }
            foreach (var slot in chunks)
            {
                if (slot.NetId == 0 || !em.Exists(slot.Entity)) continue;
                message.Clear(); WriteChunk(em, slot.Entity, slot.NetId, em.GetComponentData<ChunkBody>(slot.Entity), tick, message);
                transport.SendReliable(peer, NetMessage.ChunkSpawn, message);
            }
            message.Clear(); message.U32(tick); transport.SendReliable(peer, NetMessage.JoinEnd, message);
            peer.Phase = NetPeerPhase.InWorld;
            Game.Core.GameLog.Info(NetTransport.Category, "join snapshot for slot " + peer.Slot + ": " + grids.Count + " grids, " + peer.PendingReliableBytes / 1024 + " KB queued");
        }

        /// <summary>HostSnapshotSystem: transforms by priority, at most <paramref name="packetsPerPeer"/> packets per guest.</summary>
        public void Snapshot(EntityManager em, uint tick, double3 origin, int packetsPerPeer)
        {
            if (!tracking) return;
            foreach (var peer in transport.Peers)
            {
                if (peer.Phase != NetPeerPhase.InWorld) continue;
                if (!priorities.TryGetValue(peer, out var table)) priorities[peer] = table = new Dictionary<ulong, float>();
                ranked.Clear();
                foreach (var id in order)
                {
                    var g = grids[id];
                    if (!g.Moving || !em.Exists(g.Entity)) continue;
                    table.TryGetValue(id, out float p);
                    float speed = math.length(em.GetComponentData<PhysicsVelocity>(g.Entity).Linear);
                    p += 1 + math.min(speed, 40) / 10f;
                    // A guest's own ship goes first in every snapshot: its prediction is corrected from it (NET-011).
                    if (em.HasComponent<RemotePilot>(g.Entity) && em.GetComponentData<RemotePilot>(g.Entity).Slot == peer.Slot) p += 1000;
                    table[id] = p; ranked.Add((id, p));
                }
                foreach (var slot in chunks)
                {
                    if (slot.NetId == 0 || !em.Exists(slot.Entity)) continue;
                    table.TryGetValue(slot.NetId, out float p); p += 1.5f; table[slot.NetId] = p; ranked.Add((slot.NetId, p));
                }
                ranked.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                int next = 0;
                for (int k = 0; k < packetsPerPeer && next < ranked.Count; k++)
                {
                    packet.Clear(); packet.U32(tick); packet.D3(origin); int countAt = packet.Length; packet.U8(0); byte count = 0;
                    while (next < ranked.Count && count < 255)
                    {
                        int before = packet.Length;
                        var id = ranked[next].Id;
                        Entity entity = NetIds.IsChunk(id) ? FindChunk(id) : grids[id].Entity;
                        WriteEntry(em, entity, id, packet);
                        if (packet.Length + 1 > NetProtocol.UnreliablePacketBytes) { Truncate(packet, before); break; }
                        table[id] = 0; count++; next++;
                    }
                    packet.Data[countAt] = count;
                    if (count > 0) transport.SendUnreliable(peer, NetMessage.Snapshot, packet);
                }
            }
        }

        static void Truncate(NetWriter w, int length)
        {
            // NetWriter has no public length setter; rewrite by clearing and copying the kept prefix.
            var copy = new byte[length]; Buffer.BlockCopy(w.Data, 0, copy, 0, length);
            w.Clear(); w.Bytes(copy, 0, length);
        }

        Entity FindChunk(ulong id)
        {
            foreach (var slot in chunks) if (slot.NetId == id) return slot.Entity;
            return Entity.Null;
        }

        // Entry: id, flags (1 command, 2 shield, 4 scale, 8 weapons, 16 acknowledged pilot command, 32 thruster plumes, 64 aim anchor),
        // pose, velocities, optional blocks.
        void WriteEntry(EntityManager em, Entity e, ulong id, NetWriter w)
        {
            var pose = em.GetComponentData<LocalTransform>(e);
            bool command = em.HasComponent<ShipCommand>(e), shield = em.HasComponent<ShieldState>(e), chunk = NetIds.IsChunk(id);
            bool weapons = em.HasComponent<Game.Simulation.Weapons.WeaponControl>(e) || em.HasComponent<Game.Simulation.Weapons.ShipTarget>(e);
            bool ack = em.HasComponent<RemotePilotInput>(e);
            bool plumes = em.HasBuffer<Game.Simulation.Flight.FlightThruster>(e) && !em.HasComponent<Game.Voxel.Damage.Hulk>(e);
            // Player ships aim at a point the player picked by eye: guests rebuild it on the grid under it (AimAnchor.cs).
            ulong aimAnchor = 0; float3 aimLocal = default;
            if (command && (em.HasComponent<PlayerShip>(e) || em.HasComponent<RemotePilot>(e)))
            {
                var c = em.GetComponentData<ShipCommand>(e);
                Entity locked = em.HasComponent<Game.Simulation.Weapons.ShipTarget>(e) ? em.GetComponentData<Game.Simulation.Weapons.ShipTarget>(e).Value : Entity.Null;
                if (c.AimValid != 0 && math.all(math.isfinite(c.AimWorld))) aimAnchor = AimAnchor.Find(em, gridQuery, c.AimWorld, locked, e, out aimLocal);
            }
            byte flags = (byte)((command ? 1 : 0) | (shield ? 2 : 0) | (chunk ? 4 : 0) | (weapons ? 8 : 0) | (ack ? 16 : 0) | (plumes ? 32 : 0) | (aimAnchor != 0 ? 64 : 0));
            w.U64(id); w.U8(flags); w.F3(pose.Position); w.Quat(pose.Rotation);
            w.F3(em.HasComponent<PhysicsVelocity>(e) ? em.GetComponentData<PhysicsVelocity>(e).Linear : float3.zero);
            w.F3(GridCodec.AngularWorld(em, e, pose.Rotation));
            if (command)
            {
                var c = em.GetComponentData<ShipCommand>(e);
                w.F2(c.Thrust); w.F32(c.Turn); w.F3(c.AimWorld); w.U8(c.AimValid); w.U32(c.FireGroups); w.U8((byte)c.Flags);
            }
            if (shield) { var s = em.GetComponentData<ShieldState>(e); w.F32(s.Hp); w.F32(s.MaxHp); w.U8(s.Active); }
            if (chunk) w.F32(pose.Scale);
            if (weapons)
            {
                // AI weapons lead a target rather than an aim point: guests resolve the target by NetId and aim the same way.
                var control = em.HasComponent<Game.Simulation.Weapons.WeaponControl>(e) ? em.GetComponentData<Game.Simulation.Weapons.WeaponControl>(e) : default;
                Entity target = em.HasComponent<Game.Simulation.Weapons.ShipTarget>(e) ? em.GetComponentData<Game.Simulation.Weapons.ShipTarget>(e).Value : Entity.Null;
                ulong targetId = target != Entity.Null && em.Exists(target) && em.HasComponent<PersistentId>(target) ? em.GetComponentData<PersistentId>(target).Value : 0;
                w.U64(targetId); w.U8(control.Autofire); w.U8((byte)control.AimMode);
            }
            if (ack) w.U32(em.GetComponentData<RemotePilotInput>(e).Sequence);
            if (aimAnchor != 0) { w.U64(aimAnchor); w.F3(aimLocal); }
            if (plumes)
            {
                // Lit thrusters only, by block instance: guests have no flight solve for kinematic replicas, so plumes and engine
                // sound come from here.
                var thrusters = em.GetBuffer<Game.Simulation.Flight.FlightThruster>(e, true);
                int countAt = w.Length; w.U8(0); byte n = 0;
                for (int k = 0; k < thrusters.Length && n < ReplicaTrack.MaxPlumes; k++)
                {
                    var t = thrusters[k];
                    if (!em.Exists(t.Thruster) || !em.HasComponent<Game.Simulation.Flight.ThrusterState>(t.Thruster)) continue;
                    byte level = ReplicaTrack.EncodePlume(em.GetComponentData<Game.Simulation.Flight.ThrusterState>(t.Thruster).Intensity);
                    if (level == 0) continue;
                    w.U16((ushort)t.InstanceIndex); w.U8(level); n++;
                }
                w.Data[countAt] = n;
            }
        }

        readonly Dictionary<ulong, uint> hashedRevision = new();

        /// <summary>CellHashCheckSystem (host half): per-grid cell hashes after this tick's deltas, in the same reliable stream, so each
        /// guest compares at exactly the same state. <paramref name="all"/> = false sends only grids changed since the last report.</summary>
        public int SendHashReport(EntityManager em, uint tick, bool all)
        {
            if (!tracking || !AnyPeer(NetPeerPhase.InWorld)) return 0;
            CompleteReads(em);
            message.Clear(); message.U32(tick); int countAt = message.Length; message.U16(0); ushort n = 0;
            foreach (var id in order)
            {
                var g = grids[id];
                if (!em.Exists(g.Entity) || n == ushort.MaxValue) continue;
                if (!all && hashedRevision.TryGetValue(id, out var revision) && revision == g.Revision) continue;
                hashedRevision[id] = g.Revision;
                message.U64(id); message.U64(Game.Voxel.Replica.ReplicaVoxelApi.HashCells(em, g.Entity)); n++;
            }
            message.Data[countAt] = (byte)n; message.Data[countAt + 1] = (byte)(n >> 8);
            if (n > 0) Broadcast(NetMessage.HashReport, message);
            return n;
        }

        public void ForgetPeer(NetPeer peer) => priorities.Remove(peer);

        /// <summary>The live grid root for a network id (guests name their target lock by it).</summary>
        public bool TryGetGrid(ulong id, out Entity entity)
        {
            entity = Entity.Null;
            if (!grids.TryGetValue(id, out var g)) return false;
            entity = g.Entity; return true;
        }

        void Reset()
        {
            foreach (var g in grids.Values) g.DisposeShadows();
            grids.Clear(); order.Clear(); priorities.Clear(); hashedRevision.Clear();
            for (int i = 0; i < chunks.Length; i++) chunks[i] = default;
            tracking = false;
        }

        public void Dispose()
        {
            Reset();
            if (scratch.IsCreated) scratch.Dispose();
            if (sparse.IsCreated) sparse.Dispose();
        }
    }
}
