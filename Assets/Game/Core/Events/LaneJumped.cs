namespace Game.Core.Events
{
    /// <summary>SPEC-32 §4.1/§5.3: managed TravelService publishes once at the start of Arriving; no ECS queue.</summary>
    public readonly struct LaneJumped
    {
        public readonly uint FromStarSystemValue;
        public readonly uint ToStarSystemValue;
        public readonly int LaneIndex; // -1 for a narrative jump without a connecting lane.
        public readonly JumpSource Source;
        public LaneJumped(uint from, uint to, int lane, JumpSource source)
        { FromStarSystemValue = from; ToStarSystemValue = to; LaneIndex = lane; Source = source; }
    }
}
