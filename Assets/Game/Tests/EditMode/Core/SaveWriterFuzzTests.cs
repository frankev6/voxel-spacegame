using System;
using Game.Core;
using Game.Core.Save;
using NUnit.Framework;
using Unity.Collections;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-007: corrupt streams only ever raise SaveFormatException (SPEC-02 §5.7, §7).</summary>
    public sealed class SaveWriterFuzzTests
    {
        static byte[] ValidStream()
        {
            using var w = new SaveWriter(null, new SaveWriterReaderTests.IntJson());
            w.WriteString("block_hull"); w.WriteVarInt64(-123456789);
            var outer = w.BeginBlock();
            w.WriteBool(true); w.WriteDouble(Math.E); w.WriteJson(7);
            var inner = w.BeginBlock(); w.WriteString("Kraków"); w.WriteByteArray(new byte[] { 1, 2, 3, 4 }); w.EndBlock(inner);
            using (var arr = new NativeArray<int>(new[] { 1, 2, 3 }, Allocator.Temp)) w.WriteNativeArray(arr);
            w.EndBlock(outer);
            w.WriteVarUInt32(300); w.WriteContentId(new ContentId("ship_frigate"));
            return w.ToArray();
        }

        static void ReadAll(byte[] data)
        {
            var r = new SaveReader(data, null, new SaveWriterReaderTests.IntJson());
            r.ReadString(); r.ReadVarInt64();
            var outer = r.BeginBlock();
            r.ReadBool(); r.ReadDouble(); r.ReadJson<int>();
            var inner = r.BeginBlock(); r.ReadString(); r.ReadByteArray(); r.EndBlock(inner);
            using (var a = r.ReadNativeArray<int>(Allocator.Temp)) { }
            r.EndBlock(outer);
            r.ReadVarUInt32(); r.ReadContentId();
        }

        [Test]
        public void CorruptStreams_OnlySaveFormatException()
        {
            var valid = ValidStream();
            Assert.DoesNotThrow(() => ReadAll(valid));
            int failures = 0;
            for (int i = 0; i < 10000; i++)
            {
                var rng = RngStreams.For(1, RngPurpose.Test, (ulong)i);
                byte[] data;
                if (rng.NextBool()) data = valid.AsSpan(0, rng.NextInt(0, valid.Length)).ToArray();            // truncation
                else
                {
                    data = (byte[])valid.Clone();
                    int flips = rng.NextInt(1, 4);
                    for (int f = 0; f < flips; f++) data[rng.NextInt(0, data.Length)] ^= (byte)(1 << rng.NextInt(0, 8));
                }
                try { ReadAll(data); }
                catch (SaveFormatException) { failures++; }
                catch (Exception e) { Assert.Fail($"case {i}: {e.GetType().Name}: {e.Message}"); }
            }
            Assert.Greater(failures, 1000, "most corruptions are detected");
        }
    }
}
