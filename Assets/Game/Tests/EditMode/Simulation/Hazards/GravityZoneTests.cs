using System.Collections.Generic;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Data.Hazards;
using Game.Simulation.Hazards;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Game.Tests.EditMode.Simulation.Hazards
{
    /// <summary>HAZ-015 fixture: ZoneSystem with bodies moved by hand each tick and the clock advanced, so only zone impulses act.</summary>
    public abstract class ZoneFixtureBase : GameTestBase
    {
        protected EcsTestWorld world;
        readonly List<Object> assets = new List<Object>();
        protected EntityManager EM => world.EntityManager;
        protected const float Dt = 1f / 60f;

        [SetUp] public void SetUp() { world = CreateWorld(typeof(DamageStreamsSystem), typeof(ZoneSystem), typeof(ImpulseApplySystem)); world.AddGameplayActive(); world.AddSimClock(1); }
        [TearDown] public void TearDown() { DisposeTrackedWorlds(); foreach (var a in assets) Object.DestroyImmediate(a); assets.Clear(); }

        protected ZoneDefinition Def(ZoneShape shape, Vector2 half, params ZoneDefinition.Effect[] effects)
        {
            var def = ScriptableObject.CreateInstance<ZoneDefinition>(); assets.Add(def);
            var so = new UnityEditor.SerializedObject(def);
            so.FindProperty("id").stringValue = "zone_test"; so.FindProperty("shape").enumValueIndex = (int)shape; so.FindProperty("halfExtents").vector2Value = half;
            var list = so.FindProperty("effects"); list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Kind").enumValueIndex = (int)effects[i].Kind;
                e.FindPropertyRelative("A").floatValue = effects[i].A; e.FindPropertyRelative("B").floatValue = effects[i].B;
                e.FindPropertyRelative("Flags").intValue = (int)(ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return def;
        }

        protected Entity Body(float2 position, float2 velocity, float mass = 10000)
        {
            var e = EM.CreateEntity(typeof(LocalTransform), typeof(PhysicsVelocity), typeof(PhysicsMass), typeof(VoxelGrid), typeof(ExternalPush));
            EM.SetComponentData(e, LocalTransform.FromPosition(new float3(position.x, 0, position.y)));
            EM.SetComponentData(e, new PhysicsVelocity { Linear = new float3(velocity.x, 0, velocity.y) });
            EM.SetComponentData(e, new PhysicsMass { Transform = RigidTransform.identity, InverseMass = 1 / mass, InverseInertia = new float3(0, 1e-4f, 0) });
            EM.SetComponentData(e, new VoxelGrid { Kind = GridKind.Ship, CellSize = .5f, CellMin = -4, CellMax = 3 });
            return e;
        }
        protected float2 Position(Entity e) => EM.GetComponentData<LocalTransform>(e).Position.xz;
        protected float2 Velocity(Entity e) => EM.GetComponentData<PhysicsVelocity>(e).Linear.xz;
        protected uint Tick => world.GetSingleton<SimClock>().Tick;

        protected void Step(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
            {
                using (var q = EM.CreateEntityQuery(typeof(LocalTransform), typeof(PhysicsVelocity), typeof(VoxelGrid)))
                using (var es = q.ToEntityArray(Allocator.Temp))
                    foreach (var e in es)
                    { var t = EM.GetComponentData<LocalTransform>(e); t.Position += EM.GetComponentData<PhysicsVelocity>(e).Linear * Dt; EM.SetComponentData(e, t); }
                world.StepTicks(1); EM.CompleteAllTrackedJobs();
                using var clocks = EM.CreateEntityQuery(typeof(SimClock));
                var clock = clocks.GetSingleton<SimClock>(); clock.Tick++; clocks.SetSingleton(clock);
            }
        }
    }

    /// <summary>HAZ-015: a gravity well (Attract) bends a coasting ship's path round it (ADR-0119 decision 4).</summary>
    public sealed class GravityZoneTests : ZoneFixtureBase
    {
        [Test] public void Attract_CurvesPath()
        {
            // The moon well: GM 720 000, 450 m reach. A ship coasting past at 200 m with the circular speed (60 m/s) stays near 200 m.
            HazardSpawn.Zone(EM, Def(ZoneShape.Circle, new Vector2(450, 0), new ZoneDefinition.Effect { Kind = ZoneEffectKind.Attract, A = 720000, B = 40 }), 0, 0, 0, 1);
            var ship = Body(new float2(200, 0), new float2(0, 60));
            float least = float.MaxValue, most = 0; float2 start = Position(ship);
            for (int i = 0; i < 600; i++)   // 10 s: about half an orbit (period 21 s)
            {
                Step(); float r = math.length(Position(ship));
                least = math.min(least, r); most = math.max(most, r);
            }
            float turned = math.degrees(math.abs(math.atan2(Velocity(ship).x, Velocity(ship).y)));
            TestContext.WriteLine($"Orbit: radius {least:F1}..{most:F1} m, heading turned {turned:F0}°, at {Position(ship)}");
            Assert.Greater(turned, 120, "the coasting ship curves round the well");
            Assert.AreEqual(200, least, 20); Assert.AreEqual(200, most, 20);
            Assert.AreEqual(60, math.length(Velocity(ship)), 5, "an orbit keeps its speed");
        }

        [Test] public void Attract_NothingOutsideItsReach()
        {
            HazardSpawn.Zone(EM, Def(ZoneShape.Circle, new Vector2(450, 0), new ZoneDefinition.Effect { Kind = ZoneEffectKind.Attract, A = 720000, B = 40 }), 0, 0, 0, 1);
            var ship = Body(new float2(500, 0), new float2(0, 60));
            Step(60);
            Assert.AreEqual(0, Velocity(ship).x, 1e-6f);
        }
    }

    /// <summary>HAZ-015: ZoneSchedule switches any zone on for exactly OnTicks of each period.</summary>
    public sealed class ZoneScheduleTests : ZoneFixtureBase
    {
        [Test] public void OnOff_TickExact()
        {
            // A drag field on 30 of every 90 ticks; a body that stays inside counts the ticks it was slowed.
            var zone = HazardSpawn.Zone(EM, Def(ZoneShape.Circle, new Vector2(100, 0), new ZoneDefinition.Effect { Kind = ZoneEffectKind.Drag, A = .5f, B = 0 }), 0, 0, 0, 1);
            EM.AddComponentData(zone, new ZoneSchedule { PeriodTicks = 90, OnTicks = 30, PhaseTicks = 10, Enabled = 1 });
            var ship = Body(new float2(0, 0), new float2(0, 0));
            int slowed = 0, mismatched = 0;
            for (int i = 0; i < 270; i++)
            {
                uint tick = Tick;
                var v = EM.GetComponentData<PhysicsVelocity>(ship); v.Linear = new float3(0, 0, 10); EM.SetComponentData(ship, v);
                var t = EM.GetComponentData<LocalTransform>(ship); t.Position = 0; EM.SetComponentData(ship, t);
                world.StepTicks(1); EM.CompleteAllTrackedJobs();
                bool on = Velocity(ship).y < 10 - 1e-4f; if (on) slowed++;
                if (on != ((tick + 10) % 90 < 30)) mismatched++;
                using var clocks = EM.CreateEntityQuery(typeof(SimClock));
                var clock = clocks.GetSingleton<SimClock>(); clock.Tick++; clocks.SetSingleton(clock);
            }
            Assert.AreEqual(90, slowed, "on for exactly 30 ticks in each of three periods");
            Assert.AreEqual(0, mismatched, "on exactly on its scheduled ticks");
            var s = EM.GetComponentData<ZoneSchedule>(zone); s.Enabled = 0; EM.SetComponentData(zone, s);
            var v2 = EM.GetComponentData<PhysicsVelocity>(ship); v2.Linear = new float3(0, 0, 10); EM.SetComponentData(ship, v2);
            Step(90);
            Assert.AreEqual(10, Velocity(ship).y, 1e-4f, "a disabled zone does nothing");
        }
    }
}
