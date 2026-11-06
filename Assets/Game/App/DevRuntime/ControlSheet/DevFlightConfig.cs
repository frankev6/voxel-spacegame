#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using Game.Simulation.Flight;
using Unity.Entities;

namespace Game.App.DevRuntime.ControlSheet
{
    /// <summary>Runtime-only edits of the FlightConfig singleton for the control sheet's Flight configs panel (SPEC-70 DEV-030, §5.4).
    /// Main thread. Never touches config_flight: the boot values are kept on first read so Reset, or leaving Play, restores them.</summary>
    internal sealed class DevFlightConfig
    {
        internal delegate float Getter(in FlightConfig c);
        internal delegate void Setter(ref FlightConfig c, float value);

        /// <summary>One editable field. Ranges follow FlightConfigDefinition's attributes; Min-only fields get a practical ceiling.</summary>
        internal sealed class Field
        {
            internal readonly string Name, Unit; internal readonly float Min, Max; internal readonly bool Integer, AiLags;
            internal readonly Getter Get; internal readonly Setter Set;
            internal Field(string name, string unit, float min, float max, Getter get, Setter set, bool integer = false, bool aiLags = false)
            { Name = name; Unit = unit; Min = min; Max = max; Get = get; Set = set; Integer = integer; AiLags = aiLags; }
        }

        // aiLags: PropulsionJob reads these live, but PropulsionStats (AI planning, HUD acceleration) recomputes them only when the ship changes.
        [Game.Core.StaticStateOk("constant field table, never written")]
        internal static readonly IReadOnlyList<Field> Fields = new[]
        {
            new Field("MaxForwardSpeed", "m/s", 1, 1000, (in FlightConfig c) => c.MaxForwardSpeed, (ref FlightConfig c, float v) => c.MaxForwardSpeed = v),
            new Field("ReverseSpeedFraction", "× forward", 0, 1, (in FlightConfig c) => c.ReverseSpeedFraction, (ref FlightConfig c, float v) => c.ReverseSpeedFraction = v),
            new Field("StrafeSpeedFraction", "× forward", 0, 1, (in FlightConfig c) => c.StrafeSpeedFraction, (ref FlightConfig c, float v) => c.StrafeSpeedFraction = v),
            new Field("BoostSpeedMultiplier", "×", 1, 3, (in FlightConfig c) => c.BoostSpeedMultiplier, (ref FlightConfig c, float v) => c.BoostSpeedMultiplier = v),
            new Field("BoostThrottle", "×", 1, 2, (in FlightConfig c) => c.BoostThrottle, (ref FlightConfig c, float v) => c.BoostThrottle = v),
            new Field("ShieldSpeedFraction", "×", .3f, 1, (in FlightConfig c) => c.ShieldSpeedFraction, (ref FlightConfig c, float v) => c.ShieldSpeedFraction = v),
            new Field("MaxYawRateDeg", "°/s", 5, 720, (in FlightConfig c) => c.MaxYawRateDeg, (ref FlightConfig c, float v) => c.MaxYawRateDeg = v),
            new Field("LinearAssistGain", "", .05f, 1, (in FlightConfig c) => c.LinearAssistGain, (ref FlightConfig c, float v) => c.LinearAssistGain = v),
            new Field("AngularAssistGain", "", .05f, 1, (in FlightConfig c) => c.AngularAssistGain, (ref FlightConfig c, float v) => c.AngularAssistGain = v),
            new Field("CoastDeceleration", "/s", 0, 20, (in FlightConfig c) => c.CoastDeceleration, (ref FlightConfig c, float v) => c.CoastDeceleration = v),
            new Field("CoreLinearAccel", "m/s²", 0, 60, (in FlightConfig c) => c.CoreLinearAccel, (ref FlightConfig c, float v) => c.CoreLinearAccel = v, aiLags: true),
            new Field("CoreMaxLinearForce", "N", 0, 1e8f, (in FlightConfig c) => c.CoreMaxLinearForce, (ref FlightConfig c, float v) => c.CoreMaxLinearForce = v, aiLags: true),
            new Field("CoreYawAccelDeg", "°/s²", 0, 720, (in FlightConfig c) => c.CoreYawAccelDeg, (ref FlightConfig c, float v) => c.CoreYawAccelDeg = v, aiLags: true),
            new Field("CoreMaxYawTorque", "N·m", 0, 1e8f, (in FlightConfig c) => c.CoreMaxYawTorque, (ref FlightConfig c, float v) => c.CoreMaxYawTorque = v, aiLags: true),
            new Field("CarryDecel", "m/s²", .01f, 1e6f, (in FlightConfig c) => c.CarryDecel, (ref FlightConfig c, float v) => c.CarryDecel = v),
            new Field("CarryBrakeDecel", "m/s²", .01f, 1e6f, (in FlightConfig c) => c.CarryBrakeDecel, (ref FlightConfig c, float v) => c.CarryBrakeDecel = v),
            new Field("CarryYawDecel", "rad/s²", .01f, 1e6f, (in FlightConfig c) => c.CarryYawDecel, (ref FlightConfig c, float v) => c.CarryYawDecel = v),
            new Field("MaxCarry", "m/s", 0, 1000, (in FlightConfig c) => c.MaxCarry, (ref FlightConfig c, float v) => c.MaxCarry = v),
            new Field("MaxCarryYaw", "rad/s", 0, 20, (in FlightConfig c) => c.MaxCarryYaw, (ref FlightConfig c, float v) => c.MaxCarryYaw = v),
            new Field("TorqueWeight", "", .1f, 10, (in FlightConfig c) => c.TorqueWeight, (ref FlightConfig c, float v) => c.TorqueWeight = v),
            new Field("AllocationSweeps", "", 1, 16, (in FlightConfig c) => c.AllocationSweeps, (ref FlightConfig c, float v) => c.AllocationSweeps = (int)v, integer: true),
            new Field("ThrottleEpsilon", "", 0, .05f, (in FlightConfig c) => c.ThrottleEpsilon, (ref FlightConfig c, float v) => c.ThrottleEpsilon = v),
            new Field("PlaneCorrection", "/tick", .01f, 1, (in FlightConfig c) => c.PlaneCorrection, (ref FlightConfig c, float v) => c.PlaneCorrection = v),
            new Field("IntensityRise", "/tick", .01f, 1, (in FlightConfig c) => c.IntensityRise, (ref FlightConfig c, float v) => c.IntensityRise = v),
            new Field("IntensityFall", "/tick", .01f, 1, (in FlightConfig c) => c.IntensityFall, (ref FlightConfig c, float v) => c.IntensityFall = v),
            // ADR-0123 experimental 3D flight (DEV-031)
            new Field("VerticalSpeedFraction", "× forward", 0, 1, (in FlightConfig c) => c.VerticalSpeedFraction, (ref FlightConfig c, float v) => c.VerticalSpeedFraction = v),
            new Field("VerticalAccel", "m/s²", 0, 60, (in FlightConfig c) => c.VerticalAccel, (ref FlightConfig c, float v) => c.VerticalAccel = v),
            new Field("HeadingResponse", "/s", .2f, 20, (in FlightConfig c) => c.HeadingResponse, (ref FlightConfig c, float v) => c.HeadingResponse = v),
            new Field("MaxPitchDeg", "°", 5, 85, (in FlightConfig c) => c.MaxPitchDeg, (ref FlightConfig c, float v) => c.MaxPitchDeg = v),
            new Field("MaxPitchRateDeg", "°/s", 5, 360, (in FlightConfig c) => c.MaxPitchRateDeg, (ref FlightConfig c, float v) => c.MaxPitchRateDeg = v),
        };

