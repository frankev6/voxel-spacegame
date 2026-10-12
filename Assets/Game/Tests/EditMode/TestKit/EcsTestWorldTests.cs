using System;
using Game.Core.Components;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.TestKit
{
    /// <summary>TST-001: EcsTestWorld stepping, SimClock and teardown (SPEC-71 §5.1–§5.2).</summary>
    public sealed partial class EcsTestWorldTests : GameTestBase
    {
        [DisableAutoCreation, UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
        public partial class CountingSystem : SystemBase
        {
            public int Count; public double LastElapsed; public float LastDelta;
            protected override void OnUpdate() { Count++; LastElapsed = SystemAPI.Time.ElapsedTime; LastDelta = SystemAPI.Time.DeltaTime; }
        }

        [Test]
        public void StepTicks_Exact()
        {
            var w = CreateWorld(typeof(CountingSystem));
            var sys = w.World.GetExistingSystemManaged<CountingSystem>();
            w.StepTicks(7);
            Assert.AreEqual(7, sys.Count);
            Assert.AreEqual(7u, w.TicksRun);
            Assert.AreEqual(EcsTestWorld.TickSeconds, sys.LastDelta, 1e-7, "fixed delta inside the group");
            Assert.AreEqual(6 * EcsTestWorld.TickSeconds, sys.LastElapsed, 1e-6, "fixed elapsed time starts at 0");
        }

        [Test]
        public void StepFrame_CatchUpAndCap()
        {
            var w = CreateWorld(typeof(CountingSystem));
            var sys = w.World.GetExistingSystemManaged<CountingSystem>();
            w.StepFrame(0.05f);
            Assert.AreEqual(3, sys.Count, "0.05 s = 3 ticks");
            w.StepFrame(0.2f);
            Assert.AreEqual(3 + 6, sys.Count, "0.2 s is capped at 6 ticks");
            w.StepFrame(0.01f);
            Assert.AreEqual(9, sys.Count, "0.01 s is not a whole tick");
            w.StepFrame(0.01f);
            Assert.AreEqual(10, sys.Count, "the remainder carries over");
            Assert.AreEqual(10u, w.TicksRun);
        }

        [Test]
        public void SimClock_AutoAdvance()
        {
            var w = CreateWorld(typeof(CountingSystem));
            w.AddGameplayActive();
            var clockEntity = w.AddSimClock();
            Assert.IsTrue(w.EntityManager.HasComponent<GameplayActive>(clockEntity), "SimClock sits on the GameplayActive entity");
            w.StepTicks(7);
            var clock = w.GetSingleton<SimClock>();
            Assert.AreEqual(7u, clock.Tick);
            Assert.AreEqual(7 / 60.0, clock.UniversalTime, 1e-12);
        }

        [Test]
        public void StepFrame_RequiresAdvanceSystem()
        {
            var w = CreateWorld(typeof(CountingSystem));
            w.StepFrame(0.05f);                                     // fine without a SimClock
            w.AddSimClock();
            var e = Assert.Throws<InvalidOperationException>(() => w.StepFrame(0.05f));
            StringAssert.Contains("SimClockAdvanceSystem", e.Message);
        }

        [Test]
        public void CreateWorld_DisposedOnTeardown()
        {
            var w = CreateWorld(typeof(CountingSystem));
            var world = w.World;
            var native = w.EntityManager.CreateEntity(typeof(GameplayActive));
            Assert.IsTrue(world.IsCreated && native != Entity.Null);
            DisposeTrackedWorlds();                                 // what GameTestTearDown runs after the test
            Assert.IsFalse(world.IsCreated, "tracked worlds are disposed in teardown");
            Assert.IsNull(w.World);
            w.Dispose();                                            // twice is safe
        }

        [Test]
        public void StepTicks_EmptyWorld_AdvancesTime()
        {
            var w = CreateWorld();
            w.StepTicks(3);
            Assert.AreEqual(3u, w.TicksRun);
            Assert.AreEqual(3 * EcsTestWorld.TickSeconds, w.ElapsedSeconds, 1e-6);
        }

        static readonly object[] ReducedWorlds =
        {
            new object[] { new Type[0] },
            new object[] { new[] { typeof(CountingSystem) } },
            new object[] { new[] { typeof(Game.Core.Systems.GameplayEndCommandBufferSystem) } },
            new object[] { new[] { typeof(Game.Presentation.Systems.ManagedEventDrainSystem) } },
            new object[] { new[] { typeof(Game.Core.Systems.ShipCommandGroup), typeof(Game.Core.Systems.AbilityActivationGroup), typeof(Game.Core.Systems.VoxelRebuildGroup),
                                   typeof(Game.Core.Systems.WorldGroup), typeof(Game.Core.Systems.GameInitGroup) } },
        };

        [TestCaseSource(nameof(ReducedWorlds))]
        public void ReducedWorlds_NoOrderingWarnings(Type[] systems)
        {
            var warnings = new System.Collections.Generic.List<string>();
            void Listen(string message, string stack, UnityEngine.LogType type) { if (type == UnityEngine.LogType.Warning) warnings.Add(message); }
            UnityEngine.Application.logMessageReceived += Listen;
            try
            {
                var w = CreateWorld(systems);
                w.AddGameplayActive();
                w.StepTicks(2);
                w.StepFrame(0.05f);
            }
            finally { UnityEngine.Application.logMessageReceived -= Listen; }
            CollectionAssert.IsEmpty(warnings);
        }

        [Test]
        public void Create_And_Dispose_Within20ms()
        {
            for (int i = 0; i < 3; i++) EcsTestWorld.Create(typeof(CountingSystem)).Dispose();
            var times = new double[11];
            for (int i = 0; i < times.Length; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                EcsTestWorld.Create(typeof(CountingSystem)).Dispose();
                times[i] = sw.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times);
            if (times[5] > 20.0) Game.Tests.Common.Perf.PerfRun.WarnBudget($"EcsTestWorld.Create + Dispose median {times[5]:F4} ms; target <= 20 ms");
        }

        [Test]
        public void StepTicks_Overhead_Within0_1ms()
        {
            var w = CreateWorld(typeof(CountingSystem));
            w.StepTicks(50);
            var times = new double[101];
            for (int i = 0; i < times.Length; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                w.StepTicks(1);
                times[i] = sw.Elapsed.TotalMilliseconds;
            }
            Array.Sort(times);
            UnityEngine.Debug.Log($"EcsTestWorld.StepTicks(1) with one empty system: median {times[50]:F4} ms");
            LogAssertExpectLog("EcsTestWorld.StepTicks(1)");
            if (times[50] > 0.1) Game.Tests.Common.Perf.PerfRun.WarnBudget($"StepTicks(1) overhead median {times[50]:F4} ms; target <= 0.1 ms");
        }

        static void LogAssertExpectLog(string prefix) =>
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Log, new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(prefix)));

        /// <summary>The leak guard, run by hand in the acceptance step: it must FAIL on the leaked array. CheckForLeaks reports and resets
        /// the tracked set, so the second call counts what this test left alive.</summary>
        [Test, Explicit("must fail: proves the leak guard")]
        public void LeakGuard_LeakedNativeArray_Fails()
        {
            UnsafeUtility.CheckForLeaks();
            var leaked = new NativeArray<int>(16, Allocator.Persistent);
            try { Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "native allocations left alive by the test"); }
            finally { leaked.Dispose(); }
        }
    }
}
