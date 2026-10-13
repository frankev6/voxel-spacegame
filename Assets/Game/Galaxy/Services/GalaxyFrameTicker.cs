using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>Registry of IGalaxyFrameTick. Constructed by SPEC-01 before every Galaxy service; ticked by GalaxyServicesBridgeSystem. Implements SPEC-30 §4.8.</summary>
    public sealed class GalaxyFrameTicker
    {
        public void Register(IGalaxyFrameTick t) => throw new System.NotImplementedException("GAL-008");     // idempotent
        public void Unregister(IGalaxyFrameTick t) => throw new System.NotImplementedException("GAL-008");
        public void Tick(in GalaxyFrameContext ctx) => throw new System.NotImplementedException("GAL-008");  // order: TickOrder ascending, then type full name (ordinal); per-service try/catch → GameLog.Error, others still run
        // TODO(GAL-008): implement the behavior specified by SPEC-30.
    }
}
