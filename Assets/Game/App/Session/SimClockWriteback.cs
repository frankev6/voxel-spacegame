using System;
using Game.Core.Components;
using Game.Core.Flow;
using Unity.Entities;

namespace Game.App.Session
{
    /// <summary>Copies the World's SimClock.UniversalTime back into the session (order 0, before the fleet write-back). Only in the World:
    /// the Blueprint Lab's clock is a throwaway that never reaches the campaign (SPEC-01 decision 7). Implements SPEC-01 §5.7.</summary>
    public sealed class SimClockWriteback : ISessionWriteback
    {
        readonly World world;
        readonly Action<double> setUniversalTime;

        /// <param name="setUniversalTime">Writes GameSession.UniversalTime (FND-009 passes <c>t => session.UniversalTime = t</c>).</param>
        public SimClockWriteback(World world, Action<double> setUniversalTime)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.setUniversalTime = setUniversalTime ?? throw new ArgumentNullException(nameof(setUniversalTime));
        }

        public int WritebackOrder => 0;

        public void WriteBackFromWorld()
        {
            if (!world.IsCreated) return;
            var em = world.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<SimClock>(), ComponentType.ReadOnly<WorldSceneActive>());
            if (q.CalculateEntityCount() != 1) return;
            em.CompleteDependencyBeforeRO<SimClock>();
            setUniversalTime(q.GetSingleton<SimClock>().UniversalTime);
        }
    }
}
