using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    /// <summary>Managed definition-to-job transfer in SPEC-30 §5.1, preserving source pool order.</summary>
    internal static class GalaxyParameterPacking
    {
        internal static StarSystemTemplateParams Template(StarSystemTemplateDefinition source,
            IReadOnlyDictionary<BodyTemplateDefinition, ushort> indices, List<WeightedIndex> pools)
        {
            return new StarSystemTemplateParams
            {
                MinPlanets = source.MinPlanets,
                MaxPlanets = source.MaxPlanets,
                MaxMoonsPerPlanet = source.MaxMoonsPerPlanet,
                MinBelts = source.MinBelts,
                MaxBelts = source.MaxBelts,
                MinStations = source.MinStations,
                MaxStations = source.MaxStations,
                MinPois = source.MinPointsOfInterest,
                MaxPois = source.MaxPointsOfInterest,
                DangerMin = source.DangerMin,
                DangerMax = source.DangerMax,
                MoonChance = source.MoonChance,
                FirstOrbitRadiusM = source.FirstOrbitRadiusM,
                OrbitSpacingMinM = source.OrbitSpacingMinM,
                OrbitSpacingMaxM = source.OrbitSpacingMaxM,
                OrbitReferenceRadiusM = source.OrbitReferenceRadiusM,
                MoonOrbitMinM = source.MoonOrbitMinM,
                MoonOrbitMaxM = source.MoonOrbitMaxM,
                MoonReferenceRadiusM = source.MoonReferenceRadiusM,
                StationRadiusMinM = source.StationRadiusMinM,
                StationRadiusMaxM = source.StationRadiusMaxM,
                PoiRadiusMinM = source.PointOfInterestRadiusMinM,
                PoiRadiusMaxM = source.PointOfInterestRadiusMaxM,
                MinBodySeparationM = source.MinBodySeparationM,
                LaneRingMarginM = source.LaneRingMarginM,
                MinLaneRingRadiusM = source.MinLaneRingRadiusM,
                ExtentMarginM = source.ExtentMarginM,
                OrbitPeriodAtReferenceS = source.OrbitPeriodAtReferenceS,
                MoonPeriodAtReferenceS = source.MoonPeriodAtReferenceS,
                StarPool = Pool(source.StarPool, indices, pools),
                PlanetPool = Pool(source.PlanetPool, indices, pools),
                MoonPool = Pool(source.MoonPool, indices, pools),
                BeltPool = Pool(source.BeltPool, indices, pools),
                StationPool = Pool(source.StationPool, indices, pools),
                PoiPool = Pool(source.PointOfInterestPool, indices, pools),
            };
        }

        static int2 Pool(IReadOnlyList<WeightedBodyTemplate> source,
            IReadOnlyDictionary<BodyTemplateDefinition, ushort> indices, List<WeightedIndex> pools)
        {
            int start = pools.Count;
            for (int i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry.weight <= 0) continue;
                pools.Add(new WeightedIndex { Index = indices[entry.template], Weight = entry.weight, MaxCount = entry.maxCount });
            }
            return new int2(start, pools.Count - start);
        }

        internal static void Bodies(AuthoredStarSystemDefinition system,
            IReadOnlyDictionary<BodyTemplateDefinition, ushort> indices, List<AuthoredBodyParams> output)
        {
            var byKey = new Dictionary<string, AuthoredBody>(StringComparer.Ordinal);
            foreach (var body in system.Bodies) byKey.Add(body.key, body);
            var moonCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var packed = new List<AuthoredBodyParams>(system.Bodies.Count);
            foreach (var body in system.Bodies)
            {
                ushort slot = body.slot, subslot = 0;
                ulong parentKey = 0;
                if (body.role == BodyRole.Moon)
                {
                    var parent = byKey[body.parentKey];
                    slot = parent.slot;
                    moonCounts.TryGetValue(body.parentKey, out int count);
                    subslot = checked((ushort)count);
                    moonCounts[body.parentKey] = count + 1;
                    parentKey = new BodyId(default, BodyRole.Planet, slot, 0).LocalKey;
                }
                packed.Add(new AuthoredBodyParams
                {
                    LocalKey = new BodyId(default, body.role, slot, subslot).LocalKey,
                    ParentKey = parentKey, TemplateIndex = indices[body.template], Mode = body.mode,
                    OrbitRadiusM = body.orbitRadiusM, PhaseRad = math.radians(body.phaseDegrees),
                    Eccentricity = body.eccentricity, PeriodOverrideS = body.periodOverrideS,
                    RadiusOverrideM = body.radiusOverrideM, YawRad = math.radians(body.yawDegrees)
                });
            }
            packed.Sort((a, b) => a.LocalKey.CompareTo(b.LocalKey));
            output.AddRange(packed);
        }
    }
}
