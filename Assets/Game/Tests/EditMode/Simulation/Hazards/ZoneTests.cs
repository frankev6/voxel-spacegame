using System.Collections.Generic;
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
    /// <summary>HAZ-006 / HAZ-007: boost strips, slow fields, gates, bounds and field walls (SPEC-91 §5.2). Bodies are moved by hand
    /// each tick (no physics step), so only the zones' impulses change their velocity.</summary>
    public sealed class ZoneTests : GameTestBase
    {
        EcsTestWorld world;
        readonly List<Object> assets = new List<Object>();
        EntityManager EM => world.EntityManager;
        const float Dt = 1f / 60f;

        [SetUp]
        public void SetUp()
        {
            world = CreateWorld(typeof(DamageStreamsSystem), typeof(ZoneSystem), typeof(ImpulseApplySystem));
            world.AddGameplayActive(); world.AddSimClock(1);
            contacts.Clear();
        }
        [TearDown] public void TearDown() { DisposeTrackedWorlds(); foreach (var a in assets) Object.DestroyImmediate(a); assets.Clear(); }

        ZoneDefinition Def(ZoneShape shape, Vector2 half, float radius, params ZoneDefinition.Effect[] effects)
        {
            var def = ScriptableObject.CreateInstance<ZoneDefinition>(); assets.Add(def);
            var so = new UnityEditor.SerializedObject(def);
            so.FindProperty("id").stringValue = "zone_test";
            so.FindProperty("shape").enumValueIndex = (int)shape;
            so.FindProperty("halfExtents").vector2Value = half; so.FindProperty("pathRadius").floatValue = radius;
            var list = so.FindProperty("effects"); list.arraySize = effects.Length;
            for (int i = 0; i < effects.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Kind").enumValueIndex = (int)effects[i].Kind;
                e.FindPropertyRelative("A").floatValue = effects[i].A; e.FindPropertyRelative("B").floatValue = effects[i].B; e.FindPropertyRelative("C").floatValue = effects[i].C;
                e.FindPropertyRelative("Id").intValue = effects[i].Id;
                e.FindPropertyRelative("Flags").intValue = (int)(effects[i].Flags == 0 ? ZoneEffectFlags.AffectsShips | ZoneEffectFlags.AffectsDevices : effects[i].Flags);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return def;
        }
        static ZoneDefinition.Effect Fx(ZoneEffectKind kind, float a = 0, float b = 0, float c = 0, ushort id = 0) => new ZoneDefinition.Effect { Kind = kind, A = a, B = b, C = c, Id = id };

        Entity Body(float2 position, float2 velocity, float mass = 10000)
        {
            var e = EM.CreateEntity(typeof(LocalTransform), typeof(PhysicsVelocity), typeof(PhysicsMass), typeof(VoxelGrid), typeof(ExternalPush));
            EM.SetComponentData(e, LocalTransform.FromPosition(new float3(position.x, 0, position.y)));
            EM.SetComponentData(e, new PhysicsVelocity { Linear = new float3(velocity.x, 0, velocity.y) });
            EM.SetComponentData(e, new PhysicsMass { Transform = RigidTransform.identity, InverseMass = 1 / mass, InverseInertia = new float3(0, 1e-4f, 0) });
            EM.SetComponentData(e, new VoxelGrid { Kind = GridKind.Ship, CellSize = .5f, CellMin = -4, CellMax = 3 });   // 4 m box, radius ~3.5 m
            return e;
        }
        float2 Position(Entity e) => EM.GetComponentData<LocalTransform>(e).Position.xz;
        float2 Velocity(Entity e) => EM.GetComponentData<PhysicsVelocity>(e).Linear.xz;
        readonly List<ZoneContact> contacts = new List<ZoneContact>();
        string Dump() { var sb = new System.Text.StringBuilder(); foreach (var c in contacts) sb.Append($"[{c.Kind} id{c.Id} x{c.Crossed} in{c.Inside} {c.Body}] "); return sb.ToString(); }
        void Step(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
            {
                using (var q = EM.CreateEntityQuery(typeof(LocalTransform), typeof(PhysicsVelocity), typeof(VoxelGrid)))
                using (var es = q.ToEntityArray(Allocator.Temp))
                    foreach (var e in es)
                    {
                        var t = EM.GetComponentData<LocalTransform>(e); t.Position += EM.GetComponentData<PhysicsVelocity>(e).Linear * Dt; EM.SetComponentData(e, t);
                    }
                world.StepTicks(1);
                EM.CompleteAllTrackedJobs();
                foreach (var c in world.GetSingleton<ZoneContacts>().Current) contacts.Add(c);
            }
        }

        [Test]
        public void Boost_Accelerates()
        {
            HazardSpawn.Zone(EM, Def(ZoneShape.Box, new Vector2(30, 400), 0, Fx(ZoneEffectKind.Accelerate, 140, 60)), new float2(0, 0), 0, 1, 1);
            var body = Body(new float2(0, -350), new float2(0, 50));
            Step(60);
            Assert.AreEqual(110, Velocity(body).y, 2, "60 m/s² for one second");
            Step(60);
            Assert.AreEqual(140, Velocity(body).y, .5f, "Capped at the strip's target");
            Assert.AreEqual(0, Velocity(body).x, 1e-3f);
        }

        [Test]
        public void Drag_Slows()
        {
            HazardSpawn.Zone(EM, Def(ZoneShape.Circle, new Vector2(500, 0), 0, Fx(ZoneEffectKind.Drag, .7f, 0)), new float2(0, 0), 0, 1, 1);
            var body = Body(new float2(0, 0), new float2(0, 100));
            Step(60);
            Assert.Less(math.length(Velocity(body)), 50, "Halved within a second");
        }

        [Test]
        public void Gate_FastCrossing_Once()
        {
            var gate = HazardSpawn.Zone(EM, Def(ZoneShape.Box, new Vector2(40, 2), 0, Fx(ZoneEffectKind.Gate, 1)), new float2(0, 100), 0, 1, 7);
            HazardSpawn.SetEffectId(EM, gate, 3);
            var body = Body(new float2(5, 60), new float2(0, 300));
            Step(30);
            Assert.AreEqual(1, contacts.Count, Dump());
            Assert.AreEqual(1, contacts[0].Crossed); Assert.AreEqual(3, contacts[0].Id); Assert.AreEqual(7UL, contacts[0].Zone);
            contacts.Clear();
            EM.SetComponentData(body, new PhysicsVelocity { Linear = new float3(0, 0, -300) });
            Step(30);
            Assert.AreEqual(1, contacts.Count); Assert.AreEqual(-1, contacts[0].Crossed, "Against the gate's forward");
        }

        [Test]
        public void Gate_Hover_NoContact()
        {
            HazardSpawn.Zone(EM, Def(ZoneShape.Box, new Vector2(40, 2), 0, Fx(ZoneEffectKind.Gate, 1)), new float2(0, 100), 0, 1, 7);
            Body(new float2(-30, 100.2f), new float2(20, 0));   // sliding along the line on its forward side
            Body(new float2(80, 50), new float2(0, 60));        // passing wide of the gate's width
            Step(90);
            Assert.AreEqual(0, contacts.Count, Dump());
        }

        [Test]
        public void Gate_JumpAcross_NoContact()
        {
            // A rebase, respawn or portal moves a body tens of metres in one tick; that is not a crossing.
            HazardSpawn.Zone(EM, Def(ZoneShape.Box, new Vector2(40, 2), 0, Fx(ZoneEffectKind.Gate, 1)), new float2(0, 100), 0, 1, 7);
            var body = Body(new float2(0, 60), float2.zero);
            Step(2);
            EM.SetComponentData(body, LocalTransform.FromPosition(new float3(0, 0, 140)));
            Step(2);
            Assert.AreEqual(0, contacts.Count, Dump());
        }

        [Test]
        public void Bounds_ReportInside()
        {
            var square = new[] { new float2(-100, -100), new float2(100, -100), new float2(100, 100), new float2(-100, 100) };
            HazardSpawn.PathZone(EM, Def(ZoneShape.Path, default, 1, Fx(ZoneEffectKind.Bounds, id: 1)), square, 1, 9);
            var inside = Body(new float2(0, 0), float2.zero); var outside = Body(new float2(150, 0), float2.zero);
            Step(1);
            Assert.AreEqual(2, contacts.Count);
            foreach (var c in contacts) Assert.AreEqual(c.Body == inside ? 1 : 0, c.Inside);
        }

        [Test]
        public void FieldWall_Glancing_TurnsAndSlides()
        {
            // A wall along x = 0 (z from −500 to 500); a ship at 80 m/s hits it at 45 degrees from the +x side.
            HazardSpawn.PathZone(EM, Def(ZoneShape.Path, default, 3, Fx(ZoneEffectKind.Repel, 400, 8, 2)), new[] { new float2(0, -500), new float2(0, 500) }, 1, 11);
            var body = Body(new float2(30, 0), new float2(-56.6f, 56.6f));
            float minX = float.MaxValue;
            for (int i = 0; i < 120; i++) { Step(); minX = math.min(minX, Position(body).x); }
            Assert.Greater(minX, 0, "Never crosses the wall");
            Assert.GreaterOrEqual(Velocity(body).x, -1, "Approach speed removed");
            Assert.Greater(Velocity(body).y, 50, "Slides along the wall");
            Assert.AreNotEqual(0f, EM.GetComponentData<PhysicsVelocity>(body).Angular.y, "The contact turned it");
        }

        [Test]
        public void FieldWall_HeadOn_NoTunnelling()
        {
            HazardSpawn.PathZone(EM, Def(ZoneShape.Path, default, 3, Fx(ZoneEffectKind.Repel, 400, 8, 2)), new[] { new float2(0, -500), new float2(0, 500) }, 1, 11);
            var body = Body(new float2(40, 0), new float2(-250, 0));
            for (int i = 0; i < 60; i++) { Step(); Assert.Greater(Position(body).x, -1f, "tick " + i); }
            Assert.Greater(Velocity(body).x, -1);
        }

        [Test]
        public void Deterministic()
        {
            float2 Run()
            {
                HazardSpawn.DespawnGroup(EM, 1);
                HazardSpawn.Zone(EM, Def(ZoneShape.Box, new Vector2(30, 400), 0, Fx(ZoneEffectKind.Accelerate, 140, 60)), new float2(0, 0), .3f, 1, 1);
                HazardSpawn.PathZone(EM, Def(ZoneShape.Path, default, 3, Fx(ZoneEffectKind.Repel, 400, 8, 2)), new[] { new float2(-20, -500), new float2(-20, 500) }, 1, 2);
                var b = Body(new float2(0, -300), new float2(-20, 60));
                Step(120);
                var v = Velocity(b); EM.DestroyEntity(b); return v;
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
