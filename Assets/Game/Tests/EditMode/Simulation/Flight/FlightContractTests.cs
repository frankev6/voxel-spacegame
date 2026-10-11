using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Data.Components;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>FLT-001: shared input shape and unmanaged flight contracts (SPEC-20 §9).</summary>
    public sealed class FlightContractTests
    {
        [Test] public void PlayerCommand_HasSpec03Fields()
        {
            var expected = new Dictionary<string, Type>
            {
                ["Thrust"] = typeof(float2), ["Turn"] = typeof(float), ["AimWorld"] = typeof(float3),
                ["AimValid"] = typeof(byte), ["Flags"] = typeof(ShipCommandFlags), ["FireGroups"] = typeof(uint),
                ["Edges"] = typeof(PlayerEdgeCounters), ["FrameIndex"] = typeof(uint)
            };
            var fields = typeof(PlayerCommand).GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.AreEqual(expected.Count, fields.Length);
            foreach (var field in fields)
            {
                Assert.IsTrue(expected.TryGetValue(field.Name, out var type), field.Name);
                Assert.AreEqual(type, field.FieldType, field.Name);
            }
            var counters = typeof(PlayerEdgeCounters).GetFields(BindingFlags.Public | BindingFlags.Instance);
            CollectionAssert.AreEquivalent(new[] { "Interact", "Ability1", "Ability2", "Ability3", "Ability4",
                "TargetNext", "TargetUnderCursor", "TargetClear", "AutofireToggle", "Cruise" }, Array.ConvertAll(counters, f => f.Name));
            foreach (var counter in counters) Assert.AreEqual(typeof(byte), counter.FieldType, counter.Name);
            Assert.AreEqual(10, UnsafeUtility.SizeOf<PlayerEdgeCounters>());
            Assert.AreEqual(1, (byte)ShipCommandFlags.Boost); Assert.AreEqual(2, (byte)ShipCommandFlags.Brake);
        }

        [Test] public void Components_AreUnmanaged()
        {
            Check<ShipCommand>(); Check<PlayerCommand>(); Check<PlayerEdgeConsumed>(); Check<PlayerShip>();
            Check<PlanarConstraint>(); Check<FlightState>(); Check<ThrusterState>(); Check<ThrusterVisual>();
            Check<FlightConfig>(); Check<PropulsionStats>();
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<FlightThruster>());
            Assert.IsTrue(typeof(IBufferElementData).IsAssignableFrom(typeof(FlightThruster)));
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<ThrustProblem>()); Assert.IsTrue(UnsafeUtility.IsUnmanaged<ThrustActuator>());
            Assert.AreEqual("Game.Data.Components", typeof(PlanarConstraint).Namespace);
            Assert.AreEqual("Game.Data", typeof(PlanarConstraint).Assembly.GetName().Name);
            TestContext.WriteLine("FlightThruster layout: " + UnsafeUtility.SizeOf<FlightThruster>() + " bytes");
        }
        static void Check<T>() where T : unmanaged, IComponentData
            => Assert.IsTrue(UnsafeUtility.IsUnmanaged<T>(), typeof(T).Name);
    }
}
