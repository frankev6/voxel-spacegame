using System.Collections.Generic;
using Game.Core.Components;
using Game.Data;
using Game.Data.Blocks;
using Game.Simulation.Abilities;
using Game.Simulation.Power;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;

namespace Game.Tests.EditMode.Simulation.Power
{
    /// <summary>Six required PWR-005 scenarios drive production allocation with explicit demand.</summary>
    public sealed class PowerAllocationSystemTests
    {
        EcsTestWorld world;
        EntityManager Em => world.EntityManager;
        [SetUp] public void SetUp()
        {
            world = EcsTestWorld.Create(typeof(PowerAllocationSystem));
            world.AddGameplayActive(); world.AddSimClock(); world.AddSingleton(PowerTuning.Defaults);
            world.AddSingleton(default(BlockDefsRef)); world.AddSingleton(default(BlockModulesRef));
        }
        [TearDown] public void TearDown() => world?.Dispose();
        Entity Root(float stored = 200, float generation = 40)
        {
            var root = Em.CreateEntity(typeof(ShipPower), typeof(PowerStats));
            Em.SetComponentData(root, new ShipPower { Stored = stored, Capacity = 200, Generation = generation, Flags = ShipPowerFlags.Initialized });
            Em.AddBuffer<BlockInstance>(root); Em.AddBuffer<PowerConsumer>(root); Em.AddBuffer<AbilitySlot>(root);
            return root;
        }
        Entity Consumer(Entity root, float draw, byte priority = 100)
        {
            int index = Em.GetBuffer<BlockInstance>(root).Length;
            var entity = Em.CreateEntity(typeof(FunctionalBlock), typeof(PowerLink), typeof(Powered));
            Em.SetComponentData(entity, new FunctionalBlock { Grid = root, InstanceIndex = index });
            Em.SetComponentData(entity, new PowerLink { Bound = 1, ConsumerCount = 1 });
            Em.AddBuffer<PowerRequest>(entity).Add(new PowerRequest { Module = BlockModuleId.WeaponMount, Draw = draw,
                Priority = priority, Requesting = 1, ConsumerIndex = (short)index });
            Em.GetBuffer<BlockInstance>(root).Add(new BlockInstance { Block = 1, Functional = entity });
            Em.GetBuffer<PowerConsumer>(root).Add(new PowerConsumer { Functional = entity, InstanceIndex = index,
                Module = BlockModuleId.WeaponMount, Draw = draw, Priority = priority });
            return entity;
        }
        void Step() { world.StepTicks(1); Em.CompleteAllTrackedJobs(); }
        [Test] public void Brownout_AfterSustainTime()
        {
            var root = Root(); Consumer(root, 46); int first = 0;
            for (int tick = 1; tick <= 2002; tick++)
            { Step(); if ((Em.GetComponentData<ShipPower>(root).Flags & ShipPowerFlags.Brownout) != 0) { first = tick; break; } }
            Assert.That(first, Is.InRange(1999, 2001), "200 / (46 - 40) seconds at 60 Hz; SPEC-22 Q15 corrects rounded 33s.");
        }
        [Test] public void Brownout_HoldsThirtyTicks()
        {
            var root = Root(0); var first = Consumer(root, 20); var last = Consumer(root, 26);
            Step(); uint denied = Em.GetBuffer<PowerConsumer>(root)[1].DeniedUntilTick;
            Assert.That(Em.IsComponentEnabled<Powered>(first), Is.True);
            Assert.That(Em.IsComponentEnabled<Powered>(last), Is.False);
            for (int i = 1; i < 30; i++) { Step(); Assert.That(Em.IsComponentEnabled<Powered>(last), Is.False); }
            Assert.AreEqual(30u, denied);
            Step(); Assert.That(Em.IsComponentEnabled<Powered>(last), Is.True, "Enable-mask query must include previously disabled blocks.");
        }
        [Test] public void Shields_OutlastWeapons()
        {
            var root = Root(0); var shield = Consumer(root, 8, 200); var weapon = Consumer(root, 46);
            for (int i = 0; i < 120; i++)
            {
                Step(); Assert.That(Em.GetBuffer<PowerConsumer>(root)[0].Flags & PowerConsumerFlags.Granted, Is.Not.EqualTo((PowerConsumerFlags)0));
                Assert.That(Em.IsComponentEnabled<Powered>(shield), Is.True);
                if (i == 0) Assert.That(Em.IsComponentEnabled<Powered>(weapon), Is.False);
            }
        }
        [Test] public void AbilityCost_GrantedOrDenied()
        {
            var granted = Root(20, 0); var denied = Root(10, 0); var priority = Root(21, 0);
            foreach (var root in new[] { granted, denied, priority })
                Em.GetBuffer<AbilitySlot>(root).Add(new AbilitySlot { State = AbilitySlotState.PendingPower, PendingCost = 20 });
            Consumer(priority, 60, 200); var weapon = Consumer(priority, 60);
            Step();
            Assert.AreEqual(1u, Em.GetComponentData<ShipPower>(granted).AbilityGrantMask);
            Assert.AreEqual(0u, Em.GetComponentData<ShipPower>(granted).AbilityGrantTick);
            Assert.AreEqual(0u, Em.GetComponentData<ShipPower>(denied).AbilityGrantMask);
            Assert.AreEqual(1u, Em.GetComponentData<ShipPower>(priority).AbilityGrantMask);
            Assert.That(Em.IsComponentEnabled<Powered>(weapon), Is.False, "Shield before ability before weapon.");
        }
        [Test] public void Hulk_ClearsPower()
        {
            var root = Root(); var a = Consumer(root, 8, 200); var b = Consumer(root, 46);
            Step(); Em.AddComponent<Hulk>(root); Step(); var power = Em.GetComponentData<ShipPower>(root);
            Assert.AreEqual(0, power.Stored); Assert.AreEqual(0, power.Generation); Assert.AreEqual(0, power.Capacity);
            Assert.AreEqual(0, power.DemandPerSecond); Assert.AreEqual(0, power.GrantedPerSecond); Assert.AreEqual(0u, power.AbilityGrantMask);
            Assert.That(Em.IsComponentEnabled<Powered>(a), Is.False); Assert.That(Em.IsComponentEnabled<Powered>(b), Is.False);
        }
        [Test] public void Deterministic_AcrossWorkerCounts()
        {
            int original = JobsUtility.JobWorkerCount;
            try { CollectionAssert.AreEqual(Scenario(1), Scenario(8)); }
            finally { JobsUtility.JobWorkerCount = original; }
        }
        List<float> Scenario(int workers)
        {
            world.Dispose(); SetUp(); JobsUtility.JobWorkerCount = workers;
            var roots = new Entity[64];
            for (int i = 0; i < roots.Length; i++) { roots[i] = Root(i, 40); Consumer(roots[i], 8, 200); Consumer(roots[i], 46); }
            var result = new List<float>();
            for (int tick = 0; tick < 120; tick++)
            {
                Step(); foreach (var root in roots)
                {
                    var p = Em.GetComponentData<ShipPower>(root); result.Add(p.Stored); result.Add(p.GrantedPerSecond); result.Add((byte)p.Flags);
                    foreach (var c in Em.GetBuffer<PowerConsumer>(root))
                    { result.Add((byte)c.Flags); result.Add(c.DeniedUntilTick); result.Add(Em.IsComponentEnabled<Powered>(c.Functional) ? 1 : 0); }
                }
            }
            return result;
        }
    }
}
