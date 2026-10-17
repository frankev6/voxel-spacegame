using Game.Simulation.Ai;
using Game.Simulation.Flight;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Ai
{
    /// <summary>RACE-017: the shared pilot — a hull's envelope from its stats, and the drift-capable control.</summary>
    public sealed class PilotTests
    {
        // a mid-weight hull: main engine 24 m/s², reverse and strafe 18, yaw 90°/s² up to 120°/s
        static PilotEnvelope Hull() => new PilotEnvelope { Forward = 24, Reverse = 18, Strafe = 18, YawAccel = math.radians(90), YawRate = math.radians(120),
            Cruise = 160, Boost = 240, StrafeCap = 96, ReverseCap = 80, Response = 30, YawStop = math.radians(90) };
        static FlightState Flight() => new FlightState { ForwardCap = 160, StrafeCap = 96, ReverseCap = 80, YawRateCap = math.radians(120) };

        [Test]
        public void Envelope_DriftGrip_MainEngineAndStrafeTogether()
        {
            var e = Hull();
            Assert.AreEqual(18, e.Grip(0), 1e-4f, "nose along the velocity: the strafe thrusters alone");
            Assert.AreEqual(30, e.BestGrip, 1e-3f, "√(24² + 18²)");
            Assert.AreEqual(math.atan2(24, 18), e.DriftAngle, 1e-5f);
            Assert.AreEqual(30, e.Grip(e.DriftAngle), 1e-3f, "at the drift angle it gets all of it");
            Assert.Greater(e.CornerSpeed(600, 1), math.sqrt(18 * 600), "it can corner faster than strafe alone allows");
            Assert.AreEqual(30, e.GripAt(60), 1e-3f, "slow, the nose can reach the drift angle");
            Assert.Less(e.GripAt(200), 29, "fast, the strafe cap keeps the nose short of it");
            float v = e.CornerSpeed(2000, 1);
            Assert.AreEqual(e.GripAt(v) * 2000, v * v, v * v * .01f, "the corner speed is the one its grip at that speed holds");
        }

        [Test]
        public void Envelope_FromStats()
        {
            var stats = new PropulsionStats { ForwardAccel = 24, ReverseAccel = 12, StrafeAccel = 18, YawAccelDeg = 90 };
            var config = FlightConfig.Default;
            var cruising = PilotEnvelope.From(stats, new FlightState { ForwardCap = 120, StrafeCap = 72, ReverseCap = 60, YawRateCap = 2 }, config, false, .4f);
            Assert.AreEqual(24, cruising.Forward); Assert.AreEqual(12, cruising.Reverse); Assert.AreEqual(18, cruising.Strafe);
            Assert.AreEqual(math.radians(90), cruising.YawAccel, 1e-5f); Assert.AreEqual(.4f, cruising.Spool);
            Assert.AreEqual(120, cruising.Cruise); Assert.AreEqual(120 * config.BoostSpeedMultiplier, cruising.Boost, 1e-3f);
            var boosting = PilotEnvelope.From(stats, new FlightState { ForwardCap = 120 * config.BoostSpeedMultiplier, YawRateCap = 2 }, config, true, 0);
            Assert.AreEqual(120, boosting.Cruise, 1e-3f, "while boosting the cruise cap is the boosted cap undone");
        }

        [Test]
        public void Nose_AlongTheWay_WhenStrafeSuffices_IntoTheTurn_WhenNot()
        {
            var e = Hull(); var d = new float2(0, 1);
            Assert.AreEqual(0, PilotControl.ChooseNose(e, d, new float2(10, 0), PilotControl.MaxNose, 0), 1e-5f, "10 m/s² to the side: strafe does it");
            float hard = PilotControl.ChooseNose(e, d, new float2(26, 0), PilotControl.MaxNose, 0);
            Assert.Greater(hard, math.radians(10), "26 m/s² to the right: the nose turns into the turn so the main engine helps");
            Assert.LessOrEqual(hard, PilotControl.MaxNose + 1e-5f);
            Assert.Less(PilotControl.ChooseNose(e, d, new float2(-26, 0), PilotControl.MaxNose, 0), -math.radians(10), "and to the left for a left turn");
        }

        [Test]
        public void Turn_NoFasterThanItCanStop()
        {
            var e = Hull(); e.YawAccel = .3f; e.YawStop = .3f; float nose = 0;
            // facing +x, wanting +z: a 90° turn; a slow-turning hull asks for a fraction of its cap at first
            var cmd = PilotControl.Fly(e, Flight(), quaternion.RotateY(math.PI / 2), new float2(0, 0), new float2(0, 20), 0, 160, ref nose);
            Assert.Less(cmd.Turn, 0, "turns left toward +z");
            Assert.Greater(cmd.Turn, -.6f, "√(2·0.24·π/2) ≈ 0.87 rad/s of a 2.1 rad/s cap");
        }

        [Test]
        public void HoldsTurnsStrafeAloneCannot_ByDrifting()
        {
            // each needs more side force than the strafe thrusters' 18 m/s² (v²/R: 24, 20, 22.5) and less than the 30 a drift gives
            foreach (var (v, R) in new[] { (120f, 600f), (90f, 400f), (150f, 1000f) })
            {
                float drift = CircleError(PilotControl.MaxNose, v, R), locked = CircleError(0, v, R);
                Assert.Less(drift, 15, $"{v} m/s round {R} m: drifting, it holds the line ({drift:0.0} m off)");
                Assert.Greater(locked, drift * 3, $"{v} m/s round {R} m: nose locked to its way it slides wide ({locked:0} m against {drift:0} m)");
            }
            Assert.Less(CircleError(PilotControl.MaxNose, 60, 200), 15, "a turn the strafe thrusters make alone");
        }

        [Test]
        public void FromRest_OnACurve_ItSpeedsUp()
        {
            // the grid start: still, wanting 200 m/s along a line curving away at R = 60 m, on slow-spooling engines (the curve's pull
            // at the wanted speed once took all of the thrust it scaled into)
            var e = Hull(); e.Spool = 2; var flight = Flight(); float2 vel = 0; float nose = 0;
            for (int tick = 0; tick < 180; tick++)
            {
                var cmd = PilotControl.Fly(e, flight, quaternion.identity, vel, new float2(0, 200), 1 / 60f, 160, ref nose);
                float target = cmd.Thrust.y * flight.ForwardCap;
                vel.y += math.clamp((target - vel.y) * .5f * 60, -e.Reverse, e.Forward) / 60;
            }
            Assert.Greater(vel.y, e.Forward * 3 * .8f, "three seconds near full forward thrust");
        }

        /// <summary>Worst distance off a right-hand circle over the last 10 of 20 s, flying a simple assisted hull that aims as the
        /// racer does, at a point on the line its lookahead ahead (25 + 0.8·v m).</summary>
        static float CircleError(float maxNose, float v, float R)
        {
            var e = Hull(); var flight = Flight();
            const float dt = 1f / 60;
            float2 centre = new float2(R, 0), pos = 0, vel = new float2(0, v); float yaw = 0, yawRate = 0, nose = 0, worst = 0;
            for (int tick = 0; tick < 20 * 60; tick++)
            {
                float2 out_ = pos - centre; float at = math.atan2(out_.y, out_.x) - (25 + .8f * v) / R;
                float2 want = math.normalize(centre + new float2(math.cos(at), math.sin(at)) * R - pos) * v;
                var rotation = quaternion.RotateY(yaw);
                var cmd = PilotControl.Fly(e, flight, rotation, vel, want, 1 / R, 160, ref nose, maxNose);
                // the assist: each axis toward its target velocity, within that axis' thrust
                float2 fwd = new float2(math.sin(yaw), math.cos(yaw)), right = new float2(fwd.y, -fwd.x);
                float2 local = new float2(math.dot(vel, right), math.dot(vel, fwd));
                float2 target = new float2(cmd.Thrust.x * flight.StrafeCap, cmd.Thrust.y >= 0 ? cmd.Thrust.y * flight.ForwardCap : cmd.Thrust.y * flight.ReverseCap);
                float ax = math.clamp((target.x - local.x) * .5f / dt, -e.Strafe, e.Strafe), ay = math.clamp((target.y - local.y) * .5f / dt, -e.Reverse, e.Forward);
                vel += (right * ax + fwd * ay) * dt;
                yawRate += math.clamp((cmd.Turn * e.YawRate - yawRate) / dt, -e.YawAccel, e.YawAccel) * dt;
                yaw += yawRate * dt; pos += vel * dt;
                if (tick > 10 * 60) worst = math.max(worst, math.abs(math.length(pos - centre) - R));
            }
            return worst;
        }
    }
}
