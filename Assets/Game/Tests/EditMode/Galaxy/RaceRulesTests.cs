using Game.Galaxy.Racing;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>RACE-005: lap rules over a gate graph (SPEC-92 §5.1): 0 → 1 → 2 → 0 with a shortcut 0 → 3 → 2.</summary>
    public sealed class RaceRulesTests : GameTestBase
    {
        EcsTestWorld world;
        Entity gateEntity;
        DynamicBuffer<RaceGate> gates => EM.GetBuffer<RaceGate>(gateEntity);
        EntityManager EM => world.EntityManager;

        [SetUp]
        public void SetUp()
        {
            world = CreateWorld();
            gateEntity = EM.CreateEntity(); EM.AddBuffer<RaceGate>(gateEntity);
            var gates = this.gates;
            gates.Add(new RaceGate { Required = 1, NextMask = (1UL << 1) | (1UL << 3) });   // 0 → 1 or the shortcut 3
            gates.Add(new RaceGate { NextMask = 1UL << 2 });                                // 1 → 2
            gates.Add(new RaceGate { NextMask = 1UL << 0 });                                // 2 → 0
            gates.Add(new RaceGate { NextMask = 1UL << 2 });                                // 3 → 2
        }
        [TearDown] public void TearDown() => DisposeTrackedWorlds();

        static RacerProgress Fresh() => new RacerProgress { LastGate = -1 };
        bool Cross(ref RacerProgress r, int gate, uint tick, byte laps = 3, sbyte dir = 1)
        { uint first = 0; return RaceRules.Cross(ref r, gate, dir, gates, tick, laps, ref first); }

        [Test]
        public void ThreeLaps()
        {
            var r = Fresh();
            Assert.IsFalse(Cross(ref r, 0, 10), "the first crossing of gate 0 starts the race line");
            for (int lap = 1; lap <= 3; lap++)
            {
                Cross(ref r, 1, 100u * (uint)lap); Cross(ref r, 2, 100u * (uint)lap + 50);
                Assert.IsTrue(Cross(ref r, 0, 100u * (uint)lap + 90), "lap " + lap);
            }
            Assert.AreEqual(3, r.Lap);
            Assert.IsTrue((r.Flags & RacerFlags.Finished) != 0);
            Assert.AreEqual(390u, r.FinishTick);
        }

        [Test]
        public void Shortcut_Valid()
        {
            var r = Fresh(); Cross(ref r, 0, 0);
            Cross(ref r, 3, 10); Cross(ref r, 2, 20);
            Assert.IsTrue(Cross(ref r, 0, 30), "0 → 3 → 2 → 0 is a lap");
            Assert.AreEqual(30u, r.LastLapTicks);
        }

        [Test]
        public void WallCut_Rejected()
        {
            var r = Fresh(); Cross(ref r, 0, 0);
            Cross(ref r, 2, 10);   // skipped 1 (or 3): not counted
            Assert.AreEqual(0, r.LastGate);
            Assert.IsFalse(Cross(ref r, 0, 20), "gate 0 does not follow gate 0");
            Assert.AreEqual(0, r.Lap);
        }

        [Test]
        public void WrongWay()
        {
            var r = Fresh(); Cross(ref r, 0, 0); Cross(ref r, 1, 10);
            Cross(ref r, 1, 20, dir: -1);
            Assert.IsTrue((r.Flags & RacerFlags.WrongWay) != 0);
            Cross(ref r, 1, 30);
            Assert.IsTrue((r.Flags & RacerFlags.WrongWay) == 0, "forward over it again clears the warning");
            Assert.AreEqual(1, r.LastGate);
        }

        [Test]
        public void Places_Overtake()
        {
            var e = EM.CreateEntity(); var racers = EM.AddBuffer<RacerProgress>(e);
            var a = Fresh(); a.Slot = 0; Cross(ref a, 0, 0); Cross(ref a, 1, 10);
            var b = Fresh(); b.Slot = 1; Cross(ref b, 0, 0);
            racers.Add(a); racers.Add(b);
            using var d = new NativeArray<float>(new[] { 500f, 50f }, Allocator.Temp);
            RaceRules.Places(racers, d);
            Assert.AreEqual(1, racers[0].Place, "one more gate this lap beats a shorter distance");
            Assert.AreEqual(2, racers[1].Place);
            var bb = racers[1]; Cross(ref bb, 3, 20); racers[1] = bb;
            using var d2 = new NativeArray<float>(new[] { 500f, 50f }, Allocator.Temp);
            RaceRules.Places(racers, d2);
            Assert.AreEqual(1, racers[1].Place, "level on gates: the nearer to its next gate leads");
        }

        [Test]
        public void SameTickFinish_OrderedBySlot()
        {
            var e = EM.CreateEntity(); var racers = EM.AddBuffer<RacerProgress>(e);
            for (byte s = 0; s < 2; s++) racers.Add(new RacerProgress { Slot = s, Flags = RacerFlags.Finished | RacerFlags.Started, FinishTick = 500 });
            using var d = new NativeArray<float>(2, Allocator.Temp);
            RaceRules.Places(racers, d);
            Assert.AreEqual(1, racers[0].Place); Assert.AreEqual(2, racers[1].Place);
        }
    }
}
