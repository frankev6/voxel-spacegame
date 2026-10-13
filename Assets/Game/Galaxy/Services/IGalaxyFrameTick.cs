using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>A Galaxy managed service that needs one call per frame. Called by GalaxyFrameTicker. Implements SPEC-30 §4.8.</summary>
    public interface IGalaxyFrameTick { int TickOrder { get; } void Tick(in GalaxyFrameContext ctx); }
}
