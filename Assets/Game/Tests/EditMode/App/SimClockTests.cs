using System.Collections.Generic;
using Game.App.Session;
using Game.App.Systems;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Entities;

namespace Game.Tests.EditMode.App
{
    /// <summary>FND-007: SimClock advance and write-back (SPEC-01 §5.7, §5.8; SPEC-02 decision 6).</summary>
    public sealed partial class SimClockTests : GameTestBase
    {
        [DisableAutoCreation, UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
        public partial class TickReader : SystemBase
        {
            public readonly List<uint> Seen = new List<uint>();
            protected override void OnUpdate() => Seen.Add(SystemAPI.GetSingleton<SimClock>().Tick);
        }

        [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
        public partial class LateTickReader : SystemBase
        {
            public readonly List<uint> Seen = new List<uint>();
            protected override void OnUpdate() => Seen.Add(SystemAPI.GetSingleton<SimClock>().Tick);
        }

        EcsTestWorld GameplayWorld(bool worldScene, double start)
        {
            var w = CreateWorld(typeof(SimClockAdvanceSystem), typeof(GameplayEndCommandBufferSystem), typeof(TickReader), typeof(LateTickReader));
            var e = w.AddGameplayActive();
            if (worldScene) w.EntityManager.AddComponent<WorldSceneActive>(e);
            w.EntityManager.AddComponentData(e, new SimClock { Tick = 0, UniversalTime = start, SessionStartUniversalTime = start });
            return w;
        }

        [Test]
        public void Advances_Exactly()
        {
            const double start = 123456.75;
            var w = GameplayWorld(true, start);
            w.StepTicks(120);
            var clock = w.GetSingleton<SimClock>();
            Assert.AreEqual(120u, clock.Tick);
            Assert.AreEqual(start + 2.0, clock.UniversalTime, "exactly, not within a tolerance");
            Assert.AreEqual(start, clock.SessionStartUniversalTime);
            w.StepTicks(60 * 60 * 10);                                 // ten minutes: no drift
            Assert.AreEqual(start + 602.0, w.GetSingleton<SimClock>().UniversalTime);
        }

        [Test]
        public void ReadOrder()
        {
            var w = GameplayWorld(true, 0);
            w.StepTicks(5);
            CollectionAssert.AreEqual(new uint[] { 0, 1, 2, 3, 4 }, w.World.GetExistingSystemManaged<TickReader>().Seen, "pre-physics reads N during tick N");
            CollectionAssert.AreEqual(new uint[] { 0, 1, 2, 3, 4 }, w.World.GetExistingSystemManaged<LateTickReader>().Seen, "post-physics systems too: the advance is last");
        }

        [Test]
        public void Paused_NoAdvance()
        {
            var w = GameplayWorld(true, 0);
            w.StepFrame(1f / 60f);
            Assert.AreEqual(1u, w.GetSingleton<SimClock>().Tick);
            for (int i = 0; i < 30; i++) w.StepFrame(0f);              // timeScale = 0: Unity hands the world a zero delta
            Assert.AreEqual(1u, w.GetSingleton<SimClock>().Tick, "no tick while paused");
            w.EntityManager.DestroyEntity(w.EntityManager.CreateEntityQuery(typeof(GameplayActive)));
            w.AddSingleton(new SimClock { Tick = 7 });
            w.StepTicks(3);
            Assert.AreEqual(7u, w.GetSingleton<SimClock>().Tick, "no GameplayActive (menus): no advance");
        }

        [Test]
        public void Lab_DoesNotWriteBack()
        {
            double session = 500;
            var lab = GameplayWorld(false, 0);
            lab.StepTicks(90);
            new SimClockWriteback(lab.World, t => session = t).WriteBackFromWorld();
            Assert.AreEqual(500, session, "the Lab clock never reaches the campaign");

            var world = GameplayWorld(true, 500);
            world.StepTicks(90);
            new SimClockWriteback(world.World, t => session = t).WriteBackFromWorld();
            Assert.AreEqual(501.5, session, "the World clock is written back");
            Assert.AreEqual(0, new SimClockWriteback(world.World, t => { }).WritebackOrder);
        }
    }
}
