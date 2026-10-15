using System;
using System.Collections.Generic;
using Game.Core.Net;
using Game.Net.Protocol;
using Game.Net.Replication;
using Game.Net.Transport;
using Game.Simulation.Commands;
using Game.Voxel.Grid;
using Unity.Mathematics;

namespace Game.Net
{
    /// <summary>The co-op runtime one machine owns while a session is live: transport, the host or guest replicator and the remote
    /// pilots' newest commands. Game.App creates it and binds it to the Game.Net systems (SPEC-90 §5). Main thread.</summary>
    public sealed class CoopNet : IDisposable
    {
        public NetTransport Transport { get; } = new();
        public HostReplicator Host { get; private set; }
        public GuestReplicator Guest { get; private set; }
        public VoxelStructureRegistry Registry { get; }
        public SimRoleKind Role { get; private set; }
        public bool HashRequested { get; set; }
        readonly Dictionary<byte, PilotCommandQueue> remoteCommands = new();
        readonly NetWriter command = new(512);

        public CoopNet(VoxelStructureRegistry registry)
        {
            Registry = registry;
            Transport.Register(NetMessage.Command, OnCommand);
            Transport.PeerLeft += (peer, _) => { remoteCommands.Remove(peer.Slot); Host?.ForgetPeer(peer); };
        }

        public bool StartHost(ushort port)
        {
            if (!Transport.Listen(port)) return false;
            Role = SimRoleKind.Host; Host = new HostReplicator(Transport);
            return true;
        }

        public bool StartGuest(string address, ushort port)
        {
            if (!Transport.Connect(address, port)) return false;
            Role = SimRoleKind.Guest; Guest = new GuestReplicator(Transport);
            return true;
        }

        /// <summary>Host, once per fixed tick: the command for this tick from the guest's queue (NET-011).</summary>
        public bool TryPopCommand(byte slot, out uint sequence, out PlayerCommand value)
        {
            sequence = 0; value = default;
            if (!remoteCommands.TryGetValue(slot, out var queue) || !queue.HasCommand) return false;
            queue.Pop();
            sequence = queue.Applied; value = queue.Current;
            return true;
        }

        public string DescribeCommands()
        {
            var text = new System.Text.StringBuilder();
            foreach (var pair in remoteCommands)
            {
                var q = pair.Value; var c = q.Current;
                text.Append("\n  pilot ").Append(pair.Key).Append(" #").Append(q.Applied).Append(" queue ").Append(q.Depth).Append(" starved ").Append(q.Starved)
                    .Append(" skipped ").Append(q.Skipped).Append(" target ").Append(q.Target).Append(": thrust ").Append(c.Thrust).Append(" turn ").Append(c.Turn)
                    .Append(" fire ").Append(c.FireGroups).Append(" aim ").Append(c.AimValid).Append(' ').Append(c.AimWorld);
            }
            return text.ToString();
        }

        // Command packet: newest sequence, count, then oldest to newest a flag byte (1 = same as the previous entry) and the command.
        const int CommandsPerPacket = 8;   // redundancy: a packet repeats the last few ticks, so a lost one costs nothing

        void OnCommand(NetPeer peer, NetReader r)
        {
            uint newest = r.U32(); int count = r.U8();
            if (!remoteCommands.TryGetValue(peer.Slot, out var queue)) remoteCommands[peer.Slot] = queue = new PilotCommandQueue();
            var c = default(PlayerCommand);
            for (int i = 0; i < count; i++)
            {
                if (r.U8() == 0) c = ReadCommand(r);
                queue.Push(newest - (uint)(count - 1 - i), c);
            }
            peer.LastCommandSequence = newest;
        }

