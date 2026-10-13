using Game.Core;
using Game.Galaxy.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    /// <summary>SPEC-30 §5.4: explicit little-endian fields, independent of native struct padding.</summary>
    internal static class GalaxyHash
    {
        internal static unsafe ulong Compute(NativeArray<StarSystemDef> systems, NativeArray<LaneDef> lanes, int generatorVersion)
        {
            using var bytes = new NativeList<byte>(Allocator.Temp);
            for (int i = 0; i < systems.Length; i++)
            {
                var s = systems[i];
                U32(bytes, s.Id.Value);
                U64(bytes, math.asulong(s.PositionLy.x)); U64(bytes, math.asulong(s.PositionLy.y));
                U64(bytes, s.Seed);
                U16(bytes, s.TemplateIndex); U16(bytes, s.AuthoredIndex); U16(bytes, s.StarTemplateIndex);
                U16(bytes, (ushort)s.Flags); bytes.Add(s.Danger);
            }
            for (int i = 0; i < lanes.Length; i++)
            {
                var lane = lanes[i];
                U32(bytes, lane.A.Value); U32(bytes, lane.B.Value); U32(bytes, math.asuint(lane.LengthLy));
                bytes.Add((byte)lane.Kind); bytes.Add((byte)lane.Flags);
                U16(bytes, lane.GateIndex); U16(bytes, lane.GateTemplateA); U16(bytes, lane.GateTemplateB);
            }
            return StableHash.Hash64((byte*)bytes.GetUnsafeReadOnlyPtr(), bytes.Length, (ulong)generatorVersion);
        }

        static void U16(NativeList<byte> output, ushort value)
        { output.Add((byte)value); output.Add((byte)(value >> 8)); }
        static void U32(NativeList<byte> output, uint value)
        { for (int shift = 0; shift < 32; shift += 8) output.Add((byte)(value >> shift)); }
        static void U64(NativeList<byte> output, ulong value)
        { for (int shift = 0; shift < 64; shift += 8) output.Add((byte)(value >> shift)); }
    }
}
