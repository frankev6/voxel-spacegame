using System;
using System.Collections.Generic;
using Game.Core;
using Game.Net.Protocol;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using UnityEngine;

namespace Game.Net.Transport
{
    public delegate void NetMessageHandler(NetPeer peer, NetReader reader);

    /// <summary>SPEC-90 §5 NetTransportService: owns the Unity Transport driver, both pipelines, the handshake, pings and the
    /// reliable stream framing. Main thread only. Replication code registers handlers per <see cref="NetMessage"/>.</summary>
    public sealed class NetTransport : IDisposable
    {
        public const string Category = "Net";
        NetworkDriver driver;
        NetworkPipeline reliable, unreliable;
        readonly List<NetPeer> peers = new();
        readonly NetMessageHandler[] handlers = new NetMessageHandler[256];
        readonly NetWriter scratch = new(64);
        readonly NetReader reader = new();
        byte[] receiveBuffer = new byte[64 * 1024];
        double nextPing;
        bool disposed;

        public bool IsHost { get; private set; }
        public bool IsActive => driver.IsCreated;
        public string LocalName { get; set; } = "Pilot";
        public string LastError { get; private set; } = "";
        public NetStats Stats { get; } = new();
        public IReadOnlyList<NetPeer> Peers => peers;
        public NetPeer HostPeer => !IsHost && peers.Count > 0 ? peers[0] : null;
        public byte LocalSlot { get; private set; }

        public event Action<NetPeer> PeerWelcomed;   // host: a guest passed the version check
        public event Action<NetPeer> PeerReady;      // host: a guest's World scene is ready for the join snapshot
        public event Action<NetPeer, string> PeerLeft;
        public event Action<byte> Welcomed;          // guest: the host accepted us into this slot
        public event Action<string> Failed;          // guest: refused, timed out or lost the host

        public void Register(NetMessage kind, NetMessageHandler handler) => handlers[(byte)kind] = handler;
        public void Unregister(NetMessage kind) => handlers[(byte)kind] = null;

        NetworkSettings Settings()
        {
            var settings = new NetworkSettings(Allocator.Temp);
            settings.WithNetworkConfigParameters(connectTimeoutMS: 1000, maxConnectAttempts: 10, disconnectTimeoutMS: 10000,
                heartbeatTimeoutMS: 500, sendQueueCapacity: 4096, receiveQueueCapacity: 4096);
            settings.WithFragmentationStageParameters(payloadCapacity: NetProtocol.ReliableChunkBytes + 256);
            settings.WithReliableStageParameters(windowSize: 256);
            settings.WithNetworkSimulatorParameters();
            return settings;
        }
        void CreateDriver()
        {
            driver = NetworkDriver.Create(Settings());
            reliable = driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            unreliable = driver.CreatePipeline(typeof(UnreliableSequencedPipelineStage));
        }

        public bool Listen(ushort port)
        {
            Shutdown("restart");
            CreateDriver(); IsHost = true; LocalSlot = 0;
            if (driver.Bind(NetworkEndpoint.AnyIpv4.WithPort(port)) != 0 || driver.Listen() != 0)
            { LastError = "Could not listen on port " + port; Shutdown(LastError); return false; }
            GameLog.Info(Category, "hosting on port " + port);
            return true;
        }

        public bool Connect(string address, ushort port)
        {
            Shutdown("restart");
            if (!NetworkEndpoint.TryParse(address, port, out var endpoint)) { LastError = "Not an IP address: " + address; return false; }
            CreateDriver(); IsHost = false;
            var peer = new NetPeer { Connection = driver.Connect(endpoint), Slot = 0, Phase = NetPeerPhase.Connecting, LastHeardSeconds = Now };
            peers.Add(peer);
            GameLog.Info(Category, "connecting to " + address + ":" + port);
            return true;
        }

        static double Now => Time.realtimeSinceStartupAsDouble;

