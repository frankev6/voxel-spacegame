namespace Game.Core.Events
{
    /// <summary>Raised on every rebase (architecture §6.5). Delivered next frame through ManagedEventQueue&lt;OriginShifted&gt;;
    /// same-frame consumers read FrameOrigin.ShiftSequence/LastShiftDelta instead (§5.10). Implements SPEC-30 §4.7.</summary>
    public readonly struct OriginShifted
    {
        public readonly Unity.Mathematics.float3 Delta;    // binding; zero when StarSystemChanged == 1 (same-star-system requests carry their delta)
        public readonly byte StarSystemChanged;            // 1: everything local was replaced; consumers reset (camera cut, clear trails)
        public readonly uint StarSystemValue;              // StarSystemId.Value of the frame after the rebase
        public readonly uint Sequence;                     // FrameOrigin.ShiftSequence after the rebase
        public OriginShifted(Unity.Mathematics.float3 delta, byte changed, uint starSystemValue, uint seq) { Delta = delta; StarSystemChanged = changed; StarSystemValue = starSystemValue; Sequence = seq; }
    }
}
