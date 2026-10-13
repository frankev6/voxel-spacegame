using Game.App.Systems;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class BodyOnRailsTests
    {
        static readonly double3 Origin = new(1500, 0, -900);
        static readonly quaternion Rotation = quaternion.RotateY(.7f);
        static BodyOrbit[] Orbits() => new[]
        {
            new BodyOrbit { Orbit = new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 1000, PeriodS = 60, PhaseRad = .4 } },
            new BodyOrbit { Orbit = new OrbitElements { Mode = OrbitMode.Elliptic, RadiusM = 2200, PeriodS = 100,
                Eccentricity = .2, PhaseRad = .8, ArgPeriapsisRad = .7 } },
            new BodyOrbit { Orbit = new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 60, PeriodS = 10, PhaseRad = .2 },
                ParentOrbit = new OrbitElements { Mode = OrbitMode.Elliptic, RadiusM = 2200, PeriodS = 100,
                    Eccentricity = .2, PhaseRad = .8, ArgPeriapsisRad = .7 } }
        };
        static Entity Body(EntityManager em, BodyOrbit orbit, bool analytic = true)
        {
            var entity = em.CreateEntity(typeof(BodyOrbit), typeof(UniversePosition), typeof(LocalTransform));
            em.SetComponentData(entity, orbit);
            em.SetComponentData(entity, new UniversePosition { Value = new double3(-7, 11, 37) });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(new float3(3, 4, 5), Rotation, 2));
            if (analytic) em.AddComponent<UniversePositionIsAnalytic>(entity);
            return entity;
        }
        static void Check(EntityManager em, Entity entity, BodyOrbit orbit, double time, double3 origin)
        {
            var expected = OrbitMath.Position(new BodySpec { Orbit = orbit.Orbit, ParentOrbit = orbit.ParentOrbit }, time);
            var position = em.GetComponentData<UniversePosition>(entity).Value;
            var transform = em.GetComponentData<LocalTransform>(entity);
            // The managed oracle and Burst's transcendental arithmetic can differ in their last bits.
            // Ten nanometres is still far tighter than the specified 1 mm local-position tolerance.
            Assert.That(math.distance(expected, position), Is.LessThanOrEqualTo(1e-8));
            Assert.That(math.distance((float3)(expected - origin), transform.Position), Is.LessThanOrEqualTo(.001f));
            Assert.AreEqual(Rotation, transform.Rotation); Assert.AreEqual(2, transform.Scale);
        }

        [Test] public void MatchesOrbitMath()
        {
            using var world = EcsTestWorld.Create(typeof(BodyOnRailsSystem));
            var active = world.AddGameplayActive(); world.AddWorldSceneActive(); var clockEntity = world.AddSimClock();
            var originEntity = world.AddSingleton(new FrameOrigin { OriginInStarSystem = Origin });
            var em = world.EntityManager; var orbits = Orbits(); var entities = new Entity[orbits.Length];
            for (int i = 0; i < entities.Length; i++) entities[i] = Body(em, orbits[i]);
            var physical = Body(em, orbits[0], false);
            var untouchedPosition = em.GetComponentData<UniversePosition>(physical);
            var untouchedTransform = em.GetComponentData<LocalTransform>(physical);
            uint tick = 0;
            foreach (double time in new[] { 0, 1.0 / 60, 10, 30, 1e9 + .125 })
            {
                em.SetComponentData(clockEntity, new SimClock { Tick = tick++, UniversalTime = time });
                world.UpdateSystem<BodyOnRailsSystem>(); world.CompleteAll();
                for (int i = 0; i < entities.Length; i++) Check(em, entities[i], orbits[i], time, Origin);
            }
            var movedOrigin = new double3(1e9, 0, -1e9);
            em.SetComponentData(originEntity, new FrameOrigin { OriginInStarSystem = movedOrigin, ShiftSequence = 1 });
            world.UpdateSystem<BodyOnRailsSystem>(); world.CompleteAll();
            for (int i = 0; i < entities.Length; i++) Check(em, entities[i], orbits[i], 1e9 + .125, movedOrigin);
            Assert.AreEqual(untouchedPosition, em.GetComponentData<UniversePosition>(physical));
            Assert.AreEqual(untouchedTransform, em.GetComponentData<LocalTransform>(physical));

            var held = em.GetComponentData<UniversePosition>(entities[0]);
            em.SetComponentData(clockEntity, new SimClock { UniversalTime = 123 });
            using var sceneQuery = em.CreateEntityQuery(typeof(WorldSceneActive)); var scene = sceneQuery.GetSingletonEntity();
            em.RemoveComponent<WorldSceneActive>(scene);
            world.UpdateSystem<BodyOnRailsSystem>(); world.CompleteAll();
            Assert.AreEqual(held, em.GetComponentData<UniversePosition>(entities[0]));
            em.AddComponent<WorldSceneActive>(scene); em.RemoveComponent<GameplayActive>(active);
            world.UpdateSystem<BodyOnRailsSystem>(); world.CompleteAll();
            Assert.AreEqual(held, em.GetComponentData<UniversePosition>(entities[0]));
        }

        [Test] public void FrameRateIndependent()
        {
            var orbits = Orbits(); var baseline = new double3[3, orbits.Length]; var localBaseline = new float3[3, orbits.Length];
            bool first = true;
            foreach (int fps in new[] { 30, 60, 144 })
            {
                using var world = EcsTestWorld.Create(typeof(BodyOnRailsSystem), typeof(SimClockAdvanceSystem));
                world.AddGameplayActive(); world.AddWorldSceneActive(); world.AddSimClock(universalTime: 1000);
                world.AddSingleton(new FrameOrigin { OriginInStarSystem = Origin });
                var em = world.EntityManager; var entities = new Entity[orbits.Length];
                for (int i = 0; i < entities.Length; i++) entities[i] = Body(em, orbits[i]);
                int frames = 0;
                for (int sample = 0; sample < 3; sample++)
                {
                    uint targetTick = (uint)(2 * (sample + 1));
                    while (world.TicksRun < targetTick && frames++ < 32) world.StepFrame(1f / fps);
                    Assert.AreEqual(targetTick, world.TicksRun, "Bounded fixture must reach exactly the sampled tick at " + fps + " FPS.");
                    double time = 1000 + (targetTick - 1) * SimTime.FixedTimestepD;
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Check(em, entities[i], orbits[i], time, Origin);
                        var position = em.GetComponentData<UniversePosition>(entities[i]).Value;
                        var local = em.GetComponentData<LocalTransform>(entities[i]).Position;
                        if (first) { baseline[sample, i] = position; localBaseline[sample, i] = local; }
                        else { Assert.AreEqual(baseline[sample, i], position); Assert.AreEqual(localBaseline[sample, i], local); }
                    }
                }
                first = false;
            }
        }
    }
}
