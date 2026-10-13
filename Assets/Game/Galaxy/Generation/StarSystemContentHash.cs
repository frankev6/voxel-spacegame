using Game.Core;
using Game.Galaxy.Data;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    /// <summary>SPEC-30 §5.4: declaration-order body fields, raw IEEE bits, no mutable delta state or struct padding.</summary>
    internal static class StarSystemContentHash
    {
        internal static unsafe ulong Compute(NativeArray<BodySpec> bodies, int generatorVersion)
        {
            using var bytes = new NativeList<byte>(Allocator.Temp);
            foreach (var body in bodies)
            {
                U32(bytes, body.Id.System.Value); bytes.Add((byte)body.Id.Role);
                U16(bytes, body.Id.Slot); U16(bytes, body.Id.SubSlot);
                U64(bytes, body.ParentKey); U16(bytes, body.TemplateIndex); U16(bytes, body.LaneIndex);
                Orbit(bytes, body.Orbit); Orbit(bytes, body.ParentOrbit);
                U32(bytes, math.asuint(body.RadiusM)); U32(bytes, math.asuint(body.YawRad));
                U32(bytes, math.asuint(body.BeltInnerRadiusM)); U32(bytes, math.asuint(body.BeltOuterRadiusM));
                U64(bytes, body.Seed);
            }
            return StableHash.Hash64((byte*)bytes.GetUnsafeReadOnlyPtr(), bytes.Length, (ulong)generatorVersion);
        }
        static void Orbit(NativeList<byte> bytes, OrbitElements orbit)
        {
            bytes.Add((byte)orbit.Mode);
            U64(bytes, math.asulong(orbit.RadiusM)); U64(bytes, math.asulong(orbit.Eccentricity));
            U64(bytes, math.asulong(orbit.PeriodS)); U64(bytes, math.asulong(orbit.PhaseRad));
            U64(bytes, math.asulong(orbit.ArgPeriapsisRad));
        }
        static void U16(NativeList<byte> bytes, ushort value)
        { bytes.Add((byte)value); bytes.Add((byte)(value >> 8)); }
        static void U32(NativeList<byte> bytes, uint value)
        { for (int shift = 0; shift < 32; shift += 8) bytes.Add((byte)(value >> shift)); }
        static void U64(NativeList<byte> bytes, ulong value)
        { for (int shift = 0; shift < 64; shift += 8) bytes.Add((byte)(value >> shift)); }
    }
}
