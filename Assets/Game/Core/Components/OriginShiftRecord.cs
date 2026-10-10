namespace Game.Core.Components
{
    /// <summary>Mirror of the last floating-origin rebase for modules that cannot read SPEC-30's FrameOrigin (Simulation).
    /// On the FrameOrigin entity; created by OriginRebaseSystem.OnCreate (all zero) and written only by its DecideJob (SPEC-30 §5.10).
    /// Holds the last rebase only: a consumer whose last applied Sequence is more than 1 behind treats it as a star-system change.
    /// Implements SPEC-02 §4.3.</summary>
    public struct OriginShiftRecord : Unity.Entities.IComponentData
    {
        public uint Sequence;                      // FrameOrigin.ShiftSequence after the rebase; 0 = no rebase yet
        public Unity.Mathematics.float3 Delta;     // newLocal = oldLocal + Delta; zero when StarSystemChanged == 1
        public byte StarSystemChanged;             // 1: everything local was replaced
    }
}
