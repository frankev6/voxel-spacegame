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
        readonly Dictionary<byte, (uint Sequence, PlayerCommand Command)> remoteCommands = new();
        readonly NetWriter command = new(64);
        uint commandSequence;

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

        public bool TryGetCommand(byte slot, out uint sequence, out PlayerCommand value)
        {
            if (remoteCommands.TryGetValue(slot, out var entry)) { sequence = entry.Sequence; value = entry.Command; return true; }
            sequence = 0; value = default; return false;
        }

        void OnCommand(NetPeer peer, NetReader r)
        {
            uint sequence = r.U32();
            if (remoteCommands.TryGetValue(peer.Slot, out var old) && unchecked((int)(sequence - old.Sequence)) <= 0) return;
            var c = new PlayerCommand
            {
                Thrust = r.F2(), Turn = r.F32(), AimWorld = r.F3(), AimValid = r.U8(), Flags = (ShipCommandFlags)r.U8(), FireGroups = r.U32(),
                Edges = new PlayerEdgeCounters
                {
                    Interact = r.U8(), Ability1 = r.U8(), Ability2 = r.U8(), Ability3 = r.U8(), Ability4 = r.U8(), TargetNext = r.U8(),
                    TargetUnderCursor = r.U8(), TargetClear = r.U8(), AutofireToggle = r.U8(), Cruise = r.U8()
                }
            };
            // The guest sends aim in universe space; this end converts with its own origin when applying.
            c.AimWorld -= (float3)HostOriginForCommands;
            peer.LastCommandSequence = sequence;
            remoteCommands[peer.Slot] = (sequence, c);
        }

        /// <summary>Host: current frame origin, set by the capture system, used to bring guest aim points into the local frame.</summary>
        public double3 HostOriginForCommands { get; set; }

        /// <summary>Guest: sends the newest local command (unreliable; edge counters are cumulative, so a lost packet loses nothing).</summary>
        public void SendCommand(in PlayerCommand c, double3 origin)
        {
            var host = Transport.HostPeer;
            if (host == null || host.Phase < NetPeerPhase.Welcomed) return;
            command.Clear(); command.U32(++commandSequence);
            command.F2(c.Thrust); command.F32(c.Turn); command.F3(c.AimWorld + (float3)origin); command.U8(c.AimValid); command.U8((byte)c.Flags); command.U32(c.FireGroups);
            var e = c.Edges;
            command.U8(e.Interact); command.U8(e.Ability1); command.U8(e.Ability2); command.U8(e.Ability3); command.U8(e.Ability4);
            command.U8(e.TargetNext); command.U8(e.TargetUnderCursor); command.U8(e.TargetClear); command.U8(e.AutofireToggle); command.U8(e.Cruise);
            Transport.SendUnreliable(host, NetMessage.Command, command);
        }

        public void Dispose()
        {
            Transport.Dispose();
            Host?.Dispose(); Guest?.Reset(Registry); Guest?.Dispose();
            Host = null; Guest = null; Role = SimRoleKind.Solo;
        }
    }
}
