using Game.Galaxy.Data;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.4/§5.5 managed HUD gate prompt snapshot.</summary>
    public struct GateInfo
    {
        public int LaneIndex;
        public StarSystemId Target;
        public string TargetName;
        public bool Known, Open, CanAfford, OnRoute;
        public string LockedMessage;
        public int FuelCost;
        public int FuelHeld; // -1 when no economy is available.
    }
}