        /// <summary>Runs the driver, accepts guests and dispatches every received message. Call once per frame.</summary>
        public void Pump()
        {
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            if (IsHost)
            {
                NetworkConnection incoming;
                while ((incoming = driver.Accept()) != default)
                {
                    byte slot = FreeSlot();
                    if (slot == 0) { driver.Disconnect(incoming); GameLog.Warn(Category, "refused a guest: the session is full"); continue; }
                    peers.Add(new NetPeer { Connection = incoming, Slot = slot, Phase = NetPeerPhase.Handshaking, LastHeardSeconds = Now });
                }
            }
            for (int i = peers.Count - 1; i >= 0 && i < peers.Count; i--)
            {
                var peer = peers[i];
                NetworkEvent.Type type;
                while (driver.IsCreated && (type = driver.PopEventForConnection(peer.Connection, out var stream, out var pipe)) != NetworkEvent.Type.Empty)
                {
                    if (type == NetworkEvent.Type.Connect)
                    {
                        peer.Phase = NetPeerPhase.Handshaking;
                        scratch.Clear(); scratch.U32(NetProtocol.Version); scratch.Str(LocalName);
                        SendReliable(peer, NetMessage.Hello, scratch);
                    }
                    else if (type == NetworkEvent.Type.Disconnect)
                    {
                        Drop(peer, "the connection closed"); break;
                    }
                    else if (type == NetworkEvent.Type.Data)
                    {
                        int length = stream.Length;
                        if (receiveBuffer.Length < length) Array.Resize(ref receiveBuffer, Math.Max(length, receiveBuffer.Length * 2));
                        stream.ReadBytes(new Span<byte>(receiveBuffer, 0, length));
                        peer.LastHeardSeconds = Now;
                        if (pipe == reliable)
                        {
                            Stats.Received(NetChannel.Reliable, length);
                            peer.Received(new ReadOnlySpan<byte>(receiveBuffer, 0, length));
                            DrainReliable(peer);
                        }
                        else
                        {
                            Stats.Received(NetChannel.Unreliable, length);
                            Dispatch(peer, receiveBuffer, 0, length);
                        }
                    }
                    if (!peers.Contains(peer)) break;
                }
            }
            double now = Now;
            if (now >= nextPing)
            {
                nextPing = now + NetProtocol.PingIntervalSeconds;
                foreach (var peer in peers)
                    if (peer.Phase >= NetPeerPhase.Welcomed) { scratch.Clear(); scratch.F64(now); SendUnreliable(peer, NetMessage.Ping, scratch); }
            }
            Stats.Tick(now);
        }

        byte FreeSlot()
        {
            for (byte s = 1; s < NetProtocol.MaxPlayers; s++)
            {
                bool used = false;
                foreach (var p in peers) used |= p.Slot == s;
                if (!used) return s;
            }
            return 0;
        }

        void DrainReliable(NetPeer peer)
        {
            int offset = 0;
            while (peer.InboxLength - offset >= 5)
            {
                byte kind = peer.Inbox[offset];
                int length = peer.Inbox[offset + 1] | (peer.Inbox[offset + 2] << 8) | (peer.Inbox[offset + 3] << 16) | (peer.Inbox[offset + 4] << 24);
                if (length < 0 || length > 64 * 1024 * 1024) { Drop(peer, "corrupt reliable stream"); return; }
                if (peer.InboxLength - offset - 5 < length) break;
                Handle(peer, kind, peer.Inbox, offset + 5, length);
                if (!peers.Contains(peer)) return;
                offset += 5 + length;
            }
            if (offset > 0) peer.Consume(offset);
        }

        void Dispatch(NetPeer peer, byte[] data, int offset, int count)
        {
            if (count < 1) return;
            Handle(peer, data[offset], data, offset + 1, count - 1);
        }

