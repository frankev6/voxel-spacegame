using Game.Core.Events;
using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.4 loader seam; TRV-006 attaches it to the existing SPEC-31 StarSystemLoader.</summary>
    public interface ITravelLoader
    {
        bool IsReady { get; }
        StarSystemId Loaded { get; }
        bool IsArrivalReady { get; }
        bool IsAreaReady(double3 positionM);
        bool BeginStarSystemChange(StarSystemId target, double3 arrivalPositionM, float arrivalYawRad, StarSystemEntryReason reason);
    }
}
