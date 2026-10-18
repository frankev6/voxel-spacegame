using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Mechanisms
{
    /// <summary>HAZ-014: a real shot on a Switch block slams its blast door for its time, then the door reopens (ADR-0119 decision 2).</summary>
    [Category("Scenario")]
    public sealed class MechanismTests
    {
        const ushort Group = 7;

        static Entity Structure(CombatFixture f, float3 at, GridMotion motion, System.Action<VoxelStructure> build)
        {
            var source = new VoxelStructure(.5f); build(source);
            return f.Flight.SpawnSource(source, at, motion: motion);
        }
        static void Put(CombatFixture f, VoxelStructure s, string block, int3 p)
            => Assert.AreEqual(PlaceResult.Ok, s.TryPlaceBlock(new BlockPlacement { Block = f.Flight.Index(block), Origin = p }, f.Flight.Content), block);

        static void WaitBodies(CombatFixture f, params Entity[] roots)
        {
            for (int i = 0; i < 90; i++)
            {
                f.Tick(); bool ready = true;
                foreach (var r in roots) ready &= f.Em.HasComponent<GridColliderState>(r) && f.Em.GetComponentData<GridColliderState>(r).Phase == GridColliderPhase.Ready;
                if (ready) return;
            }
            Assert.Fail("structures not ready");
        }

        static void Rules(MechanismDefinition def, (MechanismDefinition.Trigger when, MechanismDefinition.Action[] actions) rule)
        {
            var so = new UnityEditor.SerializedObject(def);
            so.FindProperty("id").stringValue = "mechanism_test";
            var list = so.FindProperty("rules"); list.arraySize = 1;
            var r = list.GetArrayElementAtIndex(0); r.FindPropertyRelative("When").FindPropertyRelative("Kind").enumValueIndex = (int)rule.when.Kind;
            var d = r.FindPropertyRelative("Do"); d.arraySize = rule.actions.Length;
            for (int k = 0; k < rule.actions.Length; k++)
            {
                var a = d.GetArrayElementAtIndex(k);
                a.FindPropertyRelative("Kind").enumValueIndex = (int)rule.actions[k].Kind; a.FindPropertyRelative("Value").floatValue = rule.actions[k].Value;
                a.FindPropertyRelative("DelaySeconds").floatValue = rule.actions[k].DelaySeconds;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test] public void Switch_SlamsDoor()
        {
            using var f = new CombatFixture(true, typeof(MoverSystem), typeof(MechanismSystem));
            // The door: a 6 m slab that slides 8 m across the lane; open at x = 0 (point 0), shut at x = 8 (point 1), 2 s each way.
            var door = Structure(f, new float3(0, 0, 30), GridMotion.Kinematic, s => { for (int x = 0; x < 12; x++) for (int y = -2; y <= 2; y++) Put(f, s, "block_hull", new int3(x, y, 0)); });
            // The switch: a hull plate with a Switch cell on its face, 25 m ahead of the shooter.
            var panel = Structure(f, new float3(0, 0, 0), GridMotion.Static, s =>
            {
                for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++) Put(f, s, "block_hull", new int3(x, y, 1));
                Put(f, s, "block_switch", new int3(0, 0, 0));
            });
            var shooter = f.Ship("weapon_plasma_turret_small", new float3(0.25f, 0, -25));
            WaitBodies(f, door, panel); f.Ready(shooter);
            float3 open = f.Flight.Transform(door).Position, shut = open + new float3(8, 0, 0);
            HazardSpawn.AddMover(f.Em, door, new Mover { Path = MoverPath.Toggle, Rate = .5f, Playing = 1, Reverse = 1, StartTick = 0, Group = Group },
                new[] { new MoverPoint { Pose = open }, new MoverPoint { Pose = shut } });
            f.Em.AddComponentData(panel, new MechanismGroup { Group = Group });
            var def = ScriptableObject.CreateInstance<MechanismDefinition>();
            try
            {
                Rules(def, (new MechanismDefinition.Trigger { Kind = MechanismDefinition.TriggerKind.SwitchDamaged },
                    new[] { new MechanismDefinition.Action { Kind = MechanismDefinition.ActionKind.MoverReverse, Value = -1 },
                            new MechanismDefinition.Action { Kind = MechanismDefinition.ActionKind.MoverReverse, Value = 1, DelaySeconds = 4 } }));
                HazardSpawn.Mechanism(f.Em, def, Group);
            }
            finally { Object.DestroyImmediate(def); }
            f.Tick(30);
            Assert.Less(math.distance(f.Flight.Transform(door).Position, open), .01f, "the door starts open and stays open");

            float3 aim = f.Em.GetComponentData<Unity.Transforms.LocalTransform>(panel).TransformPoint(new float3(.25f, .25f, .25f));
            int hitTick = -1;
            for (int tick = 0; tick < 300 && hitTick < 0; tick++)
            {
                f.Fire(shooter, aim); f.Tick();
                if (math.distance(f.Flight.Transform(door).Position, open) > .01f) hitTick = tick;
            }
            f.Stop(shooter);
            Assert.GreaterOrEqual(hitTick, 0, "a shot on the switch starts the door");
            f.Tick(150);
            Assert.Less(math.distance(f.Flight.Transform(door).Position, shut), .02f, "slammed shut within its 2 s travel");
            f.Tick(80);   // the reopen falls due 4 s (240 ticks) after the hit
            Assert.Less(math.distance(f.Flight.Transform(door).Position, shut), .02f, "held shut for its time");
            f.Tick(170);
            Assert.Less(math.distance(f.Flight.Transform(door).Position, open), .02f, "then it reopens");
        }
    }
}
