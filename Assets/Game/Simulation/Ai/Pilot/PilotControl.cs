using Game.Core;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Unity.Mathematics;

namespace Game.Simulation.Ai
{
    /// <summary>RACE-017 (owner: "they need to learn to drift and take better turns"): turns a wanted velocity into a ship command the way
    /// a pilot flies, for any AI. It works out the acceleration the ship needs (to reach the wanted velocity, plus the pull of the curve
    /// it is on), then points the nose where its thrusters can give that acceleration: along the way when the strafe thrusters suffice,
    /// angled into the turn when they do not, so the main engine helps pull the ship round while it slides — a drift. The flight assist
    /// already pulls the velocity toward the nose; the nose angle is the pilot's choice. Turns no faster than the hull can stop turning
    /// by the heading it wants (its real yaw acceleration). Pure.</summary>
    public static class PilotControl
    {
        public const float StepRad = 5 * math.PI / 180;   // nose angles tried
        public const float MaxNose = 75 * math.PI / 180;  // never more sideways than this
        public const float SlideSpeed = 25;               // m/s: slower than this, the nose points where it goes
        public const float DriftSlack = 10 * math.PI / 180;   // never more than this past the hull's drift angle

        /// <summary>Command for wanted world velocity `want` (planar) on a path of signed curvature `curvature` (1/m, + = turning right).
        /// `nose` carries the chosen nose angle off the wanted direction between ticks (hysteresis) and returns this tick's.</summary>
        public static ShipCommand Fly(in PilotEnvelope env, in FlightState flight, quaternion rotation, float2 velocity, float2 want, float curvature,
            float forwardCap, ref float nose, float maxNose = MaxNose)
        {
            float yaw = Planar.YawFromForward(math.rotate(rotation, new float3(0, 0, 1)));
            float speed = math.length(want);
            float2 d = speed > .5f ? want / speed : new float2(math.sin(yaw), math.cos(yaw));
            float2 right = new float2(d.y, -d.x);
            // the acceleration it needs: close the velocity gap within its response time, and hold the curve
            float tau = .8f + env.Spool * .5f;
            // (the wanted direction already bends with the path, so only half the curve's pull is added on top; and at the speed it is
            // moving, not the one it wants: from rest the wanted speed's pull asked hundreds of m/s² sideways, and scaled into the
            // thrusters that left nothing to speed up with)
            float moving = math.max(0, math.dot(velocity, d));
            float2 need = (want - velocity) / tau + right * (moving * moving * curvature * .5f);
            // The nose answers the turn, not the braking (reverse thrust brakes), and never asks more than the hull has: an unbounded
            // demand made every angle look short and the nose flailed.
            float lateral = math.clamp(math.dot(need, right), -env.BestGrip, env.BestGrip), along = math.clamp(math.dot(need, d), 0, env.Forward);
            // no more sideways than the assist may fly: past StrafeCap the wanted velocity cannot be commanded, and it brakes instead
            float sideways = math.asin(math.saturate(env.StrafeCap * .95f / math.max(1, speed)));
            float limit = math.length(velocity) < SlideSpeed ? 0 : math.min(math.min(maxNose, env.DriftAngle + DriftSlack), sideways);
            float chosen = ChooseNose(env, d, d * along + right * lateral, limit, nose);
            // and it moves no faster than the hull can swing its nose: a slow hull chasing a jumping angle never settled
            float step = math.min(env.YawRate, math.sqrt(env.YawStop)) * Game.Core.SimTime.FixedTimestep * .6f;
            nose = nose + math.clamp(chosen - nose, -step, step);

            var cmd = default(ShipCommand);
            cmd.Turn = TurnTo(env, yaw, math.atan2(d.x, d.y) + nose);
            cmd.Thrust = Thrust(env, flight, rotation, velocity, need, forwardCap);
            return cmd;
        }

