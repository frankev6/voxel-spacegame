using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.2.</summary>
    public enum InteractionKind : byte { None = 0, Dock = 1, Inspect = 2, LaneGate = 3, Hail = 4, Salvage = 5 }
}
