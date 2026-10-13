using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>Implemented by SPEC-32 (TravelService). Lane availability for the map and routes. Implements SPEC-30 §4.8.</summary>
    public interface ILaneAccess
    {
        bool IsLaneOpen(int laneIndex);               // gate condition true (ungated lanes: always true)
        string GetLockedMessage(int laneIndex);       // "" when open
        int GetFuelCost(int laneIndex);
        uint Revision { get; }                        // +1 whenever any IsLaneOpen result may have changed (the "ILaneAccess change counter" of §5.12)
    }
}
