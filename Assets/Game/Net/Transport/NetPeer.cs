using System;
using Unity.Networking.Transport;

namespace Game.Net.Transport
{
    public enum NetPeerPhase : byte { Connecting = 0, Handshaking = 1, Welcomed = 2, Ready = 3, InWorld = 4, Closing = 5 }

    /// <summary>One remote end: on the host one per guest, on a guest exactly one (the host, slot 0). Main thread.</summary>
    public sealed class NetPeer
    {
        internal NetworkConnection Connection;
        public byte Slot { get; internal set; }
        public string Name { get; internal set; } = "";
        public NetPeerPhase Phase { get; internal set; }
        public float RttMs { get; internal set; }
        public double LastHeardSeconds { get; internal set; }
        public uint LastCommandSequence { get; internal set; }

        // Reliable ordered byte stream (ADR-0116 §6): whole messages are appended, the transport sends it in chunks.
        internal byte[] Outbox = new byte[64 * 1024];
        internal int OutboxStart, OutboxEnd;
        internal byte[] Inbox = new byte[64 * 1024];
        internal int InboxLength;

        public int PendingReliableBytes => OutboxEnd - OutboxStart;

        internal void Append(byte[] source, int offset, int count)
        {
            if (OutboxStart > 0 && OutboxEnd + count > Outbox.Length)
            {
                Buffer.BlockCopy(Outbox, OutboxStart, Outbox, 0, OutboxEnd - OutboxStart);
                OutboxEnd -= OutboxStart; OutboxStart = 0;
            }
            if (OutboxEnd + count > Outbox.Length)
            {
                int size = Outbox.Length * 2; while (size < OutboxEnd + count) size *= 2;
                Array.Resize(ref Outbox, size);
            }
            Buffer.BlockCopy(source, offset, Outbox, OutboxEnd, count); OutboxEnd += count;
        }
        internal void Received(ReadOnlySpan<byte> bytes)
        {
            if (InboxLength + bytes.Length > Inbox.Length)
            {
                int size = Inbox.Length * 2; while (size < InboxLength + bytes.Length) size *= 2;
                Array.Resize(ref Inbox, size);
            }
            bytes.CopyTo(new Span<byte>(Inbox, InboxLength, bytes.Length)); InboxLength += bytes.Length;
        }
        internal void Consume(int count)
        {
            Buffer.BlockCopy(Inbox, count, Inbox, 0, InboxLength - count); InboxLength -= count;
        }
    }
}
