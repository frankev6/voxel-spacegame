using Game.Data.Combat;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Physics;

namespace Game.Tests.EditMode.Simulation.Flight
{
    public sealed class PropulsionStatsTests : PropulsionIntegrationFixture
    {
        [Test] public void Spawn_Totals()
        {
            var root = Ship(); Tick(); var stats = Stats(root);
            Assert.AreEqual(4, stats.AliveThrusterCount); Assert.AreEqual(40000, stats.ForwardThrust);
            Assert.AreEqual(40000, stats.ReverseThrust); Assert.AreEqual(40000, stats.StrafeThrust);
            Assert.AreEqual(100, stats.ForwardAccel);
            Assert.AreEqual(EM.GetComponentData<ShipStats>(root).FunctionalRevision, stats.SourceRevision);
        }
        [Test] public void ThrusterDestroyed_Recomputes()
        {
            var root = Ship(); Tick();
            var instances = EM.GetBuffer<BlockInstance>(root); var destroyed = instances[0];
            destroyed.Flags |= BlockInstanceFlags.Destroyed; instances[0] = destroyed;
            var ship = EM.GetComponentData<ShipStats>(root); ship.FunctionalRevision++; EM.SetComponentData(root, ship);
            Tick(); Assert.AreEqual(3, Stats(root).AliveThrusterCount);
            var sentinel = Stats(root); sentinel.ThrusterDraw = -123; EM.SetComponentData(root, sentinel);
            Tick(); Assert.AreEqual(-123, Stats(root).ThrusterDraw, "Unchanged functional revision must not rescan instances.");
            EM.AddComponent<Hulk>(root); ship.FunctionalRevision++; EM.SetComponentData(root, ship);
            Tick(); Assert.AreEqual(sentinel, Stats(root), "Hulks retain their final feature stats.");
        }
        [Test] public void Gyro_TurnsWithoutSideThrusters()
        {
            var root = Ship(1); Tick(); var stats = Stats(root);
            Assert.AreEqual(0, stats.YawTorque); Assert.Greater(stats.GyroYawTorque, 0); Assert.Greater(stats.TurnRateDeg, 0);
        }
        [Test] public void Multiplier_CheapUpdate()
        {
            var root = Ship(); Tick(); var before = Stats(root);
            var sentinel = before; sentinel.ThrusterDraw = -123; EM.SetComponentData(root, sentinel);
            Multiplier(root, StatId.Thrust, 3); Tick();
            Assert.That(Stats(root).ForwardAccel, Is.EqualTo(before.ForwardAccel * 3).Within(.0001f));
            Assert.AreEqual(before.SourceRevision, Stats(root).SourceRevision); Assert.AreEqual(-123, Stats(root).ThrusterDraw);
            // PhysicsMass changes can update gyro inertia without a functional rescan.
            var mass = EM.GetComponentData<PhysicsMass>(root); mass.InverseInertia.y *= .5f; EM.SetComponentData(root, mass);
            Tick(); Assert.That(Stats(root).GyroYawTorque, Is.EqualTo(before.GyroYawTorque * 2).Within(.001f));
            Assert.AreEqual(-123, Stats(root).ThrusterDraw);
        }
    }
}
