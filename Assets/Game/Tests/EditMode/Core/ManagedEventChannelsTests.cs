using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Events;
using Game.Data.Components;
using Game.Presentation.Systems;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-009: every ECS-producible managed event payload has a channel once the drain system exists (SPEC-02 §5.6, §7).</summary>
    public sealed class ManagedEventChannelsTests
    {
        // The managed events of architecture §6.6.
        static readonly string[] ArchitecturePayloads =
        { "ShipDestroyed", "StarSystemEntered", "BodyInteracted", "BodyDestroyed", "LaneJumped", "ItemAcquired", "FactChanged", "SessionReset", "GameSaved", "GameLoaded" };

        // Unmanaged payloads that only managed code publishes, straight through EventBus<T>: no ECS producer, so no queue
        // (SPEC-02 open question 12). SessionReset: SPEC-01 SessionModel.
        static readonly HashSet<string> ManagedOnly = new HashSet<string> { "SessionReset" };

        static IEnumerable<Type> PayloadTypes()
        {
            var assemblies = new[] { typeof(EventHub).Assembly, typeof(ManagedEventQueue<>).Assembly };
            foreach (var name in ArchitecturePayloads)
            {
                if (ManagedOnly.Contains(name)) continue;
                var type = assemblies.Select(a => a.GetType("Game.Core.Events." + name) ?? a.GetType("Game.Data.Logic." + name)).FirstOrDefault(t => t != null);
                if (type != null && type.IsValueType && UnsafeUtility.IsUnmanaged(type)) yield return type;
            }
        }

        [Test]
        public void AllPayloadQueues_Registered()
        {
            using var world = new World("managed-event-channels");
            world.CreateSystemManaged<ManagedEventDrainSystem>();
            var checkedNames = new List<string>();
            foreach (var payload in PayloadTypes())
            {
                var queueType = typeof(ManagedEventQueue<>).MakeGenericType(payload);
                ComponentType component;
                try { component = ComponentType.ReadOnly(queueType); }
                catch (Exception e) { Assert.Fail($"{payload.Name}: ManagedEventQueue<{payload.Name}> is not a registered component (RegisterGenericComponentType missing): {e.Message}"); throw; }
                using var q = world.EntityManager.CreateEntityQuery(component);
                Assert.AreEqual(1, q.CalculateEntityCount(), $"{payload.Name} has no channel: add system.RegisterChannel<{payload.Name}>() to ManagedEventChannels.RegisterAll");
                checkedNames.Add(payload.Name);
            }
            UnityEngine.Debug.Log($"ManagedEventChannels: checked [{string.Join(", ", checkedNames)}]");
        }

        [Test]
        public void ManagedOnlyList_IsStillTrue()
        {
            // Guards the exclusion list: an excluded name must exist, be unmanaged (else it would not need excluding) and be listed in §6.6.
            foreach (var name in ManagedOnly)
            {
                Assert.Contains(name, ArchitecturePayloads);
                var type = typeof(EventHub).Assembly.GetType("Game.Core.Events." + name);
                Assert.IsNotNull(type, name);
                Assert.IsTrue(UnsafeUtility.IsUnmanaged(type), name);
            }
        }
    }
}
