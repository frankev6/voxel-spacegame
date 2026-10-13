using Game.Core;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    /// <summary>SPEC-30 §5.5 shared body draw and authored/gate construction, usable from Burst jobs.</summary>
    internal static class BodyGeneration
    {
        internal struct Draws { internal float Template, Radius, Phase, ExtraA, ExtraB; }
        internal static Draws Roll(ulong seed)
        {
            var rng = RngStreams.For(seed, RngPurpose.BodyGeneration, 0);
            return new Draws { Template = rng.NextFloat(), Radius = rng.NextFloat(), Phase = rng.NextFloat(), ExtraA = rng.NextFloat(), ExtraB = rng.NextFloat() };
        }
        internal static BodySpec Create(StarSystemDef system, BodyRole role, ushort slot, ushort subslot, out Draws draws)
        {
            var id = new BodyId(system.Id, role, slot, subslot);
            ulong seed = StableHash.Combine(system.Seed, id.LocalKey);
            draws = Roll(seed);
            return new BodySpec { Id = id, Seed = seed, LaneIndex = GalaxyConstants.NoIndex };
        }
        internal static double Period(double radius, double referencePeriod, double referenceRadius)
            => math.max(60.0, referencePeriod * math.pow(radius / referenceRadius, 1.5));
        internal static void Radius(ref BodySpec body, BodyTemplateParams template, float roll)
        {
            body.RadiusM = math.lerp(template.RadiusMinM, template.RadiusMaxM, roll);
            if (body.Id.Role == BodyRole.Belt)
            {
                body.BeltInnerRadiusM = (float)(body.Orbit.RadiusM - template.BeltWidthM / 2);
                body.BeltOuterRadiusM = (float)(body.Orbit.RadiusM + template.BeltWidthM / 2);
            }
        }
        internal static void Authored(StarSystemDef system, StarSystemTemplateParams settings,
            NativeArray<AuthoredBodyParams> authored, int2 range, NativeArray<BodyTemplateParams> templates,
            NativeList<BodySpec> output)
        {
            for (int i = range.x; i < range.x + range.y; i++)
            {
                var source = authored[i];
                var id = BodyId.FromLocalKey(system.Id, source.LocalKey);
                var body = Create(system, id.Role, id.Slot, id.SubSlot, out var draws);
                body.TemplateIndex = source.TemplateIndex; body.ParentKey = source.ParentKey;
                bool moon = id.Role == BodyRole.Moon;
                body.Orbit = new OrbitElements
                {
                    Mode = source.Mode, RadiusM = source.OrbitRadiusM, PhaseRad = source.PhaseRad,
                    Eccentricity = source.Eccentricity,
                    PeriodS = source.PeriodOverrideS > 0 ? source.PeriodOverrideS : Period(source.OrbitRadiusM,
                        moon ? settings.MoonPeriodAtReferenceS : settings.OrbitPeriodAtReferenceS,
                        moon ? settings.MoonReferenceRadiusM : settings.OrbitReferenceRadiusM)
                };
                if (source.ParentKey != 0)
                    for (int j = 0; j < output.Length; j++)
                        if (output[j].Id.LocalKey == source.ParentKey) { body.ParentOrbit = output[j].Orbit; break; }
                Radius(ref body, templates[body.TemplateIndex], draws.Radius);
                if (source.RadiusOverrideM > 0) body.RadiusM = source.RadiusOverrideM;
                body.YawRad = source.YawRad;
                output.Add(body);
            }
        }
        internal static double Extent(NativeList<BodySpec> bodies)
        {
            double extent = 0;
            foreach (var body in bodies)
                extent = math.max(extent, body.Id.Role == BodyRole.Belt ? body.BeltOuterRadiusM :
                    body.Orbit.RadiusM * (1 + body.Orbit.Eccentricity) + body.RadiusM + body.ParentOrbit.RadiusM);
            return extent;
        }
        internal static void Gate(StarSystemDef system, ushort slot, ushort laneIndex, LaneDef lane,
            double2 neighbourPosition, double ring, double separation, NativeArray<BodyTemplateParams> templates,
            NativeList<BodySpec> output)
        {
            var body = Create(system, BodyRole.LaneGate, slot, 0, out var draws);
            body.LaneIndex = laneIndex;
            body.TemplateIndex = lane.A.Equals(system.Id) ? lane.GateTemplateA : lane.GateTemplateB;
            double2 delta = neighbourPosition - system.PositionLy;
            double angle = math.atan2(delta.y, delta.x);
            for (int retry = 0; retry < 16; retry++)
            {
                bool collision = false;
                foreach (var prior in output)
                {
                    if (prior.Id.Role != BodyRole.LaneGate) continue;
                    double difference = math.abs(math.atan2(math.sin(angle - prior.Orbit.PhaseRad), math.cos(angle - prior.Orbit.PhaseRad)));
                    if (difference * ring < separation) { collision = true; break; }
                }
                if (!collision) break;
                angle += separation / ring;
            }
            body.Orbit = new OrbitElements { Mode = OrbitMode.Fixed, RadiusM = ring, PhaseRad = angle };
            body.YawRad = (float)(angle + math.PI_DBL);
            Radius(ref body, templates[body.TemplateIndex], draws.Radius);
            output.Add(body);
        }
    }
}