        PlayerCommand ReadCommand(NetReader r)
        {
            var c = new PlayerCommand
            {
                Thrust = r.F2(), Turn = r.F32(), AimWorld = r.F3(), AimValid = r.U8(), Flags = (ShipCommandFlags)r.U8(), FireGroups = r.U32(),
                Edges = new PlayerEdgeCounters
                {
                    Interact = r.U8(), Ability1 = r.U8(), Ability2 = r.U8(), Ability3 = r.U8(), Ability4 = r.U8(), TargetNext = r.U8(),
                    TargetUnderCursor = r.U8(), TargetClear = r.U8(), AutofireToggle = r.U8(), Cruise = r.U8()
                }
            };
            // The guest sends aim in universe space; this end converts with its own origin.
            c.AimWorld -= (float3)HostOriginForCommands;
            return c;
        }

        static bool Same(in PlayerCommand a, in PlayerCommand b) =>
            a.Thrust.Equals(b.Thrust) && a.Turn.Equals(b.Turn) && a.AimWorld.Equals(b.AimWorld) && a.AimValid == b.AimValid && a.Flags == b.Flags
            && a.FireGroups == b.FireGroups && SameEdges(a.Edges, b.Edges);

        static bool SameEdges(in PlayerEdgeCounters a, in PlayerEdgeCounters b) =>
            a.Interact == b.Interact && a.Ability1 == b.Ability1 && a.Ability2 == b.Ability2 && a.Ability3 == b.Ability3 && a.Ability4 == b.Ability4
            && a.TargetNext == b.TargetNext && a.TargetUnderCursor == b.TargetUnderCursor && a.TargetClear == b.TargetClear
            && a.AutofireToggle == b.AutofireToggle && a.Cruise == b.Cruise;

        /// <summary>Host: current frame origin, set by the capture system, used to bring guest aim points into the local frame.</summary>
        public double3 HostOriginForCommands { get; set; }

        // Guest: one command per fixed tick, in universe space.
        readonly PlayerCommand[] tickCommands = new PlayerCommand[CommandsPerPacket];
        uint tickSequence, sentSequence;
        public uint TickSequence => tickSequence;

        /// <summary>Guest, once per fixed tick: labels this tick's command with the next sequence and keeps it for sending. Returns the
        /// sequence, which prediction records the tick's resulting state under.</summary>
        public uint RecordTickCommand(in PlayerCommand c, double3 origin)
        {
            var universe = c; universe.AimWorld += (float3)origin;
            tickCommands[++tickSequence % CommandsPerPacket] = universe;
            return tickSequence;
        }

        /// <summary>Guest: sends the last few ticks' commands when any tick ran since the last send (unreliable; edge counters are
        /// cumulative, so a lost packet loses nothing).</summary>
        public void SendCommands()
        {
            var host = Transport.HostPeer;
            if (host == null || host.Phase < NetPeerPhase.Welcomed || tickSequence == sentSequence) return;
            int count = (int)math.min(tickSequence, CommandsPerPacket);
            command.Clear(); command.U32(tickSequence); command.U8((byte)count);
            for (int i = 0; i < count; i++)
            {
                uint seq = tickSequence - (uint)(count - 1 - i);
                var c = tickCommands[seq % CommandsPerPacket];
                if (i > 0 && Same(c, tickCommands[(seq - 1) % CommandsPerPacket])) { command.U8(1); continue; }
                command.U8(0);
                command.F2(c.Thrust); command.F32(c.Turn); command.F3(c.AimWorld); command.U8(c.AimValid); command.U8((byte)c.Flags); command.U32(c.FireGroups);
                var e = c.Edges;
                command.U8(e.Interact); command.U8(e.Ability1); command.U8(e.Ability2); command.U8(e.Ability3); command.U8(e.Ability4);
                command.U8(e.TargetNext); command.U8(e.TargetUnderCursor); command.U8(e.TargetClear); command.U8(e.AutofireToggle); command.U8(e.Cruise);
            }
            Transport.SendUnreliable(host, NetMessage.Command, command);
            sentSequence = tickSequence;
        }

        public void Dispose()
        {
            Transport.Dispose();
            Host?.Dispose(); Guest?.Reset(Registry); Guest?.Dispose();
            Host = null; Guest = null; Role = SimRoleKind.Solo;
        }
    }
}
