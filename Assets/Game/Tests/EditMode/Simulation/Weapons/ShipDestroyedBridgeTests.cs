using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Components;
using Game.Presentation.Systems;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>WPN-017: production queue/drain delivery, ownership attribution and preserved loss identity (SPEC-21 §4.6b).</summary>
    [Category("Weapons")]
    public sealed unsafe class ShipDestroyedBridgeTests : GameTestBase
    {
        EcsTestWorld test;
        EntityManager em;
        readonly List<ShipDestroyed> received = new List<ShipDestroyed>();

        [SetUp]
        public void SetUp()
        {
            EventHub.Reset();
            received.Clear();
            test = CreateWorld(typeof(DamageStreamsSystem), typeof(ShipDestroyedBridgeSystem), typeof(ManagedEventDrainSystem));
            em = test.EntityManager;
            EventBus<ShipDestroyed>.Subscribe(Receive);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<ShipDestroyed>.Unsubscribe(Receive);
            EventHub.Reset();
        }

        void Receive(ShipDestroyed value) => received.Add(value);

        Entity Grid(ulong id, byte faction = 3, ContentHash blueprint = default, GridKind formerKind = GridKind.Ship)
        {
            Entity grid = em.CreateEntity(typeof(PersistentId), typeof(Allegiance), typeof(GridSource), typeof(HulkInfo), typeof(GridDamageStats));
            em.SetComponentData(grid, new PersistentId(id));
            em.SetComponentData(grid, new Allegiance { Faction = faction });
            em.SetComponentData(grid, new GridSource { Blueprint = blueprint });
            em.SetComponentData(grid, new HulkInfo { FormerKind = formerKind, Cause = HulkCause.CoreLost });
            return grid;
        }

        [BurstCompile]
        struct EmitCoreLossJob : IJob
        {
            public NativeList<GridCoreLost> Output;
            public GridCoreLost Loss;
            public void Execute() => Output.Add(Loss);
        }

        void LoseCore(Entity grid, ulong id, uint tick)
        {
            var streams = test.GetSingleton<DamageStreams>();
            var data = streams.Data;
            data->CoreLostWriteHandle = new EmitCoreLossJob
            {
                Output = data->CoreLost,
                Loss = new GridCoreLost { Grid = grid, PersistentId = id, Tick = tick }
            }.Schedule(data->CoreLostWriteHandle);
        }

        void Frame() => test.StepFrame(0);

        [Test]
        public void AiShip_FieldsFilled()
        {
            Entity player = Grid(100, 0);
            em.AddComponent<PlayerShip>(player);
            Entity craft = Grid(200, 0);
            em.AddComponentData(craft, new OwnedBy { Owner = player, OwnerPersistentId = 100 });
            var blueprint = new ContentHash(501);
            Entity victim = Grid(300, blueprint: blueprint);
            em.SetComponentData(victim, new GridDamageStats { LastDamageSource = craft, LastDamageInstigator = player });
            LoseCore(victim, 300, 17);

            Assert.IsEmpty(received);
            Frame();
            Assert.AreEqual(1, received.Count, "The registered drain must deliver in the same frame");
            var loss = received[0];
            Assert.AreEqual(300UL, loss.PersistentId);
            Assert.AreEqual(blueprint, loss.Blueprint);
            Assert.AreEqual(17U, loss.Tick);
            Assert.AreEqual(3, loss.Faction);
            Assert.AreEqual(0, loss.IsPlayer);
            Assert.AreEqual((byte)GridKind.Ship, loss.Kind);
            Assert.AreEqual(default(ContentHash), loss.Encounter);
            Assert.AreEqual(0U, loss.EncounterInstance);
            Assert.AreEqual(100UL, loss.KillerPersistentId, "Credit follows the resolved owner, not the craft source");
            Assert.AreEqual(1, loss.KillerIsPlayer);
            Frame();
            Assert.AreEqual(1, received.Count, "Drained core loss must not be published twice");
        }

        [Test]
        public void Player_IsPlayer()
        {
            Entity player = Grid(42, 0);
            em.AddComponent<PlayerShip>(player);
            LoseCore(player, 42, 9);
            Frame();
            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(1, received[0].IsPlayer);
            Assert.AreEqual(0, received[0].Faction);
            Assert.AreEqual(0UL, received[0].KillerPersistentId);
            Assert.AreEqual(0, received[0].KillerIsPlayer);
        }

        [Test]
        public void SameTick_SortedById()
        {
            Entity larger = Grid(900);
            Entity smaller = Grid(100);
            LoseCore(larger, 900, 22);
            LoseCore(smaller, 100, 22);
            Frame();
            Assert.AreEqual(2, received.Count);
            Assert.AreEqual(100UL, received[0].PersistentId);
            Assert.AreEqual(900UL, received[1].PersistentId);
            Assert.AreEqual(22U, received[0].Tick);
            Assert.AreEqual(22U, received[1].Tick);
        }

        [Test]
        public void DestroyedGrid_StillReported()
        {
            Entity victim = Grid(77, blueprint: new ContentHash(12), formerKind: GridKind.Station);
            em.AddComponent<PlayerShip>(victim);
            LoseCore(victim, 77, 33);
            em.DestroyEntity(victim);
            // No GameplayActive exists in this world: presentation must still report the last loss on scene exit.
            Frame();
            Assert.AreEqual(1, received.Count);
            var loss = received[0];
            Assert.AreEqual(77UL, loss.PersistentId);
            Assert.AreEqual(33U, loss.Tick);
            Assert.AreEqual(255, loss.Faction);
            Assert.AreEqual(default(ContentHash), loss.Blueprint);
            Assert.AreEqual(default(ContentHash), loss.Encounter);
            Assert.AreEqual(0U, loss.EncounterInstance);
            Assert.AreEqual(0, loss.Kind);
            Assert.AreEqual(0, loss.IsPlayer);
            Assert.AreEqual(0UL, loss.KillerPersistentId);
            Assert.AreEqual(0, loss.KillerIsPlayer);
        }
    }
}