        void Handle(NetPeer peer, byte kind, byte[] data, int offset, int count)
        {
            reader.Reset(data, offset, count);
            try
            {
                switch ((NetMessage)kind)
                {
                    case NetMessage.Ping:
                        scratch.Clear(); scratch.F64(reader.F64()); SendUnreliable(peer, NetMessage.Pong, scratch); return;
                    case NetMessage.Pong:
                        peer.RttMs = (float)((Now - reader.F64()) * 1000); return;
                    case NetMessage.Goodbye:
                        Drop(peer, (IsHost ? "said goodbye: " : "the host ended the session: ") + (reader.Remaining > 2 ? reader.Str() : "left")); return;
                    case NetMessage.Hello when IsHost: OnHello(peer, reader); return;
                    case NetMessage.Ready when IsHost:
                        if (peer.Phase == NetPeerPhase.Welcomed) { peer.Phase = NetPeerPhase.Ready; PeerReady?.Invoke(peer); }
                        return;
                    case NetMessage.Welcome when !IsHost:
                        LocalSlot = reader.U8(); peer.Name = reader.Str(); peer.Phase = NetPeerPhase.Welcomed;
                        GameLog.Info(Category, "joined as slot " + LocalSlot);
                        Welcomed?.Invoke(LocalSlot); return;
                    case NetMessage.Reject when !IsHost:
                        string reason = reader.Str(); LastError = reason;
                        Shutdown(reason); Failed?.Invoke(reason); return;
                }
                handlers[kind]?.Invoke(peer, reader);
            }
            catch (Exception e)
            {
                GameLog.Exception(Category, e, "net message " + (NetMessage)kind);
                if (e is NetFormatException) Drop(peer, "bad message " + (NetMessage)kind);
            }
        }

        void OnHello(NetPeer peer, NetReader r)
        {
            uint version = r.U32(); peer.Name = r.Str();
            if (version != NetProtocol.Version)
            {
                scratch.Clear(); scratch.Str("Version mismatch: host " + NetProtocol.Version + ", you " + version);
                SendReliable(peer, NetMessage.Reject, scratch);
                peer.Phase = NetPeerPhase.Closing; return;
            }
            peer.Phase = NetPeerPhase.Welcomed;
            scratch.Clear(); scratch.U8(peer.Slot); scratch.Str(LocalName);
            SendReliable(peer, NetMessage.Welcome, scratch);
            GameLog.Info(Category, "guest '" + peer.Name + "' joined slot " + peer.Slot);
            PeerWelcomed?.Invoke(peer);
        }

        /// <summary>Queues one whole message on the reliable ordered stream.</summary>
        public void SendReliable(NetPeer peer, NetMessage kind, NetWriter payload)
        {
            if (peer == null || peer.Phase == NetPeerPhase.Closing && kind != NetMessage.Reject) return;
            int length = payload?.Length ?? 0;
            Span<byte> header = stackalloc byte[5];
            header[0] = (byte)kind; header[1] = (byte)length; header[2] = (byte)(length >> 8); header[3] = (byte)(length >> 16); header[4] = (byte)(length >> 24);
            var bytes = header.ToArray();
            peer.Append(bytes, 0, 5);
            if (length > 0) peer.Append(payload.Data, 0, length);
            Stats.SentMessage(NetChannel.Reliable);
        }

        /// <summary>Sends one unreliable packet now; payloads larger than <see cref="NetProtocol.UnreliablePacketBytes"/> are dropped.</summary>
        public bool SendUnreliable(NetPeer peer, NetMessage kind, NetWriter payload)
        {
            if (peer == null || !driver.IsCreated || peer.Phase < NetPeerPhase.Handshaking) return false;
            int length = payload?.Length ?? 0;
            if (length + 1 > NetProtocol.UnreliablePacketBytes) { GameLog.Warn(Category, "unreliable packet too large: " + kind); return false; }
            if (driver.BeginSend(unreliable, peer.Connection, out var writer) != 0) return false;
            writer.WriteByte((byte)kind);
            if (length > 0) writer.WriteBytes(new Span<byte>(payload.Data, 0, length));
            if (writer.HasFailedWrites) { driver.AbortSend(writer); return false; }
            int sent = driver.EndSend(writer);
            if (sent < 0) return false;
            Stats.Sent(NetChannel.Unreliable, length + 1); Stats.SentMessage(NetChannel.Unreliable);
            return true;
        }

