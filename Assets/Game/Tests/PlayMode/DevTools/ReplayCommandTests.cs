using System;
using System.Collections;
using System.IO;
using Game.App.DevRuntime;
using Game.App.Testing;
using Game.Core.Components;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.DevTools
{
    /// <summary>DEV-016: real boot, imported Fighter, command front-end, input sampling and physics.</summary>
    public sealed class ReplayCommandTests : GameFlowFixture
    {
        DevCommandResult Command(string line)
        {
            var host = Service<DevRuntimeHost>(); var result = host.Commands.Execute(line, host.Context);
            Assert.AreNotEqual(DevResultKind.Failed, result.Kind, line + ": " + result.Text);
            return result;
        }
        IEnumerator StartGame()
        {
            var result = Command("newgame seed=7"); Assert.AreEqual(DevResultKind.Pending, result.Kind);
            var host = Service<DevRuntimeHost>(); Assert.IsTrue(host.TryGetOperation(result.OperationId, out var operation));
            yield return WaitUntil(() => operation.Status != DevOperationStatus.Running, 60, "newgame seed=7");
            Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status, operation.Message);
        }
        float3 Position => Em.GetComponentData<LocalTransform>(PlayerRoot).Position;
        uint Tick { get { using var q = Em.CreateEntityQuery(typeof(SimClock)); return q.GetSingleton<SimClock>().Tick; } }
        [UnityTest]
        public IEnumerator RecordReplay_SamePosition()
        {
            string name = "dev016_test_" + Guid.NewGuid().ToString("N");
            string path = Path.GetFullPath(Path.Combine("TestData", "Replays", name + ".pcr"));
            try
            {
                yield return StartGame(); float3 recordedPosition, initial;
                using (var frames = new FlightFrames())
                {
                    Command("key keys=W,D,Q release=true"); frames.Step(0); initial = Position;
                    Command("key keys=W hold=0"); frames.Step(0);
                    Command("record start"); uint start = Tick;
                    for (int i = 0; i < 300; i++)
                    {
                        if (i == 100) Command("key keys=D hold=0");
                        if (i == 200) { Command("key keys=D release=true"); Command("key keys=Q hold=0"); }
                        frames.Step(1);
                    }
                    Assert.AreEqual(start + 300, Tick);
                    Command("record stop"); Command("record save name=" + name);
                    recordedPosition = Position;
                    Command("key keys=W,D,Q release=true"); frames.Step(0);
                }
                Assert.IsTrue(File.Exists(path)); var saved = PlayerCommandScript.Load(path);
                Assert.AreEqual(300, saved.EndTick - saved.StartTick);
                Assert.Greater(math.distance(initial, recordedPosition), 10, "Real controls must move the ship");
                yield return StartGame();
                using (var frames = new FlightFrames())
                {
                    frames.Step(0); Assert.LessOrEqual(math.distance(initial, Position), 1e-4, "Same seed starts at the same local pose");
                    var result = Command("replay name=" + name); Assert.AreEqual(DevResultKind.Pending, result.Kind);
                    uint start = Tick;
                    for (int i = 0; i < 300; i++) frames.Step(1);
                    Assert.AreEqual(start + 300, Tick); float3 replayed = Position;
                    // Observe the exclusive-end frame without simulating an extra coast tick.
                    frames.Step(0); var host = Service<DevRuntimeHost>(); host.Update();
                    Assert.IsTrue(host.TryGetOperation(result.OperationId, out var operation));
                    Assert.AreEqual(DevOperationStatus.Succeeded, operation.Status, operation.Message);
                    float error = math.distance(recordedPosition, replayed);
                    TestContext.WriteLine($"Seed 7 / 300 ticks; recorded {recordedPosition}, replayed {replayed}, error {error:R} m; local coordinates (SPEC-70 Q22)");
                    Assert.LessOrEqual(error, 1e-4);
                }
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        // Drive the production world, without replacing any simulation system or spawning a test ship.
        // Exact frame/tick stepping avoids headless frame speed and fixed-step catch-up changing the endpoint.
        sealed class FlightFrames : IDisposable
        {
            readonly World world = World.DefaultGameObjectInjectionWorld;
            readonly FixedStepSimulationSystemGroup fixedGroup;
            readonly IRateManager previous;
            readonly TimeData previousTime;
            readonly bool wasInLoop;
            readonly OneTick rate = new OneTick();
            double elapsed;
            public FlightFrames()
            {
                previousTime = world.Time; elapsed = world.Time.ElapsedTime;
                fixedGroup = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>(); previous = fixedGroup.RateManager;
                wasInLoop = ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(world);
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world); fixedGroup.RateManager = rate;
            }
            public void Step(int ticks)
            {
                InputSystem.Update(); rate.Remaining = ticks;
                float delta = ticks / 60f; elapsed += delta; world.SetTime(new TimeData(elapsed, delta));
                world.GetExistingSystemManaged<InitializationSystemGroup>().Update();
                world.GetExistingSystemManaged<SimulationSystemGroup>().Update();
                world.EntityManager.CompleteAllTrackedJobs();
            }
            public void Dispose()
            {
                fixedGroup.RateManager = previous; world.SetTime(previousTime);
                if (wasInLoop) ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
            }
            unsafe sealed class OneTick : IRateManager
            {
                bool pushed;
                DoubleRewindableAllocators* previousAllocators;
                public int Remaining;
                public float Timestep { get; set; } = 1f / 60;
                public bool ShouldGroupUpdate(ComponentSystemGroup group)
                {
                    if (pushed) { group.World.PopTime(); group.World.RestoreGroupAllocator(previousAllocators); pushed = false; }
                    if (Remaining == 0) return false;
                    Remaining--; group.World.PushTime(new TimeData(group.World.Time.ElapsedTime, Timestep));
                    previousAllocators = group.World.CurrentGroupAllocators; group.World.SetGroupAllocator(group.RateGroupAllocators); pushed = true;
                    return true;
                }
            }
        }
    }
}