        FlightConfig boot;
        World bootWorld;                                   // the boot values belong to this world; another world takes a fresh copy

        /// <summary>Sets one field, clamped to its range (whole numbers for integer fields). Unknown names and non-finite values are refused.</summary>
        internal static bool TrySet(ref FlightConfig config, string name, double value, out string error)
        {
            error = null;
            var field = Find(name);
            if (field == null) { error = "unknown flight field '" + name + "'"; return false; }
            if (double.IsNaN(value) || double.IsInfinity(value)) { error = name + " must be a finite number"; return false; }
            float v = Math.Clamp((float)value, field.Min, field.Max);
            if (field.Integer) v = (float)Math.Round(v);
            field.Set(ref config, v);
            return true;
        }

        /// <summary>Restores one field (or every field when <paramref name="name"/> is empty) from <paramref name="original"/>.</summary>
        internal static bool TryReset(ref FlightConfig config, in FlightConfig original, string name, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(name)) { config = original; return true; }
            var field = Find(name);
            if (field == null) { error = "unknown flight field '" + name + "'"; return false; }
            field.Set(ref config, field.Get(original));
            return true;
        }

        static Field Find(string name)
        {
            for (int i = 0; i < Fields.Count; i++) if (string.Equals(Fields[i].Name, name, StringComparison.Ordinal)) return Fields[i];
            return null;
        }

        /// <summary>The panel's document: every field's live value, boot value and range; <c>available:false</c> outside a game world.</summary>
        internal static string Json(bool available, in FlightConfig current, in FlightConfig original, string lastError)
        {
            var json = new StringBuilder(4096).Append("{\"available\":").Append(available ? "true" : "false")
                .Append(",\"error\":").Append(DevJsonWriter.Quote(lastError)).Append(",\"fields\":[");
            for (int i = 0; i < Fields.Count; i++)
            {
                var f = Fields[i];
                if (i > 0) json.Append(',');
                json.Append("{\"name\":").Append(DevJsonWriter.Quote(f.Name)).Append(",\"unit\":").Append(DevJsonWriter.Quote(f.Unit))
                    .Append(",\"min\":").Append(DevJsonWriter.Number(f.Min)).Append(",\"max\":").Append(DevJsonWriter.Number(f.Max))
                    .Append(",\"integer\":").Append(f.Integer ? "true" : "false").Append(",\"aiLags\":").Append(f.AiLags ? "true" : "false");
                if (available)
                    json.Append(",\"value\":").Append(DevJsonWriter.Number(f.Get(current))).Append(",\"boot\":").Append(DevJsonWriter.Number(f.Get(original)));
                json.Append('}');
            }
            return json.Append("]}").ToString();
        }

        /// <summary>The live singleton and the boot values; false when the world or the singleton is missing.</summary>
        internal bool TryRead(World world, out FlightConfig current, out FlightConfig original)
        {
            current = original = default;
            if (world == null || !world.IsCreated) return false;
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FlightConfig>());
            if (!query.TryGetSingleton(out current)) return false;
            if (bootWorld != world) { boot = current; bootWorld = world; }
            original = boot;
            return true;
        }

        /// <summary>Applies one queued change: a value (<paramref name="reset"/> false) or a reset of one field or all. §5.4: between world updates, jobs completed.</summary>
        internal bool TryApply(World world, string name, double value, bool reset, out string error)
        {
            if (!TryRead(world, out var config, out var original)) { error = "no FlightConfig: start a game first"; return false; }
            bool ok = reset ? TryReset(ref config, original, name, out error) : TrySet(ref config, name, value, out error);
            if (!ok) return false;
            var em = world.EntityManager;
            em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<FlightConfig>());
            query.SetSingleton(config);
            return true;
        }
    }
}
#endif
