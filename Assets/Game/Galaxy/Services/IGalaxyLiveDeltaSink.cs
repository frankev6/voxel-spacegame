using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>Implemented by SPEC-31 (StarSystemLoader). Lets delta writes reach the loaded star system. Implements SPEC-30 §4.8.</summary>
    public interface IGalaxyLiveDeltaSink
    {
        void PrepareBodyRemoval(BodyId body);        // ADR-0077: suppress capture and retire live source ownership before deleting a snapshot
        void OnBodyDeltaChanged(BodyId body);         // replacement / story spawn added or removed in a loaded star system
        void FlushStructures();                       // capture damaged body grids into deltas now (called before save)
    }
}
