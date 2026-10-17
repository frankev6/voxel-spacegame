using Game.Simulation.Flight;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Ai
{
    /// <summary>RACE-017 (owner: "it will be important for the real game world ai to be good at movement"): what a hull can do, from
    /// what is baked into it — PropulsionStats (thrusters and mass, recomputed when one is shot off), the flight caps this tick, and the
    /// engines' spool. Per axis the stats are exact; what they leave out is how the axes combine: with its nose angled into a turn a ship
    /// gets side force from its main engine and its strafe thrusters together (F·sin θ + S·cos θ), up to √(F² + S²) at the drift angle
    /// atan(F/S). Pure; any AI that flies a ship plans with it.</summary>
    public struct PilotEnvelope
    {
        public float Forward, Reverse, Strafe;    // m/s² along each axis (core assist included)
        public float YawAccel, YawRate;           // rad/s², rad/s cap
        public float YawStop;                     // rad/s² it can count on to stop a turn mid-drift: the gyro's, and half the thrusters' (they are busy pushing)
        public float Cruise, Boost, StrafeCap, ReverseCap;   // m/s caps (Boost = forward cap while boosting)
        public float Spool;                       // s for the main engines to reach full thrust (0 = instant)
        public float Response;                    // 1/s: the share of a velocity gap the flight assist closes per second

        public static PilotEnvelope From(in PropulsionStats stats, in FlightState flight, in FlightConfig config, bool boosting, float spool)
        {
            float boost = math.max(1, config.BoostSpeedMultiplier);
            float cruise = boosting ? flight.ForwardCap / boost : flight.ForwardCap;
            return new PilotEnvelope
            {
                Forward = math.max(.5f, stats.ForwardAccel), Reverse = math.max(.5f, stats.ReverseAccel), Strafe = math.max(.5f, stats.StrafeAccel),
                YawAccel = math.max(.2f, math.radians(stats.YawAccelDeg)), YawRate = math.max(.1f, flight.YawRateCap), YawStop = YawStopOf(stats),
                Cruise = cruise, Boost = cruise * boost, StrafeCap = flight.StrafeCap, ReverseCap = flight.ReverseCap, Spool = spool,
                Response = math.max(1, config.LinearAssistGain / Game.Core.SimTime.FixedTimestep),
            };
        }

        /// <summary>This tick's caps exactly as propulsion will apply them (FlightMath), not last tick's FlightState: a ship's command
        /// may be rewritten before the AI reads it (the player's own, under the autopilot), so last tick's boost flag and caps disagreed
        /// by the boost factor, and a command sized against the wrong cap asked for a third less than it meant.</summary>
        public static PilotEnvelope From(in PropulsionStats stats, in FlightConfig config, float speedMultiplier, float yawRateCap, float spool)
        {
            FlightMath.SpeedCaps(config, speedMultiplier, false, out float cruise, out float reverse, out float strafe);
            return new PilotEnvelope
            {
                Forward = math.max(.5f, stats.ForwardAccel), Reverse = math.max(.5f, stats.ReverseAccel), Strafe = math.max(.5f, stats.StrafeAccel),
                YawAccel = math.max(.2f, math.radians(stats.YawAccelDeg)), YawRate = math.max(.1f, yawRateCap), YawStop = YawStopOf(stats),
                Cruise = cruise, Boost = cruise * math.max(1, config.BoostSpeedMultiplier), StrafeCap = strafe, ReverseCap = reverse, Spool = spool,
                Response = math.max(1, config.LinearAssistGain / Game.Core.SimTime.FixedTimestep),
            };
        }

        /// <summary>The gyro's share of the rated yaw acceleration, plus half the thrusters' share. A hull that turns on its thrusters
        /// loses that torque to the push in a drift: planning stops on all of it, a slow one swung past its heading again and again.</summary>
        static float YawStopOf(in PropulsionStats stats)
        {
            float full = math.max(.2f, math.radians(stats.YawAccelDeg)), torque = stats.YawTorque + stats.GyroYawTorque;
            float gyro = torque > 0 ? stats.GyroYawTorque / torque : 1;
            return math.max(.2f, full * (gyro + (1 - gyro) * .5f));
        }

        /// <summary>Main engines' spool: the thrust-weighted seconds to full of the thrusters that push forward.</summary>
        public static float ForwardSpool(DynamicBuffer<FlightThruster> thrusters)
        {
            float weight = 0, seconds = 0;
            for (int i = 0; i < thrusters.Length; i++)
            {
                var t = thrusters[i];
                if (t.ForceDirLocal.z < .7f) continue;
                weight += t.MaxForce; seconds += t.MaxForce * (t.SpoolUp > 0 ? 1 / t.SpoolUp : 0);
            }
            return weight > 0 ? seconds / weight : 0;
        }

        /// <summary>Side acceleration with the nose `nose` rad off the velocity, into the turn (0..π/2).</summary>
        public float Grip(float nose) { nose = math.clamp(nose, 0, math.PI / 2); return Forward * math.sin(nose) + Strafe * math.cos(nose); }
        /// <summary>The most side acceleration it has: everything into the turn, at the drift angle.</summary>
        public float BestGrip => math.sqrt(Forward * Forward + Strafe * Strafe);
        /// <summary>The nose angle off the velocity at which the main engine and the strafe thrusters together turn it hardest.</summary>
        public float DriftAngle => math.atan2(Forward, Strafe);
        /// <summary>The side acceleration it has at `speed`: the nose may only angle so far that the sideways part of the velocity stays
        /// within the strafe cap (past it the assist cannot command the slide), so a fast ship drifts at less than its drift angle.</summary>
        public float GripAt(float speed) => Grip(math.min(DriftAngle, math.asin(math.saturate(StrafeCap * .95f / math.max(1, speed)))));
        /// <summary>The fastest it can hold a turn of radius r with `share` of its grip at that speed, and no faster than it can yaw
        /// round it. (Planning with the full drift grip entered fast corners too fast and ran wide.)</summary>
        public float CornerSpeed(float r, float share)
        {
            float v = math.sqrt(BestGrip * share * r);
            for (int k = 0; k < 4; k++) v = math.sqrt(GripAt(v) * share * r);   // less grip the faster it goes: settles in a few steps
            return math.min(v, YawRate * .9f * r);
        }
        /// <summary>Distance to slow from v0 to v1 with `share` of its reverse thrust.</summary>
        public float StopDistance(float v0, float v1, float share) => math.max(0, v0 * v0 - v1 * v1) / (2 * math.max(.1f, Reverse * share));
    }
}
