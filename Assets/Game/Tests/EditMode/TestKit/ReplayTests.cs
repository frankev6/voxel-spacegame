#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using Game.App.Systems;
using Game.App.Testing;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Presentation.Input;
using Game.Simulation.Commands;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.TestKit
{
    internal struct ReplayTickRecord : IBufferElementData { public ShipCommand Command; }
    [DisableAutoCreation, UpdateInGroup(typeof(ShipCommandGroup)), UpdateAfter(typeof(PlayerCommandApplySystem))]
    internal partial struct ReplayTickCaptureSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<GameplayActive>();
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (command, trace) in SystemAPI.Query<RefRO<ShipCommand>, DynamicBuffer<ReplayTickRecord>>().WithAll<PlayerShip>())
                trace.Add(new ReplayTickRecord { Command = command.ValueRO });
        }
    }
    /// <summary>SPEC-71 TST-002: real frame-stage hooks feeding the production fixed-step command consumer.</summary>
    public sealed class ReplayTests : GameTestBase
    {
        EcsTestWorld World(out Entity ship)
        {
            var w = CreateWorld(typeof(PlayerInputSampleSystem), typeof(PlayerCommandReplaySystem), typeof(PlayerCommandRecordSystem),
                typeof(PlayerCommandApplySystem), typeof(SimClockAdvanceSystem), typeof(ReplayTickCaptureSystem));
            w.AddGameplayActive(); w.AddSimClock();
            ship = w.EntityManager.CreateEntity(typeof(PlayerShip), typeof(ShipCommand));
            var trace = w.EntityManager.AddBuffer<ReplayTickRecord>(ship); trace.Capacity = 128;
            return w;
        }
        static PlayerCommandScript FlightScript()
        {
            var script = new PlayerCommandScript();
            var command = new PlayerCommand { Thrust = new float2(.25f, .75f), Turn = -.25f, AimValid = 1,
                AimWorld = new float3(300, 2, -100), FireGroups = 1, Flags = ShipCommandFlags.Boost };
            script.Set(0, command);
            command.Edges.Interact = 1; command.FireGroups = 7; script.Set(20, command);
            command.Thrust = float2.zero; command.Turn = .5f; command.Flags = ShipCommandFlags.Brake; script.Set(60, command);
            command.Turn = 0; command.Flags = 0; command.FireGroups = 0; script.Set(100, command);
            script.Set(119, command); // Extend the hold to tick 120 without adding an odd-tick change.
            return script;
        }
        ShipCommand[] Replay(int fps, PlayerCommandScript script)
        {
            using var w = World(out var ship);
            var replay = w.World.GetExistingSystemManaged<PlayerCommandReplaySystem>(); replay.Play(script);
            for (int frame = 0; frame < fps * 2; frame++) w.StepFrame(1f / fps);
            Assert.AreEqual(120, w.TicksRun, fps + " FPS fixed tick count");
            var trace = w.EntityManager.GetBuffer<ReplayTickRecord>(ship).ToNativeArray(Unity.Collections.Allocator.Temp);
            try { return trace.Select(r => r.Command).ToArray(); }
            finally { trace.Dispose(); }
        }
        [Test] public void Text_RoundTrip()
        {
            using var w = World(out _);
            w.World.GetExistingSystemManaged<PlayerInputSampleSystem>().Enabled = false;
            var recorder = new PlayerCommandRecorder(); recorder.Start(w.World);
            var command = new PlayerCommand
            {
                Thrust = new float2(math.asfloat(0x80000000u), math.asfloat(0x3F000001u)), Turn = -.000125f,
                AimWorld = new float3(1e20f, math.asfloat(1u), .12345678f), AimValid = 1, Flags = ShipCommandFlags.Boost,
                FireGroups = 0x80000001u, FrameIndex = 999,
                Edges = new PlayerEdgeCounters { Interact = 1, Ability1 = 2, Ability2 = 3, Ability3 = 4, Ability4 = 5,
                    TargetNext = 6, TargetUnderCursor = 7, TargetClear = 8, AutofireToggle = 9, Cruise = 255 }
            };
            using var query = w.EntityManager.CreateEntityQuery(typeof(PlayerCommand));
            query.SetSingleton(command); w.StepFrame(1f / 60);
            w.StepFrame(1f / 60);
            command.Turn = .2f; command.FrameIndex++; query.SetSingleton(command); w.StepFrame(1f / 60);
            var script = recorder.Stop(); Assert.IsFalse(recorder.IsRecording);
            Assert.AreEqual(0, script.StartTick); Assert.AreEqual(3, script.EndTick); Assert.IsTrue(script.ChangesOnlyAtEvenTicks);
            string text = script.ToText().Replace("end 3\n", "end 3\nseed 0123456789ABCDEF\ncontent FEDCBA9876543210\n");
            var parsed = PlayerCommandScript.Parse(text); var roundTrip = PlayerCommandScript.Parse(parsed.ToText());
            Assert.AreEqual(parsed.ToText(), roundTrip.ToText()); StringAssert.Contains("seed 0123456789ABCDEF", roundTrip.ToText());
            StringAssert.Contains("content FEDCBA9876543210", roundTrip.ToText());
            for (uint tick = 0; tick < script.EndTick; tick++) SameFields(script.At(tick), roundTrip.At(tick));
            string path = Path.Combine(Path.GetTempPath(), "command-replay-" + Guid.NewGuid().ToString("N") + ".cmdrec");
            try { parsed.Save(path); Assert.AreEqual(parsed.ToText(), PlayerCommandScript.Load(path).ToText()); }
            finally { if (File.Exists(path)) File.Delete(path); }
            var odd = new PlayerCommandScript(); odd.Set(0, default); odd.Set(1, command); Assert.IsFalse(odd.ChangesOnlyAtEvenTicks);
        }
        static void SameFields(PlayerCommand a, PlayerCommand b)
        {
            Assert.AreEqual(math.asuint(a.Thrust), math.asuint(b.Thrust)); Assert.AreEqual(math.asuint(a.Turn), math.asuint(b.Turn));
            Assert.AreEqual(math.asuint(a.AimWorld), math.asuint(b.AimWorld)); Assert.AreEqual(a.AimValid, b.AimValid);
            Assert.AreEqual(a.Flags, b.Flags); Assert.AreEqual(a.FireGroups, b.FireGroups); Assert.AreEqual(a.Edges, b.Edges);
            Assert.AreEqual(0, b.FrameIndex, "FrameIndex is intentionally not serialized");
        }
        [Test] public void Replay_SameShipCommands_AllFrameRates()
        {
            var script = FlightScript(); Assert.IsTrue(script.ChangesOnlyAtEvenTicks);
            var at30 = Replay(30, script); var at60 = Replay(60, script); var at144 = Replay(144, script);
            Assert.AreEqual(120, at30.Length); CollectionAssert.AreEqual(at30, at60); CollectionAssert.AreEqual(at30, at144);
            Assert.AreEqual(new float2(.25f, .75f), at30[0].Thrust); Assert.AreEqual(float2.zero, at30[60].Thrust);
            Assert.AreEqual(7, at30[20].FireGroups);
        }
        [Test] public void Edge_FiresOnce()
        {
            foreach (int fps in new[] { 30, 60, 144 })
                Assert.AreEqual(1, Replay(fps, FlightScript()).Count(c => (c.Edges & ShipCommandEdges.Interact) != 0), fps + " FPS Interact");
        }
        [Test] public void StopsAtEnd()
        {
            using var w = World(out _); var replay = w.World.GetExistingSystemManaged<PlayerCommandReplaySystem>();
            replay.Play(FlightScript()); w.StepTicks(120); w.UpdateManaged<PlayerCommandReplaySystem>();
            Assert.IsTrue(replay.Finished); Assert.IsFalse(replay.IsPlaying);
            using var q = w.EntityManager.CreateEntityQuery(typeof(PlayerCommand));
            var live = new PlayerCommand { Turn = -.75f }; q.SetSingleton(live); w.UpdateManaged<PlayerCommandReplaySystem>();
            Assert.AreEqual(live.Turn, q.GetSingleton<PlayerCommand>().Turn, "Replay no longer overwrites live commands");
        }
        [Test] public void Parse_ErrorLine()
        {
            const string record = "0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0";
            var malformed = Assert.Throws<FormatException>(() => PlayerCommandScript.Parse("cmdrec 1\nstart 0\nend 2\n0 bad\n"));
            StringAssert.Contains("Line 4:", malformed.Message);
            var order = Assert.Throws<FormatException>(() => PlayerCommandScript.Parse("cmdrec 1\nstart 0\nend 2\n" + record + "\n" + record));
            StringAssert.Contains("Line 5:", order.Message);
        }
    }
}
#endif
