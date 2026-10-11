using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    /// <summary>PHY-001: SPEC-12 §4.2 filters, including the debug-only all-layer exception.</summary>
    public sealed class PhysicsLayersTests
    {
        static CollisionFilter[] Bodies() => new[] { PhysicsLayers.Grid, PhysicsLayers.ChunkBody,
            PhysicsLayers.Celestial, PhysicsLayers.Sensor, PhysicsLayers.EditorPreview };
        static CollisionFilter[] Queries() => new[] { PhysicsLayers.ProjectileQuery, PhysicsLayers.BeamQuery,
            PhysicsLayers.ExplosionQuery, PhysicsLayers.CameraOcclusionQuery, PhysicsLayers.PickingQuery,
            PhysicsLayers.InteractionQuery, PhysicsLayers.SpawnClearanceQuery, PhysicsLayers.EditorPickQuery,
            PhysicsLayers.DebugAllQuery };

        [Test]
        public void Matrix_MatchesSpecTable()
        {
            bool[,] expected = { { true, true, true, true, false }, { true, true, true, false, false },
                { true, true, false, false, false }, { true, false, false, false, false },
                { false, false, false, false, false } };
            var bodies = Bodies();
            for (int i = 0; i < bodies.Length; i++)
                for (int j = 0; j < bodies.Length; j++)
                    Assert.AreEqual(expected[i, j], PhysicsLayers.CanCollide(bodies[i], bodies[j]), $"pair {i}, {j}");
        }

        [Test]
        public void Queries_HitExactlyListedLayers()
        {
            // Bit positions in this table are the body-array indices, independent of the implementation filters.
            int[] expected = { 0b00101, 0b00101, 0b00001, 0b00001, 0b00101, 0b01101, 0b00111, 0b10001, 0b11111 };
            var queries = Queries(); var bodies = Bodies();
            for (int i = 0; i < queries.Length; i++)
                for (int j = 0; j < bodies.Length; j++)
                    Assert.AreEqual((expected[i] & (1 << j)) != 0, PhysicsLayers.CanCollide(queries[i], bodies[j]), $"query {i}, body {j}");
        }

        [Test]
        public void NoQueryHitsChunkBodyExceptSpawnClearance()
        {
            var queries = Queries();
            for (int i = 0; i < queries.Length - 1; i++) // DebugAllQuery is explicitly diagnostic, not a gameplay query.
                Assert.AreEqual(i == 6, PhysicsLayers.CanCollide(queries[i], PhysicsLayers.ChunkBody), $"query {i}");
            Assert.IsTrue(PhysicsLayers.CanCollide(PhysicsLayers.DebugAllQuery, PhysicsLayers.ChunkBody));
        }
    }
}
