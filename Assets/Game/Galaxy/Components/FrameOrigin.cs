using Game.Galaxy.Data;
using Unity.Entities;

namespace Game.Galaxy.Components
{
    /// <summary>Singleton. Written only by OriginRebaseSystem. Implements SPEC-30 §4.7.</summary>
    public struct FrameOrigin : IComponentData
    {
        public Unity.Mathematics.double3 OriginInStarSystem;   // binding (architecture §6.5)
        public StarSystemId StarSystem;                        // star system this frame belongs to; None before the first load
        public uint ShiftSequence;                             // +1 on every rebase (threshold or request)
        public Unity.Mathematics.float3 LastShiftDelta;        // newLocal = oldLocal + delta for the last rebase; zero when it changed star system
        public byte LastShiftChangedStarSystem;                // 1 when the last rebase came from a request with a different star system
        public uint AppliedRequestSerial;                      // Serial of the last OriginRebaseRequest applied
    }
}
