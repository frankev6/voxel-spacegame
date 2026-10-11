using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Core.Save;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-007: SaveWriter / SaveReader / SaveKey (SPEC-02 §4.7, §5.7).</summary>
    public sealed class SaveWriterReaderTests
    {
        /// <summary>Dictionary-backed palette for tests.</summary>
        internal sealed class TestPalette : ISaveIdPalette, ISaveIdPaletteReader
        {
            readonly List<ContentId> ids = new List<ContentId>();
            public ushort GetOrAdd(in ContentId id) { int i = ids.IndexOf(id); if (i < 0) { ids.Add(id); i = ids.Count - 1; } return (ushort)i; }
            public ContentId Get(ushort local) => ids[local];
        }

        /// <summary>JSON codec for int only: UTF-8 of value.ToString().</summary>
        internal sealed class IntJson : ISaveJsonCodec
        {
            public byte[] Serialize<T>(T value) => Encoding.UTF8.GetBytes(value.ToString());
            public T Deserialize<T>(ReadOnlySpan<byte> utf8) => (T)(object)int.Parse(Encoding.UTF8.GetString(utf8));
        }

        sealed class FakeDeferred : ISaveDeferredBlock
        {
            public readonly byte[] Bytes; public FakeDeferred(params byte[] b) { Bytes = b; }
            public byte[] Encode() => Bytes; public void Dispose() { }
        }

        static byte[] Bytes(Action<SaveWriter> write)
        {
            using var w = new SaveWriter();
            write(w);
            return w.ToArray();
        }

        static void AreBytes(string hex, byte[] actual, string what)
            => Assert.AreEqual(hex, BitConverter.ToString(actual).Replace("-", " "), what);

        [Test]
        public void Encoding_Examples_Exact()
        {
            AreBytes("00", Bytes(w => w.WriteBool(false)), "false"); AreBytes("01", Bytes(w => w.WriteBool(true)), "true");
            AreBytes("34 12", Bytes(w => w.WriteUInt16(0x1234)), "u16 LE");
            AreBytes("FF FF FF FF", Bytes(w => w.WriteInt32(-1)), "i32 −1");
            AreBytes("08 07 06 05 04 03 02 01", Bytes(w => w.WriteUInt64(0x0102030405060708)), "u64 LE");
            AreBytes("00 00 00 80", Bytes(w => w.WriteSingle(-0f)), "−0f bits");
            AreBytes("00", Bytes(w => w.WriteVarUInt32(0)), "var 0"); AreBytes("7F", Bytes(w => w.WriteVarUInt32(127)), "var 127");
            AreBytes("80 01", Bytes(w => w.WriteVarUInt32(128)), "var 128"); AreBytes("AC 02", Bytes(w => w.WriteVarUInt32(300)), "var 300");
            AreBytes("FF FF FF FF 0F", Bytes(w => w.WriteVarUInt32(uint.MaxValue)), "var max");
            AreBytes("00", Bytes(w => w.WriteVarInt32(0)), "zz 0"); AreBytes("01", Bytes(w => w.WriteVarInt32(-1)), "zz −1");
            AreBytes("02", Bytes(w => w.WriteVarInt32(1)), "zz 1"); AreBytes("03", Bytes(w => w.WriteVarInt32(-2)), "zz −2");
            AreBytes("00", Bytes(w => w.WriteString(null)), "null string"); AreBytes("01", Bytes(w => w.WriteString("")), "empty string");
            AreBytes("03 61 62", Bytes(w => w.WriteString("ab")), "\"ab\"");
            AreBytes("03 01 02 03", Bytes(w => w.WriteByteArray(new byte[] { 1, 2, 3 })), "byte array");
            AreBytes("02 00 00 00 05 06", Bytes(w => { var b = w.BeginBlock(); w.WriteByte(5); w.WriteByte(6); w.EndBlock(b); }), "block length prefix");
            AreBytes("00 00 80 3F 00 00 00 40 00 00 40 40", Bytes(w => w.WriteFloat3(new float3(1, 2, 3))), "float3 x,y,z");
        }

        [Test]
        public void RoundTrip_AllPrimitives()
        {
            float nanPayload = BitConverter.Int32BitsToSingle(0x7FC01234);
            var bytes = Bytes(w =>
            {
                w.WriteByte(200); w.WriteSByte(-5); w.WriteBool(true); w.WriteUInt16(65535); w.WriteInt16(short.MinValue);
                w.WriteUInt32(uint.MaxValue); w.WriteInt32(int.MinValue); w.WriteUInt64(ulong.MaxValue); w.WriteInt64(long.MinValue);
                w.WriteSingle(-0f); w.WriteSingle(nanPayload); w.WriteDouble(double.Epsilon); w.WriteDouble(-0.0);
                w.WriteVarUInt32(uint.MaxValue); w.WriteVarUInt64(ulong.MaxValue); w.WriteVarInt32(int.MinValue); w.WriteVarInt64(long.MinValue);
                w.WriteVarInt64(long.MaxValue);
                w.WriteFloat2(new float2(1, -2)); w.WriteFloat4(new float4(1, 2, 3, 4)); w.WriteQuaternion(new quaternion(0.1f, 0.2f, 0.3f, 0.9f));
                w.WriteDouble3(new double3(1e300, -1e-300, 0.5)); w.WriteInt3(new int3(-1, 0, int.MaxValue)); w.WriteBytes(new byte[] { 9, 8 });
            });
            var r = new SaveReader(bytes);
            Assert.AreEqual(200, r.ReadByte()); Assert.AreEqual(-5, r.ReadSByte()); Assert.IsTrue(r.ReadBool());
            Assert.AreEqual(65535, r.ReadUInt16()); Assert.AreEqual(short.MinValue, r.ReadInt16());
            Assert.AreEqual(uint.MaxValue, r.ReadUInt32()); Assert.AreEqual(int.MinValue, r.ReadInt32());
            Assert.AreEqual(ulong.MaxValue, r.ReadUInt64()); Assert.AreEqual(long.MinValue, r.ReadInt64());
            Assert.AreEqual(BitConverter.SingleToInt32Bits(-0f), BitConverter.SingleToInt32Bits(r.ReadSingle()), "−0 kept");
            Assert.AreEqual(0x7FC01234, BitConverter.SingleToInt32Bits(r.ReadSingle()), "NaN payload kept");
            Assert.AreEqual(double.Epsilon, r.ReadDouble()); Assert.AreEqual(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(r.ReadDouble()));
            Assert.AreEqual(uint.MaxValue, r.ReadVarUInt32()); Assert.AreEqual(ulong.MaxValue, r.ReadVarUInt64());
            Assert.AreEqual(int.MinValue, r.ReadVarInt32()); Assert.AreEqual(long.MinValue, r.ReadVarInt64()); Assert.AreEqual(long.MaxValue, r.ReadVarInt64());
            Assert.AreEqual(new float2(1, -2), r.ReadFloat2()); Assert.AreEqual(new float4(1, 2, 3, 4), r.ReadFloat4());
            Assert.AreEqual(new quaternion(0.1f, 0.2f, 0.3f, 0.9f), r.ReadQuaternion());
            Assert.AreEqual(new double3(1e300, -1e-300, 0.5), r.ReadDouble3()); Assert.AreEqual(new int3(-1, 0, int.MaxValue), r.ReadInt3());
            var two = new byte[2]; r.ReadBytes(two); CollectionAssert.AreEqual(new byte[] { 9, 8 }, two);
            Assert.IsTrue(r.IsAtEnd);
        }

        [Test]
        public void RoundTrip_Strings_NullEmptyUnicode()
        {
            var strings = new[] { null, "", "a", "Kraków_ünïcödé", "日本語のテキスト", "emoji 🚀", new string('x', 70000) };
            var bytes = Bytes(w => { foreach (var s in strings) w.WriteString(s); });
            var r = new SaveReader(bytes);
            foreach (var s in strings) Assert.AreEqual(s, r.ReadString());
            Assert.IsTrue(r.IsAtEnd);
        }

        [Test]
        public void RoundTrip_NativeArray()
        {
            using var empty = new NativeArray<int3>(0, Allocator.Temp);
            var values = new uint[256 * 1024];   // 1 MiB
            for (int i = 0; i < values.Length; i++) values[i] = (uint)(i * 2654435761u);
            using var big = new NativeArray<uint>(values, Allocator.Temp);
            var bytes = Bytes(w => { w.WriteNativeArray(empty); w.WriteNativeArray(big); });
            var r = new SaveReader(bytes);
            using var e2 = r.ReadNativeArray<int3>(Allocator.Temp); Assert.AreEqual(0, e2.Length);
            using var b2 = r.ReadNativeArray<uint>(Allocator.Temp);
            Assert.AreEqual(big.Length, b2.Length);
            Assert.IsTrue(big.SequenceEqual(b2));
        }

        [Test]
        public void Deterministic_SameBytes()
        {
            void Write(SaveWriter w) { w.WriteString("block_hull"); w.WriteDouble(Math.PI); var b = w.BeginBlock(); w.WriteVarInt64(-12345); w.EndBlock(b); }
            CollectionAssert.AreEqual(Bytes(Write), Bytes(Write));
        }

        [Test]
        public void Blocks_Nested_RoundTrip()
        {
            var bytes = Bytes(w =>
            {
                var outer = w.BeginBlock(); w.WriteInt32(1);
                var inner = w.BeginBlock(); w.WriteString("in"); w.EndBlock(inner);
                w.WriteInt32(2); w.EndBlock(outer); w.WriteInt32(3);
            });
            var r = new SaveReader(bytes);
            var o = r.BeginBlock(); Assert.AreEqual(1, r.ReadInt32());
            var i = r.BeginBlock(); Assert.AreEqual("in", r.ReadString()); r.EndBlock(i);
            Assert.AreEqual(2, r.ReadInt32()); r.EndBlock(o); Assert.AreEqual(3, r.ReadInt32()); Assert.IsTrue(r.IsAtEnd);
        }

        [Test]
        public void Block_OlderReaderSkipsNewTail()
        {
            var bytes = Bytes(w => { var b = w.BeginBlock(); w.WriteInt32(7); w.WriteString("added in v2"); w.WriteDouble(1); w.EndBlock(b); w.WriteInt32(99); });
            var r = new SaveReader(bytes);
            var blk = r.BeginBlock(); Assert.AreEqual(7, r.ReadInt32());
            Assert.Throws<SaveFormatException>(() => r.Skip(1000), "reads stop at the block end");
            r.EndBlock(blk);
            Assert.AreEqual(99, r.ReadInt32(), "the unread tail was skipped");
        }

        [Test]
        public void EndBlock_Unbalanced_Throws()
        {
            using var w = new SaveWriter();
            var outer = w.BeginBlock(); var inner = w.BeginBlock();
            Assert.Throws<InvalidOperationException>(() => w.EndBlock(outer), "LIFO only");
            Assert.Throws<InvalidOperationException>(() => w.ToArray(), "open blocks");
            w.EndBlock(inner); w.EndBlock(outer);
            Assert.Throws<InvalidOperationException>(() => w.EndBlock(outer), "already closed");
            w.Dispose();
            Assert.Throws<ObjectDisposedException>(() => w.WriteByte(1));
        }

        [Test]
        public void Reader_Truncated_ThrowsSaveFormat()
        {
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 1, 2, 3 }).ReadInt32());
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 5, 0x61 }).ReadString(), "string longer than the data");
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 10, 0, 0, 0, 1 }).BeginBlock(), "block longer than the data");
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F }).ReadNativeArray<long>(Allocator.Temp), "array larger than the data");
            var sliced = new SaveReader(new byte[] { 9, 1, 2, 3, 9 }, 1, 3);
            Assert.AreEqual(3, sliced.Length); Assert.AreEqual(1, sliced.ReadByte()); Assert.AreEqual(1, sliced.Position);
        }

        [Test]
        public void Reader_BadBool_Throws() { Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 2 }).ReadBool()); }

        [Test]
        public void Reader_VarIntOverflow_Throws()
        {
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x10 }).ReadVarUInt32(), "5th byte > 0x0F");
            Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 }).ReadVarUInt32(), "6 bytes");
            Assert.Throws<SaveFormatException>(() => new SaveReader(Enumerable.Repeat((byte)0xFF, 9).Concat(new byte[] { 0x02 }).ToArray()).ReadVarUInt64(), "10th byte > 0x01");
            Assert.AreEqual(ulong.MaxValue, new SaveReader(Enumerable.Repeat((byte)0xFF, 9).Concat(new byte[] { 0x01 }).ToArray()).ReadVarUInt64());
        }

        [Test]
        public void Reader_InvalidUtf8_Throws()
        {
            var ex = Assert.Throws<SaveFormatException>(() => new SaveReader(new byte[] { 3, 0xC3, 0x28 }).ReadString());
            StringAssert.Contains("invalid UTF-8", ex.Message);
        }

        [Test]
        public void SaveKey_Validation()
        {
            Assert.IsTrue(SaveKey.IsValid("SESS")); Assert.IsTrue(SaveKey.IsValid("A1B2"));
            foreach (var bad in new[] { "sess", "SES", "SESS1", null, "SE-S" }) Assert.IsFalse(SaveKey.IsValid(bad), bad ?? "null");
            foreach (var k in new[] { "SESS", "FLET", "ZZZZ", "0000" }) Assert.AreEqual(k, SaveKey.FromFourCC(SaveKey.ToFourCC(k)));
            Assert.AreEqual((uint)'S' | (uint)'E' << 8 | (uint)'S' << 16 | (uint)'S' << 24, SaveKey.ToFourCC("SESS"));
            Assert.Throws<ArgumentException>(() => SaveKey.ToFourCC("bad"));
            Assert.Throws<SaveFormatException>(() => SaveKey.FromFourCC(0x00414141));
        }

        [Test]
        public void ContentId_PaletteMode_RoundTrip()
        {
            var palette = new TestPalette();
            using var w = new SaveWriter(palette);
            w.WriteContentId(new ContentId("block_hull")); w.WriteContentId(new ContentId("block_hull")); w.WriteContentId(new ContentId("ship_frigate"));
            var bytes = w.ToArray();
            AreBytes("00 00 00 00 01 00", bytes, "two identical UInt16s, then the next palette index");
            var r = new SaveReader(bytes, palette);
            Assert.AreEqual(new ContentId("block_hull"), r.ReadContentId()); Assert.AreEqual(new ContentId("block_hull"), r.ReadContentId());
            Assert.AreEqual(new ContentId("ship_frigate"), r.ReadContentId());
        }

        [Test]
        public void ContentId_StringMode_RoundTrip()
        {
            var bytes = Bytes(w => { w.WriteContentId(new ContentId("block_hull")); w.WriteContentId(ContentId.None); });
            var r = new SaveReader(bytes);
            Assert.AreEqual(new ContentId("block_hull"), r.ReadContentId()); Assert.IsTrue(r.ReadContentId().IsNone);
        }

        [Test]
        public void Deferred_SplicedInOrder()
        {
            using var w = new SaveWriter();
            w.WriteByte(0xAA);                                   // prefix
            w.WriteDeferred(new FakeDeferred(1, 2, 3));
            w.WriteByte(0xBB);                                   // middle
            w.WriteDeferred(new FakeDeferred(Enumerable.Repeat((byte)7, 200).ToArray()));
            w.WriteByte(0xCC);                                   // suffix
            Assert.AreEqual(2, w.DeferredCount);
            var bytes = w.ToArray(b => b.Encode());
            var expected = new List<byte> { 0xAA, 3, 1, 2, 3, 0xBB, 0xC8, 0x01 };
            expected.AddRange(Enumerable.Repeat((byte)7, 200)); expected.Add(0xCC);
            CollectionAssert.AreEqual(expected, bytes);
            var r = new SaveReader(bytes);
            Assert.AreEqual(0xAA, r.ReadByte()); CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, r.ReadByteArray());
            Assert.AreEqual(0xBB, r.ReadByte()); Assert.AreEqual(200, r.ReadByteArray().Length); Assert.AreEqual(0xCC, r.ReadByte());
            Assert.Throws<InvalidOperationException>(() => w.ToArray(_ => null), "an encoder returning null");
        }

        [Test]
        public void Deferred_InsideBlock_Throws()
        {
            using var w = new SaveWriter();
            var b = w.BeginBlock();
            Assert.Throws<InvalidOperationException>(() => w.WriteDeferred(new FakeDeferred(1)));
            w.EndBlock(b);
        }

        [Test]
        public void ToArrayWithoutEncoder_Throws()
        {
            using var w = new SaveWriter();
            w.WriteDeferred(new FakeDeferred(1));
            Assert.Throws<InvalidOperationException>(() => w.ToArray());
            Assert.Throws<InvalidOperationException>(() => w.AsSpan());
        }

        [Test]
        public void Json_RoundTrip_AndErrorsWrapped()
        {
            var json = new IntJson();
            using var w = new SaveWriter(null, json);
            w.WriteJson(42);
            var r = new SaveReader(w.ToArray(), null, json);
            Assert.AreEqual(42, r.ReadJson<int>());
            var bad = new SaveReader(new byte[] { 2, (byte)'x', (byte)'y' }, null, json);
            Assert.Throws<SaveFormatException>(() => bad.ReadJson<int>(), "codec exception wrapped");
            Assert.Throws<InvalidOperationException>(() => new SaveWriter().WriteJson(1), "no codec");
            Assert.Throws<InvalidOperationException>(() => new SaveReader(new byte[] { 0 }).ReadJson<int>(), "no codec");
        }

        [Test]
        public void Empty_IsAtEnd()
        {
            Assert.IsTrue(SaveReader.Empty.IsAtEnd);
            Assert.AreEqual(0, SaveReader.Empty.Remaining);
            Assert.Throws<SaveFormatException>(() => SaveReader.Empty.ReadByte());
            Assert.AreEqual(0, SaveReader.Empty.Position, "immutable");
        }
    }
}
