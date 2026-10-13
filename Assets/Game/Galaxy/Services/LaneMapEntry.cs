using Game.Galaxy.Data;

namespace Game.Galaxy.Services
{
    /// <summary>One visible lane on the galaxy map (SPEC-30 §4.8, §5.12).</summary>
    public readonly struct LaneMapEntry
    {
        public readonly int LaneIndex;
        public readonly StarSystemId A, B;
        public readonly bool Open;
        public readonly string LockedMessage;
        public readonly int FuelCost;

        public LaneMapEntry(int laneIndex, StarSystemId a, StarSystemId b, bool open, string lockedMessage, int fuelCost)
        {
            LaneIndex = laneIndex;
            A = a;
            B = b;
            Open = open;
            LockedMessage = lockedMessage;
            FuelCost = fuelCost;
        }
    }
}
