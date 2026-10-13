using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Galaxy.Encounters;
using Game.Presentation.Systems;
using Game.Simulation;
using Game.Simulation.Factions;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Game.Tests.EditMode.Galaxy.Encounters
{
    /// <summary>The two ENC-001 checks: unmanaged ECS contracts/event channel and the exact data/simulation stance mirror.</summary>
    public sealed class EncounterContractTests : GameTestBase
    {
        [Test] public void EcsTypes_Unmanaged()
        {
            Buffer<FactionStanding>(); Buffer<FactionRelationRequest>();
            Component<FactionState>(); Component<EncounterTag>();
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<FactionRelationChanged>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<EncounterDefeated>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<TerritoryEntered>());
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<PlacementParams>());
            Assert.IsFalse(UnsafeUtility.IsUnmanaged<EncounterAnchor>(), "Story-tag anchors are deliberately managed data, never ECS components.");
            Assert.IsFalse(UnsafeUtility.IsUnmanaged<PlannedShip>(), "Definition references remain in managed composition outputs.");

            // Validate the actual generic component registration and automatic default channel wiring without a faction producer.
            EventHub.Reset();
            var received = new List<FactionRelationChanged>();
            Action<FactionRelationChanged> handler = received.Add;
            EventBus<FactionRelationChanged>.Subscribe(handler);
            try
            {
                using var world = new World("Encounter faction event contract");
                var drain = world.GetOrCreateSystemManaged<ManagedEventDrainSystem>();
                using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ManagedEventQueue<FactionRelationChanged>>());
                Assert.AreEqual(1, query.CalculateEntityCount());
                drain.RegisterChannel<FactionRelationChanged>();
                Assert.AreEqual(1, query.CalculateEntityCount(), "The required channel stays idempotent.");
                var faction = new ContentHash(42);
                var payload = new FactionRelationChanged(faction, 0, -30, (byte)FactionStance.Neutral, (byte)FactionStance.Hostile);
                query.GetSingleton<ManagedEventQueue<FactionRelationChanged>>().Queue.Enqueue(new ManagedEventEnvelope<FactionRelationChanged>
                { Tick = 7, SortKey = 1, Payload = payload });
                Assert.IsEmpty(received);
                drain.Update();
                Assert.AreEqual(1, received.Count);
                Assert.AreEqual(faction, received[0].Faction); Assert.AreEqual(0, received[0].OldRelation); Assert.AreEqual(-30, received[0].NewRelation);
                Assert.AreEqual((byte)FactionStance.Neutral, received[0].OldStance); Assert.AreEqual((byte)FactionStance.Hostile, received[0].NewStance);
                drain.Update(); Assert.AreEqual(1, received.Count, "Drained event is not repeated.");
            }
            finally { EventBus<FactionRelationChanged>.Unsubscribe(handler); EventHub.Reset(); }
        }

        [Test] public void StanceMirror_ValuesEqual()
        {
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(FactionStanceValue)));
            Assert.AreEqual(typeof(byte), Enum.GetUnderlyingType(typeof(FactionStance)));
            CollectionAssert.AreEqual(Enum.GetNames(typeof(FactionStance)), Enum.GetNames(typeof(FactionStanceValue)));
            foreach (FactionStanceValue value in Enum.GetValues(typeof(FactionStanceValue)))
                Assert.AreEqual((byte)Enum.Parse<FactionStance>(value.ToString()), (byte)value, value.ToString());
            Assert.AreEqual(0, (byte)FactionStanceValue.Neutral);
            Assert.AreEqual(1, (byte)FactionStanceValue.Friendly);
            Assert.AreEqual(2, (byte)FactionStanceValue.Hostile);
        }

        static void Component<T>() where T : unmanaged, IComponentData => Assert.IsTrue(UnsafeUtility.IsUnmanaged<T>(), typeof(T).Name);
        static void Buffer<T>() where T : unmanaged, IBufferElementData
        {
            Assert.IsTrue(UnsafeUtility.IsUnmanaged<T>(), typeof(T).Name);
            var capacity = (InternalBufferCapacityAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(InternalBufferCapacityAttribute));
            Assert.NotNull(capacity); Assert.AreEqual(0, capacity.Capacity, typeof(T).Name);
        }
    }
}
