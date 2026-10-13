using Game.Data.Definitions;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-30 §4.8.</summary>
    public readonly struct GalaxyFrameContext
    {
        public readonly Unity.Entities.EntityManager EntityManager;
        public readonly double UniversalTime;    // SimClock.UniversalTime
        public readonly uint Tick;               // SimClock.Tick
        public readonly float UnscaledDeltaTime; // UnityEngine.Time.unscaledDeltaTime, clamped to 0.1
        public readonly int FrameCount;          // UnityEngine.Time.frameCount

        public GalaxyFrameContext(Unity.Entities.EntityManager entityManager, double universalTime, uint tick, float unscaledDeltaTime, int frameCount)
        {
            EntityManager = entityManager;
            UniversalTime = universalTime;
            Tick = tick;
            UnscaledDeltaTime = unscaledDeltaTime;
            FrameCount = frameCount;
        }
    }
}
