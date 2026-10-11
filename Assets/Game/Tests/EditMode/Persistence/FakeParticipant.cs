using System;
using System.Collections.Generic;
using System.Threading;
using Game.Core;
using Game.Core.Save;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>Test participant: writes a few primitives, a content id and optionally a deferred block (SPEC-05 §4.9 rules).</summary>
    public sealed class FakeParticipant : ISaveParticipant
    {
        public string Key { get; }
        public int Version { get; }
        public int Value = 7;
        public bool ThrowOnCapture;
        public bool WithDeferred;
        public int DeferredSleepMs;
        public bool ThrowOnEncode;
        public readonly List<FakeDeferredBlock> Blocks = new List<FakeDeferredBlock>();
        public int RestoredValue = -1;

        public FakeParticipant(string key, int version = 1) { Key = key; Version = version; }

        public void Capture(SaveWriter writer)
        {
            writer.WriteInt32(Value);
            writer.WriteContentId(new ContentId("block_" + Key.ToLowerInvariant()));
            if (WithDeferred)
            {
                var b = new FakeDeferredBlock(Key, DeferredSleepMs, ThrowOnEncode);
                Blocks.Add(b);
                writer.WriteDeferred(b);
            }
            if (ThrowOnCapture) throw new InvalidOperationException("capture failure for the test");
            writer.WriteString("end");
        }

        public void Restore(SaveReader reader, int fromVersion) { RestoredValue = fromVersion == 0 ? 0 : reader.ReadInt32(); }
    }

    /// <summary>Counts Encode and Dispose; the live count proves every block is disposed on every path.</summary>
    public sealed class FakeDeferredBlock : ISaveDeferredBlock
    {
        static int live;
        public static int Live => Volatile.Read(ref live);
        public static void ResetLive() => Volatile.Write(ref live, 0);

        readonly string tag; readonly int sleepMs; readonly bool throwOnEncode;
        public int Encodes, Disposes;

        public FakeDeferredBlock(string tag, int sleepMs, bool throwOnEncode) { this.tag = tag; this.sleepMs = sleepMs; this.throwOnEncode = throwOnEncode; Interlocked.Increment(ref live); }

        public byte[] Encode()
        {
            Encodes++;
            if (sleepMs > 0) Thread.Sleep(sleepMs);
            if (throwOnEncode) throw new InvalidOperationException("encode failure for the test");
            return System.Text.Encoding.UTF8.GetBytes("deferred:" + tag);
        }

        public void Dispose()
        {
            if (Interlocked.Increment(ref Disposes) == 1) Interlocked.Decrement(ref live);   // idempotent, as IDisposable requires
        }
    }
}