        /// <summary>Command for wanted world velocity `want` while holding the nose on `faceYaw`, a heading the caller chose (on a target,
        /// broadside, in formation): no drift, since the nose is not the pilot's to angle.</summary>
        public static ShipCommand Face(in PilotEnvelope env, in FlightState flight, quaternion rotation, float2 velocity, float2 want, float faceYaw,
            float forwardCap)
        {
            float yaw = Planar.YawFromForward(math.rotate(rotation, new float3(0, 0, 1)));
            var cmd = default(ShipCommand);
            cmd.Turn = TurnTo(env, yaw, faceYaw);
            cmd.Thrust = Thrust(env, flight, rotation, velocity, (want - velocity) / (.8f + env.Spool * .5f), forwardCap);
            return cmd;
        }

        /// <summary>Turn command toward faceYaw: a correction it can stop in time, on the yaw it can count on mid-drift (the wanted
        /// direction already turns with the path; adding the curve's rate overturned it).</summary>
        public static float TurnTo(in PilotEnvelope env, float yaw, float faceYaw)
        {
            float error = Planar.WrapAngle(faceYaw - yaw);
            float rate = math.sign(error) * math.min(math.min(math.abs(error) * 3, env.YawRate), math.sqrt(2 * env.YawStop * .8f * math.abs(error)));
            return math.clamp(rate / env.YawRate, -1, 1);
        }

        /// <summary>Thrust command for world acceleration `need`: scaled as a whole into what the thrusters have so it keeps its
        /// direction, and commanded as the velocity the assist reaches from here (it closes Response of a gap per second). Commanding
        /// the wanted velocity instead let each axis chase its own share: in a drift the strafe saturated, the main engine stayed at
        /// full along a nose angled short of the drift angle, and the ship sped up and ran wide.</summary>
        public static float2 Thrust(in PilotEnvelope env, in FlightState flight, quaternion rotation, float2 velocity, float2 need, float forwardCap)
        {
            float2 accel = FlightMath.ToLocalPlanar(new float3(need.x, 0, need.y), rotation);
            float fit = math.min(1, math.min(env.Strafe / math.max(1e-4f, math.abs(accel.x)),
                (accel.y > 0 ? env.Forward : env.Reverse) / math.max(1e-4f, math.abs(accel.y))));
            // (the assist adds a push's carry to whatever is commanded, ADR-0118: command the difference)
            float2 local = FlightMath.ToLocalPlanar(new float3(velocity.x, 0, velocity.y), rotation) + accel * (fit / env.Response) - flight.Carry;
            return new float2(math.clamp(local.x / math.max(1, env.StrafeCap), -1, 1),
                local.y >= 0 ? math.clamp(local.y / math.max(1, forwardCap), 0, 1) : math.clamp(local.y / math.max(1, env.ReverseCap), -1, 0));
        }

        /// <summary>The nose angle off direction d (rad, + = right) at which the thrusters can best give acceleration `need`: the least
        /// shortfall on any axis, then the least angle, then the least change from last tick's.</summary>
        public static float ChooseNose(in PilotEnvelope env, float2 d, float2 need, float limit, float previous)
        {
            float best = 0, bestCost = float.MaxValue;
            int steps = (int)math.floor(limit / StepRad + 1e-3f);
            for (int k = -steps; k <= steps; k++)
            {
                float a = k * StepRad, c = math.cos(a), s = math.sin(a);
                float2 n = new float2(d.x * c + d.y * s, -d.x * s + d.y * c);   // d turned right by a
                float2 r = new float2(n.y, -n.x);
                float forward = math.dot(need, n), side = math.dot(need, r);
                float shortfall = math.max(0, forward - env.Forward) + math.max(0, -forward - env.Reverse) + math.max(0, math.abs(side) - env.Strafe);
                float cost = shortfall + .6f * math.abs(a) + .8f * math.abs(a - previous);
                if (cost < bestCost) { bestCost = cost; best = a; }
            }
            return best;
        }
    }
}
