using System;
using Game.Data.Blocks;
using Unity.Mathematics;

namespace Game.Editor.Migration
{
    /// <summary>SPEC-72 §5.5: snap within one degree, then match exact integer Ry·Rx·Rz against the cube group.</summary>
    public static class LegacyEuler
    {
        public const float SnapToleranceDegrees = 1.0f;
        public static bool TryToOrientation(float rotX, float rotY, float rotZ, out byte orientation, out float maxSnapErrorDegrees)
        {
            orientation = Orientation.Invalid;
            bool x = Snap(rotX, out int qx, out float ex);
            bool y = Snap(rotY, out int qy, out float ey);
            bool z = Snap(rotZ, out int qz, out float ez);
            maxSnapErrorDegrees = Math.Max(ex, Math.Max(ey, ez));
            if (!x || !y || !z) return false;
            int cx = Cos(qx), sx = Sin(qx), cy = Cos(qy), sy = Sin(qy), cz = Cos(qz), sz = Sin(qz);
            var rx = new int3x3(1, 0, 0, 0, cx, -sx, 0, sx, cx);
            var ry = new int3x3(cy, 0, sy, 0, 1, 0, -sy, 0, cy);
            var rz = new int3x3(cz, -sz, 0, sz, cz, 0, 0, 0, 1);
            return OrientationTables.TryFromMatrix(math.mul(math.mul(ry, rx), rz), out orientation);
        }
        static bool Snap(float angle, out int quarter, out float error)
        {
            quarter = 0;
            if (float.IsNaN(angle) || float.IsInfinity(angle)) { error = float.PositiveInfinity; return false; }
            // Equivalent modulo full turns, but the integer cast cannot overflow on large finite inputs.
            double reduced = (double)angle % 360.0;
            int q = (int)Math.Round(reduced / 90.0, MidpointRounding.AwayFromZero);
            error = (float)Math.Abs(reduced - 90.0 * q);
            quarter = ((q % 4) + 4) % 4;
            return error <= SnapToleranceDegrees;
        }
        static int Cos(int q) => q == 0 ? 1 : q == 2 ? -1 : 0;
        static int Sin(int q) => q == 1 ? 1 : q == 3 ? -1 : 0;
    }
}
