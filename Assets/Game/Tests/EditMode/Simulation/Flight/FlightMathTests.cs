using Game.Simulation.Commands;
using Game.Simulation.Flight;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>FLT-002 flight-assist formulas (SPEC-20 §5.3).</summary>
    public sealed class FlightMathTests
    {
        [Test] public void Gyro_Capped()
        {
            var config = FlightConfig.Default;
            Assert.AreEqual(0, FlightMath.GyroYawTorque(config, 0));
            Assert.AreEqual(0, FlightMath.GyroYawTorque(config, -1));
            Assert.AreEqual(100 * math.PI / 3, FlightMath.GyroYawTorque(config, .01f), .00002f);
            Assert.AreEqual(config.CoreMaxYawTorque, FlightMath.GyroYawTorque(config, 1e-9f));
        }

        [Test] public void Caps_Boost()
        {
            var config = FlightConfig.Default;
            FlightMath.SpeedCaps(config, 1, false, out var forward, out var reverse, out var strafe);
            Assert.AreEqual(120, forward); Assert.AreEqual(60, reverse); Assert.AreEqual(72, strafe);
            FlightMath.SpeedCaps(config, 2, true, out forward, out reverse, out strafe);
            Assert.AreEqual(360, forward); Assert.AreEqual(120, reverse); Assert.AreEqual(144, strafe);
            Assert.AreEqual(4 * math.PI / 3, FlightMath.YawRateCap(config, 2), .000001f);
        }

        [Test] public void Targets_Coast()
        {
            var config = FlightConfig.Default;
            foreach (var speed in new[] { -90f, -30f, 0f, 85f, 160f })
            {
                FlightMath.Targets(config, default, 0, ShipCommandFlags.None, new float2(12, speed), 120, 60, 72, 2,
                    out var velocity, out var yaw);
                Assert.AreEqual(0, velocity.x);
                float expected = math.clamp(speed, -60, 120) * (1 - config.CoastDeceleration * Game.Core.SimTime.FixedTimestep / config.LinearAssistGain);
                Assert.That(velocity.y, Is.EqualTo(expected).Within(.00002f)); Assert.AreEqual(0, yaw);
            }
            FlightMath.Targets(config, new float2(.5f, -1), -.5f, ShipCommandFlags.None, default, 120, 60, 72, 2,
                out var reverse, out var turn);
            Assert.AreEqual(new float2(36, -60), reverse); Assert.AreEqual(-1, turn);
        }

        [Test] public void Targets_Brake()
        {
            FlightMath.Targets(FlightConfig.Default, new float2(1), 1, ShipCommandFlags.Brake | ShipCommandFlags.Boost,
                new float2(20, 80), 180, 60, 72, 2, out var velocity, out var yaw);
            Assert.AreEqual(float2.zero, velocity); Assert.AreEqual(0, yaw);
        }

        [Test] public void ToLocalPlanar_Yaw90()
        {
            var local = FlightMath.ToLocalPlanar(new float3(10, 3, -4), quaternion.RotateY(math.PI / 2));
            Assert.AreEqual(4, local.x, .000002f); Assert.AreEqual(10, local.y, .000002f);
        }
    }
}
