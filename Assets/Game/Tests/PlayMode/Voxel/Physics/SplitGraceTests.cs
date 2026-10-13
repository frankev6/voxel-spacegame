using Game.Core.Components;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.PlayMode.Voxel.Physics
{
    /// <summary>PHY-014: real dynamic grid contacts, grace expiry and the empty-world scheduling path (SPEC-12 §5.12).</summary>
    [Category("Voxel"), Category("Scenario")]
    public sealed class SplitGraceTests : GameTestBase
    {
        // Ship20k is 40 m wide; this offset leaves 0.8 m of overlap along X.
        const float OverlapOffset = 39.2f;
        const uint UntilTick = 60;

        [Test]
        public void Overlapping_NoContactUntilGraceEnds()
        {
            using var f = new PhysicsScenarioTests.Fixture(typeof(SplitGraceContactSystem));
            Entity parent = f.Spawn();
            Entity piece = f.Spawn(position: new float3(OverlapOffset, 0, 0));
            f.Em.AddComponentData(piece, new SplitGrace { Other = parent, UntilTick = UntilTick });
            f.Ready(parent, piece);
            Assert.Less(f.Singleton<SimClock>().Tick, UntilTick, "Full colliders must be ready during grace");

            while (f.Singleton<SimClock>().Tick < UntilTick)
            {
                AssertStationary(f, parent, float3.zero);
                AssertStationary(f, piece, new float3(OverlapOffset, 0, 0));
                f.Tick();
            }
            // Tick 59 has completed; tick 60 is the first tick allowed to produce contacts.
            AssertStationary(f, parent, float3.zero);
            AssertStationary(f, piece, new float3(OverlapOffset, 0, 0));
            float before = math.distance(f.Position(parent), f.Position(piece));
            f.Tick();
            Assert.Greater(math.distance(f.Position(parent), f.Position(piece)), before + .001f);
            Assert.Less(f.Velocity(parent).Linear.x, 0);
            Assert.Greater(f.Velocity(piece).Linear.x, 0);

            // ADR-0063: pooled chunks additionally wait for geometric clearance after minimum grace.
            Place(f.Em, parent, float3.zero); Place(f.Em, piece, new float3(OverlapOffset, 0, 0));
            f.Em.SetComponentData(parent, default(Unity.Physics.PhysicsVelocity));
            f.Em.SetComponentData(piece, default(Unity.Physics.PhysicsVelocity));
            f.Em.AddComponentData(piece, new ChunkContactState());
            f.Tick(4);
            AssertStationary(f, parent, float3.zero);
            AssertStationary(f, piece, new float3(OverlapOffset, 0, 0));
            Assert.AreEqual(0, f.Em.GetComponentData<ChunkContactState>(piece).Armed);
            Place(f.Em, piece, new float3(100, 0, 0)); f.Tick();
            Assert.AreEqual(1, f.Em.GetComponentData<ChunkContactState>(piece).Armed);
            Place(f.Em, piece, new float3(OverlapOffset, 0, 0)); f.Tick();
            Assert.Greater(f.Velocity(piece).Linear.x, 0, "After clearance, a returning chunk collides normally.");
        }

        [Test]
        public void OtherPairs_Unaffected()
        {
            using var f = new PhysicsScenarioTests.Fixture(typeof(SplitGraceContactSystem));
            Entity parent = f.Spawn();
            Entity piece = f.Spawn(position: new float3(100, 0, 0));
            Entity unrelated = f.Spawn(position: new float3(200, 0, 0));
            f.Ready(parent, piece, unrelated);
            f.Em.AddComponentData(piece, new SplitGrace { Other = parent, UntilTick = UntilTick });
            Place(f.Em, piece, new float3(OverlapOffset, 0, 0));
            float3 unrelatedPosition = new float3(2 * OverlapOffset, 0, 0);
            Place(f.Em, unrelated, unrelatedPosition);
            Assert.Less(f.Singleton<SimClock>().Tick + 4, UntilTick);

            // The same grace-bearing body still contacts a third body while its parent pair is suppressed.
            f.Tick(4);
            AssertStationary(f, parent, float3.zero);
            Assert.Greater(f.Position(unrelated).x, unrelatedPosition.x + .001f);
            Assert.Greater(f.Velocity(unrelated).Linear.x, 0);
            Assert.Less(f.Velocity(piece).Linear.x, 0);
        }

        [Test]
        public void NoGrace_JobNotScheduled()
        {
            using var f = new PhysicsScenarioTests.Fixture(typeof(SplitGraceContactSystem));
            Entity first = f.Spawn();
            Entity second = f.Spawn(position: new float3(100, 0, 0));
            f.Ready(first, second);
            using var query = f.Em.CreateEntityQuery(typeof(SplitGrace));
            Assert.IsTrue(query.IsEmptyIgnoreFilter);
            var handle = f.Test.World.GetExistingSystem<SplitGraceContactSystem>();
            ref var state = ref f.Test.World.Unmanaged.ResolveSystemStateRef(handle);
            // All tracked jobs were completed by the fixture. The empty path must leave its dependency untouched.
            state.Dependency = default;
            f.Test.UpdateSystem<SplitGraceContactSystem>();
            Assert.AreEqual(default(JobHandle), state.Dependency);

            Place(f.Em, second, new float3(OverlapOffset, 0, 0));
            f.Tick();
            Assert.Greater(math.distance(f.Position(first), f.Position(second)), OverlapOffset + .001f);
        }

        static void Place(EntityManager em, Entity body, float3 position)
        {
            var transform = em.GetComponentData<LocalTransform>(body);
            transform.Position = position;
            em.SetComponentData(body, transform);
        }

        static void AssertStationary(PhysicsScenarioTests.Fixture f, Entity body, float3 position)
        {
            Assert.Less(math.distance(position, f.Position(body)), 1e-5f);
            Assert.Less(math.length(f.Velocity(body).Linear), 1e-5f);
            Assert.Less(math.length(f.Velocity(body).Angular), 1e-5f);
        }
    }
}
