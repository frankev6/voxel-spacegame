namespace Game.Editor.Migration
{
    /// <summary>Implements SPEC-72 §4.2. Legacy Euler rotations to orientation bytes.</summary>
    public static class LegacyEuler
    {
        public const float SnapToleranceDegrees = 1.0f;
        public static bool TryToOrientation(float rotX, float rotY, float rotZ, out byte orientation, out float maxSnapErrorDegrees) { throw new System.NotImplementedException("MIG-007"); }
    }
}
