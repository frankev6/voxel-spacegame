using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Managed lane gate condition (only authored lanes are gated in v1). Implements SPEC-30 §4.</summary>
    public sealed class LaneGateSpec
    {
        public Game.Data.Logic.CompiledCondition Condition;   // the authored lane's object (not copied); bound at boot by SPEC-40
        public string LockedMessage;
    }
}
