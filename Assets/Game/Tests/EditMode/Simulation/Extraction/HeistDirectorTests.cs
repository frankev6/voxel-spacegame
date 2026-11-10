using Game.Simulation.Extraction;
using Game.Simulation.Mechanisms;
using Game.Tests.EditMode.Simulation.Hazards;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Extraction
{
    /// <summary>EXT-006 (SPEC-93 decision 8): the alarm timeline, its causes and the outcomes.</summary>
    public sealed class HeistDirectorTests : ZoneFixtureBase
    {
        protected override System.Type[] Systems => new[] { typeof(DamageStreamsSystem), typeof(TowSystem), typeof(CoreTetherSystem),
            typeof(WatchSystem), typeof(HeistDirector), typeof(MechanismSystem), typeof(ImpulseApplySystem), typeof(CoreHoldSystem) };
        const byte Alliance = 4;
        Entity place, core, tether, crew;

        void Build()
        {
            var holder = EM.CreateEntity(typeof(LocalTransform)); EM.SetComponentData(holder, LocalTransform.FromPosition(float3.zero));
            core = Body(new float2(0, 0), float2.zero, 100000);
            EM.AddComponentData(core, new StationCore { Holder = holder, HoldRotation = quaternion.identity, Tethers = 1, MassT = 100 });
            tether = EM.CreateEntity(typeof(CoreTether)); EM.SetComponentData(tether, new CoreTether { Core = core, Holder = holder, District = 1 });
            place = EM.CreateEntity(typeof(HeistState), typeof(AlarmStep));
            EM.SetComponentData(place, new HeistState { Faction = Alliance, Core = core, EscapeRadius = 500 });
            var steps = EM.GetBuffer<AlarmStep>(place);
            steps.Add(new AlarmStep { AtSeconds = 0, Kind = AlarmStepKind.Message });
            steps.Add(new AlarmStep { AtSeconds = 10, Kind = AlarmStepKind.Mechanism, Action = MechanismActionKind.MoverPlay, Group = 7 });
            steps.Add(new AlarmStep { AtSeconds = 30, Kind = AlarmStepKind.Wave, Group = 2 });
            crew = Body(new float2(0, 300), float2.zero);
            TransponderApi.Set(EM, crew, true, AccessCode.Trader);
            Step();
        }

        HeistState H => EM.GetComponentData<HeistState>(place);
        int Signals(AlarmStepKind kind)
        {
            int n = 0; foreach (var s in world.GetSingleton<HeistSignals>().Pending) if (s.Kind == kind && s.Index != 255) n++; return n;
        }

        [Test] public void Calm_UntilTheCoreIsFreed_ThenStepsFireOnceEach()
        {
            Build();
            Step(60);
            Assert.That(H.Phase, Is.EqualTo(HeistPhase.Calm));
            world.GetSingleton<TetherCutRequests>().Pending.Add(tether); Step();
            Assert.That(H.Phase, Is.EqualTo(HeistPhase.Alarm)); Assert.That(H.Cause, Is.EqualTo(AlarmCause.CoreFreed));
            Assert.That(Signals(AlarmStepKind.Message), Is.EqualTo(1), "the 0 s step fires at once");
            bool door = false;
            for (int i = 0; i < 60 * 11; i++)
            {
                Step();
                var a = world.GetSingleton<MechanismActions>();
                foreach (var act in a.Current) door |= act.Kind == MechanismActionKind.MoverPlay && act.Group == 7;
            }
            Assert.That(door, Is.True, "the doors close at 10 s");
            Assert.That(Signals(AlarmStepKind.Wave), Is.EqualTo(0));
            Step(60 * 20);
            Assert.That(Signals(AlarmStepKind.Wave), Is.EqualTo(1), "the wave at 30 s, once");
            Assert.That(H.Phase, Is.EqualTo(HeistPhase.Lockdown));
            Step(60);
            Assert.That(Signals(AlarmStepKind.Wave), Is.EqualTo(1));
        }

        [Test] public void Identified_StartsTheAlarmEarly()
        {
            Build();
            EM.AddComponentData(crew, new Wanted { FactionMask = 1ul << Alliance });
            Step();
            Assert.That(H.Phase, Is.EqualTo(HeistPhase.Alarm)); Assert.That(H.Cause, Is.EqualTo(AlarmCause.Identified));
        }

        [Test] public void CorePastTheEscapeRadius_Escaped()
        {
            Build();
            world.GetSingleton<TetherCutRequests>().Pending.Add(tether); Step();
            var p = EM.GetComponentData<LocalTransform>(core); p.Position = new float3(0, 0, 600); EM.SetComponentData(core, p);
            Step();
            Assert.That(H.Phase, Is.EqualTo(HeistPhase.Over)); Assert.That(H.Outcome, Is.EqualTo(HeistOutcome.Escaped));
        }

        [Test] public void CrewGone_Caught()
        {
            Build();
            world.GetSingleton<TetherCutRequests>().Pending.Add(tether); Step();
            EM.AddComponent<Hulk>(crew); Step();
            Assert.That(H.Outcome, Is.EqualTo(HeistOutcome.Caught));
        }
    }
}