        /// <summary>Moves queued reliable bytes into the driver until its window fills, then flushes. Call once per frame, last.</summary>
        public void Flush()
        {
            if (!driver.IsCreated) return;
            for (int i = peers.Count - 1; i >= 0; i--)
            {
                var peer = peers[i];
                if (peer.Phase < NetPeerPhase.Handshaking) continue;
                Stats.PeakReliableBacklog = Math.Max(Stats.PeakReliableBacklog, peer.PendingReliableBytes);
                while (peer.PendingReliableBytes > 0)
                {
                    int chunk = Math.Min(peer.PendingReliableBytes, NetProtocol.ReliableChunkBytes);
                    if (driver.BeginSend(reliable, peer.Connection, out var writer) != 0) break;
                    writer.WriteBytes(new Span<byte>(peer.Outbox, peer.OutboxStart, chunk));
                    if (writer.HasFailedWrites) { driver.AbortSend(writer); break; }
                    int sent = driver.EndSend(writer);
                    if (sent < 0) break;          // reliable window full: keep the bytes and retry next frame
                    peer.OutboxStart += chunk; Stats.Sent(NetChannel.Reliable, chunk);
                }
                if (peer.PendingReliableBytes == 0) { peer.OutboxStart = peer.OutboxEnd = 0; }
                if (peer.Phase == NetPeerPhase.Closing && peer.PendingReliableBytes == 0) Drop(peer, "refused");
            }
            driver.ScheduleFlushSend().Complete();
        }

        public void Disconnect(NetPeer peer, string reason) => Drop(peer, reason);

        void Drop(NetPeer peer, string reason)
        {
            if (!peers.Remove(peer)) return;
            if (driver.IsCreated && driver.GetConnectionState(peer.Connection) != NetworkConnection.State.Disconnected) driver.Disconnect(peer.Connection);
            GameLog.Info(Category, "peer " + peer.Slot + " left: " + reason);
            PeerLeft?.Invoke(peer, reason);
            if (!IsHost)
            {
                string message = peer.Phase == NetPeerPhase.Connecting ? "Could not reach the host" : "Lost the host: " + reason;
                LastError = message; Failed?.Invoke(message);
            }
        }

        public void SetSimulator(uint latencyMs, uint jitterMs, float lossPercent)
        {
            if (!driver.IsCreated) return;
            // Only this machine's sends are delayed, so setting it on one side adds the full latency to the round trip.
            driver.ModifyNetworkSimulatorParameters(new NetworkSimulatorParameter
            {
                SendDelayMS = latencyMs, SendJitterMS = jitterMs, SendPacketLossPercent = lossPercent / 2,
                ReceivePacketLossPercent = lossPercent / 2,
            });
        }

        public void Shutdown(string reason)
        {
            if (!driver.IsCreated) { peers.Clear(); return; }
            // The goodbye skips the network simulator (its delayed packets die with the driver) and goes out three times against loss.
            driver.ModifyNetworkSimulatorParameters(default);
            scratch.Clear(); scratch.Str(reason);
            for (int copy = 0; copy < 3; copy++)
                foreach (var peer in peers) if (peer.Phase >= NetPeerPhase.Welcomed) SendUnreliable(peer, NetMessage.Goodbye, scratch);
            foreach (var peer in peers) if (driver.GetConnectionState(peer.Connection) != NetworkConnection.State.Disconnected) driver.Disconnect(peer.Connection);
            driver.ScheduleFlushSend().Complete();
            driver.Dispose();
            peers.Clear(); IsHost = false; LocalSlot = 0;
            GameLog.Info(Category, "transport closed: " + reason);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Shutdown("dispose");
        }
    }
}
