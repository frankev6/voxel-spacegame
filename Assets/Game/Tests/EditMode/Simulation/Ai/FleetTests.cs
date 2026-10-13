using Game.Core.Components;
using Game.Simulation.Ai;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Ai
{
    public sealed class FleetTests : GameTestBase
    {
        static void Join(AiSteeringTests.Fixture f, Entity root, Entity leader, byte slot)
            => f.Em.AddComponentData(root, new FleetMember { Leader = leader, Slot = slot, FormationIndex = 1 });

        [Test] public void HoldsSlots()
        {
            using var f = new AiSteeringTests.Fixture(physics: true, fleet: true);
            var leader = f.PhysicalShip(); var member = f.PhysicalShip();
            var brain = f.Brain(leader); brain.GoalPosition = new float3(0, 0, 5000); brain.GoalSpeedFraction = .1f;
            f.Em.SetComponentData(leader, brain);
            var offset = new float3(-30, 0, -40);
            f.Em.SetComponentData(member, LocalTransform.FromPosition(offset)); Join(f, member, leader, 0);
            float peakError = 0;
            for (int tick = 0; tick < 600; tick++)
            {
                f.Tick();
                var leaderPose = f.Em.GetComponentData<LocalTransform>(leader);
                var memberPose = f.Em.GetComponentData<LocalTransform>(member);
                peakError = math.max(peakError, math.distance(memberPose.Position, leaderPose.Position + offset));
            }
            Assert.Greater(f.Em.GetComponentData<LocalTransform>(leader).Position.z, 20, "Leader must cruise under real propulsion.");
            Assert.LessOrEqual(peakError, 15, "Member must track its moving slot through real steering and physics.");
        }

        [Test] public void FollowsLeaderTarget()
        {
            using var f = new AiSteeringTests.Fixture(decisions: true, fleet: true);
            var leader = f.Ship(); var member = f.Ship(); Join(f, member, leader, 0);
            var enemy = f.Grid(new float3(0, 0, 150), 1);
            for (int i = 0; i < 12; i++) f.Tick();
            Assert.AreEqual(enemy, f.Em.GetComponentData<ShipTarget>(leader).Value);
            Assert.AreEqual(enemy, f.Em.GetComponentData<ShipTarget>(member).Value);
            Assert.AreEqual(AiState.Engage, f.Brain(member).State);
            Assert.AreEqual(f.Brain(leader).ThinkCount, f.SphereQueries,
                "Each leader think queries once; live fleet members must never query the sphere.");
            Assert.Greater(f.Brain(member).ThinkCount, 0);
        }

        [Test] public void LeaderLoss_Promotes()
        {
            foreach (bool hulk in new[] { false, true })
            {
                using var f = new AiSteeringTests.Fixture(fleet: true);
                var leader = f.Ship(); var laterSlot = f.Ship(); var promoted = f.Ship(); var third = f.Ship();
                Join(f, laterSlot, leader, 2); Join(f, promoted, leader, 0); Join(f, third, leader, 1);
                f.Em.SetComponentData(promoted, new PersistentId(20));
                if (hulk) f.Em.AddComponent<Hulk>(leader); else f.Em.DestroyEntity(leader);
                f.Tick();
                Assert.AreEqual(Entity.Null, f.Em.GetComponentData<FleetMember>(promoted).Leader);
                Assert.AreEqual(promoted, f.Em.GetComponentData<FleetMember>(laterSlot).Leader);
                Assert.AreEqual(promoted, f.Em.GetComponentData<FleetMember>(third).Leader);
                f.Tick();
                Assert.AreEqual(promoted, f.Em.GetComponentData<FleetMember>(third).Leader, "Promotion is stable on later ticks.");
            }
        }
    }
}
