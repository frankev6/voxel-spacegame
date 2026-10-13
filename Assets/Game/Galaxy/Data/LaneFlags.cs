using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.2.</summary>
    [System.Flags] public enum LaneFlags : byte { None = 0, Authored = 1, HiddenUntilKnown = 2 }
}
