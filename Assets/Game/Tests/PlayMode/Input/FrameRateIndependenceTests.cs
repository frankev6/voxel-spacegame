using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Core.Settings;
using Game.Input;
using Game.Presentation.Input;
using Game.Simulation.Commands;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Input
{
    internal struct InputTickRecord : IBufferElementData
    {
        public ShipCommand Command;
        public float Seconds;
    }
    [DisableAutoCreation, UpdateInGroup(typeof(ShipCommandGroup)), UpdateAfter(typeof(PlayerCommandApplySystem))]
    internal partial struct InputTickRecorderSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<GameplayActive>();
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (command, history) in SystemAPI.Query<RefRO<ShipCommand>, DynamicBuffer<InputTickRecord>>().WithAll<PlayerShip>())
                history.Add(new InputTickRecord { Command = command.ValueRO, Seconds = SystemAPI.Time.DeltaTime });
        }
    }

    /// <summary>INP-007: real per-frame device sampling followed by the production fixed-step command consumer.</summary>
    public sealed class FrameRateIndependenceTests
    {
        struct Result
        {
            public int Fps, Interact, Ability1, ThrustTicks, ZeroTickFrames, MultiTickFrames;
            public float ThrustSeconds;
        }
        InputSettings.UpdateMode previousUpdateMode;
        float previousCaptureDelta, previousTimeScale;
        Keyboard keyboard;
        Mouse mouse;
        InputService input;
        Result[] results;
        [SetUp] public void SetUp()
        {
            previousUpdateMode = InputSystem.settings.updateMode;
            previousCaptureDelta = Time.captureDeltaTime; previousTimeScale = Time.timeScale;
            Time.timeScale = 1;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            keyboard = InputSystem.AddDevice<Keyboard>("FrameRateKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("FrameRateMouse");
            input = new InputService(new ControlSettingsData());
            input.Controls.asset.devices = new InputDevice[] { keyboard, mouse };
            input.SetContext(InputContext.Flight);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            input?.Dispose();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.updateMode = previousUpdateMode;
            Time.captureDeltaTime = previousCaptureDelta; Time.timeScale = previousTimeScale;
            yield return null;
        }
        static bool Pulse(double time, double start) => time >= start && time < start + .05;
        IEnumerator RunRates()
        {
            int[] rates = { 30, 60, 144 }; results = new Result[rates.Length];
            for (int r = 0; r < rates.Length; r++)
            {
                int fps = rates[r]; Time.captureDeltaTime = 1f / fps;
                using var world = EcsTestWorld.Create(typeof(PlayerCommandApplySystem), typeof(InputTickRecorderSystem));
                world.AddGameplayActive();
                var em = world.EntityManager;
                var ship = em.CreateEntity(typeof(PlayerShip), typeof(ShipCommand));
                var history = em.AddBuffer<InputTickRecord>(ship); history.Capacity = 128;
                // Keep the sampler outside the helper's root groups so it runs exactly once, before StepFrame's fixed ticks.
                var sampler = world.World.GetOrCreateSystemManaged<PlayerInputSampleSystem>(); sampler.Bind(input, null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); sampler.Update();
                var result = new Result { Fps = fps };
                var held = new List<Key>(3);
                for (int frame = 0; frame < fps * 2; frame++)
                {
                    yield return null;
                    Assert.AreEqual(1f / fps, Time.deltaTime, 1e-6f, "captureDeltaTime controls scaled frame time");
                    double time = (double)frame / fps; held.Clear();
                    if (time >= .5 && time < 1.5) held.Add(Key.W);
                    // The first one-frame pulse reaches a zero-tick frame at 144 FPS and must survive until consumed.
                    if (frame == 0 || Pulse(time, .25) || Pulse(time, .75) || Pulse(time, 1.25)) held.Add(Key.F);
                    if (Pulse(time, .1) || Pulse(time, .9) || Pulse(time, 1.7)) held.Add(Key.Digit1);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(held.ToArray())); InputSystem.Update();
                    sampler.Update();
                    uint before = world.TicksRun; world.StepFrame(Time.deltaTime);
                    uint ticks = world.TicksRun - before;
                    if (ticks == 0) result.ZeroTickFrames++;
                    if (ticks > 1) result.MultiTickFrames++;
                }
                foreach (var record in em.GetBuffer<InputTickRecord>(ship))
                {
                    Assert.AreEqual(EcsTestWorld.TickSeconds, record.Seconds, 1e-7f);
                    if ((record.Command.Edges & ShipCommandEdges.Interact) != 0) result.Interact++;
                    if ((record.Command.Edges & ShipCommandEdges.Ability1) != 0) result.Ability1++;
                    if (record.Command.Thrust.y != 0) result.ThrustTicks++;
                    result.ThrustSeconds += record.Command.Thrust.y * record.Seconds;
                }
                results[r] = result;
                TestContext.WriteLine($"{fps} FPS: {world.TicksRun} fixed ticks, {result.Interact} Interact, {result.Ability1} Ability1, {result.ThrustTicks} thrust ticks / {result.ThrustSeconds:F6} s; zero-tick frames {result.ZeroTickFrames}, multi-tick frames {result.MultiTickFrames}");
                sampler.Unbind();
            }
        }
        [UnityTest] public IEnumerator Edges_SameAt30_60_144()
        {
            yield return RunRates();
            foreach (var result in results)
            {
                Assert.AreEqual(4, result.Interact, result.Fps + " FPS Interact");
                Assert.AreEqual(3, result.Ability1, result.Fps + " FPS Ability1");
            }
            Assert.Greater(results[0].MultiTickFrames, 0, "30 FPS exercises multiple fixed ticks per input sample");
            Assert.Greater(results[2].ZeroTickFrames, 0, "144 FPS exercises retained edges before a fixed tick");
        }
        [UnityTest] public IEnumerator Thrust_TickCountsMatch()
        {
            yield return RunRates();
            foreach (var result in results)
            {
                Assert.AreEqual(60, result.ThrustTicks, 1, result.Fps + " FPS, one-second held input");
                Assert.AreEqual(results[0].ThrustTicks, result.ThrustTicks, 1);
                Assert.AreEqual(1, result.ThrustSeconds, EcsTestWorld.TickSeconds + 1e-5f);
                Assert.AreEqual(results[0].ThrustSeconds, result.ThrustSeconds, EcsTestWorld.TickSeconds + 1e-5f);
            }
        }
    }
}
