using Game.Data.Definitions;
using Game.Data.Hazards;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace Game.Tests.EditMode.Simulation.Hazards
{
    /// <summary>HAZ-014: mechanism rules fire on their triggers and hand actions to the systems that own the state they change.</summary>
    public sealed class MechanismTests : ZoneFixtureBase
    {
        protected override System.Type[] Systems => new[] { typeof(DamageStreamsSystem), typeof(ZoneSystem), typeof(ImpulseApplySystem), typeof(MechanismSystem) };

        internal static MechanismDefinition Rules(MechanismDefinition def, params (MechanismDefinition.Trigger when, MechanismDefinition.Action[] actions)[] rules)
        {
            var so = new UnityEditor.SerializedObject(def);
            so.FindProperty("id").stringValue = "mechanism_test";
            var list = so.FindProperty("rules"); list.arraySize = rules.Length;
            for (int i = 0; i < rules.Length; i++)
            {
                var r = list.GetArrayElementAtIndex(i); var w = r.FindPropertyRelative("When");
                w.FindPropertyRelative("Kind").enumValueIndex = (int)rules[i].when.Kind; w.FindPropertyRelative("Group").intValue = rules[i].when.Group;
                w.FindPropertyRelative("Value").floatValue = rules[i].when.Value; w.FindPropertyRelative("Offset").floatValue = rules[i].when.Offset;
                var d = r.FindPropertyRelative("Do"); d.arraySize = rules[i].actions.Length;
                for (int k = 0; k < rules[i].actions.Length; k++)
                {
                    var a = d.GetArrayElementAtIndex(k); var src = rules[i].actions[k];
                    a.FindPropertyRelative("Kind").enumValueIndex = (int)src.Kind; a.FindPropertyRelative("Group").intValue = src.Group;
                    a.FindPropertyRelative("Value").floatValue = src.Value; a.FindPropertyRelative("DelaySeconds").floatValue = src.DelaySeconds;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return def;
        }

        [Test] public void Timer_TogglesZone()
        {
            // Every 2 s the timer turns the drag field off, and back on 1 s later.
            var zone = HazardSpawn.Zone(EM, Def(ZoneShape.Circle, new Vector2(100, 0), new ZoneDefinition.Effect { Kind = ZoneEffectKind.Drag, A = .5f }), 0, 0, 3, 1);
            HazardSpawn.Mechanism(EM, Rules(Asset<MechanismDefinition>(), (new MechanismDefinition.Trigger { Kind = MechanismDefinition.TriggerKind.Timer, Value = 2 },
                new[] { new MechanismDefinition.Action { Kind = MechanismDefinition.ActionKind.ZoneDisable },
                        new MechanismDefinition.Action { Kind = MechanismDefinition.ActionKind.ZoneEnable, DelaySeconds = 1 } })), 3);
            var ship = Body(0, 0);
            int wrong = 0, slowed = 0; var trace = new System.Text.StringBuilder();
            for (int i = 0; i < 360; i++)
            {
                uint tick = Tick;
                var v = EM.GetComponentData<PhysicsVelocity>(ship); v.Linear = new float3(0, 0, 10); EM.SetComponentData(ship, v);
                world.StepTicks(1); EM.CompleteAllTrackedJobs();
                bool on = Velocity(ship).y < 10 - 1e-4f; if (on) slowed++;
                // The timer fires on tick 120k; the decider runs after the zones, so the field is off from 120k + 1 to 120k + 60.
                bool expected = !(tick > 120 && (tick - 1) % 120 < 60);
                if (on != expected) { wrong++; if (wrong < 8) trace.Append($"t{tick}:{(on ? 1 : 0)} "); }
            }
            Assert.AreEqual(0, wrong, "the field is off for 1 s after each 2 s timer: " + trace);
            Assert.AreEqual(360 - 120, slowed);
        }
    }

    /// <summary>HAZ-014: with only the decider running, actions are records; the owners' state is untouched.</summary>
    public sealed class MechanismRoutingTests : ZoneFixtureBase
    {
        protected override System.Type[] Systems => new[] { typeof(MechanismSystem) };

        [Test] public void Actions_RouteToOwners()
        {
            // Without the owners running, the decider writes action records and changes nothing itself.
            var mover = EM.CreateEntity(typeof(Mover), typeof(MoverPoint));
            var before = new Mover { Path = MoverPath.Rotate, Rate = 1, Playing = 1, Group = 5 };
            EM.SetComponentData(mover, before);
            var zone = HazardSpawn.Zone(EM, Def(ZoneShape.Circle, new Vector2(10, 0), new ZoneDefinition.Effect { Kind = ZoneEffectKind.Drag, A = .5f }), 0, 0, 5, 1);
            EM.AddComponentData(zone, new ZoneSchedule { Enabled = 1 });
            HazardSpawn.Mechanism(EM, MechanismTests.Rules(Asset<MechanismDefinition>(), (new MechanismDefinition.Trigger { Kind = MechanismDefinition.TriggerKind.Timer, Value = 1 },
                new[] { new MechanismDefinition.Action { Kind = MechanismDefinition.ActionKind.MoverPlay, Value = -1 },
                        new MechanismDefinition.Action { Kind = MechanismDefinition.ActionKind.ZoneDisable } })), 5);
            while (Tick < 60) world.StepTicks(1);
            world.StepTicks(1); EM.CompleteAllTrackedJobs();
            var actions = world.GetSingleton<MechanismActions>();
            Assert.AreEqual(2, actions.Current.Length, "both actions fall due on the timer tick");
            Assert.AreEqual(MechanismActionKind.MoverPlay, actions.Current[0].Kind); Assert.AreEqual(5, actions.Current[0].Group);
            Assert.AreEqual(MechanismActionKind.ZoneDisable, actions.Current[1].Kind);
            Assert.AreEqual(before.Playing, EM.GetComponentData<Mover>(mover).Playing, "MoverSystem, not the decider, applies mover actions");
            Assert.AreEqual(1, EM.GetComponentData<ZoneSchedule>(zone).Enabled, "ZoneSystem, not the decider, applies zone actions");
        }
    }

    /// <summary>HAZ-014: actions keep a mover's pose continuous (no jumps when a door reverses mid-travel or a rail turns back).</summary>
    public sealed class MechanismActionMathTests
    {
        static float3 At(in Mover m, NativeArray<MoverPoint> p, uint tick) { MoverMath.Pose(m, p, tick, out var pos, out _); return pos; }

        [Test] public void Reverse_KeepsPoseContinuous()
        {
            var points = new NativeArray<MoverPoint>(new[] { new MoverPoint { Pose = 0 }, new MoverPoint { Pose = new float3(0, 0, 10) } }, Allocator.Temp);
            foreach (var path in new[] { MoverPath.Toggle, MoverPath.Rail })
            {
                var m = new Mover { Path = path, Rate = .25f, Speed = 2.5f, Playing = 1, StartTick = 100, PingPong = 1 };
                var before = At(m, points, 190);
                MechanismActionMath.Apply(ref m, new MechanismAction { Kind = MechanismActionKind.MoverReverse }, 190);
                Assert.Less(math.distance(before, At(m, points, 190)), 1e-3f, path + " reverses where it is");
                Assert.Less(At(m, points, 250).z, before.z, path + " then runs back");
            }
            var r = new Mover { Path = MoverPath.Rotate, Rate = .5f, Playing = 1, StartTick = 10 };
            MoverMath.Pose(r, points, 400, out _, out float yaw);
            MechanismActionMath.Apply(ref r, new MechanismAction { Kind = MechanismActionKind.MoverReverse }, 400);
            MoverMath.Pose(r, points, 400, out _, out float after);
            Assert.AreEqual(yaw, after, 1e-4f); Assert.AreEqual(-.5f, r.Rate);
            points.Dispose();
        }

        [Test] public void Pause_HoldsAndResumes()
        {
            var points = new NativeArray<MoverPoint>(new[] { new MoverPoint { Pose = 0 }, new MoverPoint { Pose = new float3(100, 0, 0) } }, Allocator.Temp);
            var m = new Mover { Path = MoverPath.Rail, Speed = 6, Playing = 1, StartTick = 0, PingPong = 1 };
            var at = At(m, points, 60);
            MechanismActionMath.Apply(ref m, new MechanismAction { Kind = MechanismActionKind.MoverPlay, Value = -1 }, 60);
            Assert.AreEqual(at, At(m, points, 600), "paused: holds its pose");
            MechanismActionMath.Apply(ref m, new MechanismAction { Kind = MechanismActionKind.MoverPlay, Value = 1 }, 600);
            Assert.AreEqual(at.x, At(m, points, 600).x, 1e-4f); Assert.AreEqual(at.x + 6, At(m, points, 660).x, 1e-3f, "resumes from there");
            MechanismActionMath.Apply(ref m, new MechanismAction { Kind = MechanismActionKind.MoverSpeed, Value = 12 }, 660);
            Assert.AreEqual(at.x + 6, At(m, points, 660).x, 1e-3f); Assert.AreEqual(at.x + 18, At(m, points, 720).x, 1e-2f, "a new speed from where it is");
            points.Dispose();
        }
    }
}
