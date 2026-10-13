using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.4.</summary>
    public struct WeightedIndex { public ushort Index; public float Weight; public int MaxCount; }
}
