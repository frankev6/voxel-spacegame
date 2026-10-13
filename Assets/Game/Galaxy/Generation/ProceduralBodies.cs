using Game.Core;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    internal struct ProceduralBodies
    {
        internal StarSystemDef System;
        internal StarSystemTemplateParams Settings;
        internal NativeArray<WeightedIndex> Pools;
        internal NativeArray<BodyTemplateParams> Templates;
        internal NativeList<BodySpec> Output;
        internal NativeReference<int> Warning;
        NativeArray<int> used;

        internal void Generate()
        {
            var rng = RngStreams.For(System.Seed, RngPurpose.StarSystemLayout, 0);
            int planets = rng.NextInt(Settings.MinPlanets, Settings.MaxPlanets + 1);
            int belts = rng.NextInt(Settings.MinBelts, Settings.MaxBelts + 1);
            int stations = rng.NextInt(Settings.MinStations, Settings.MaxStations + 1);
            int pois = rng.NextInt(Settings.MinPois, Settings.MaxPois + 1);
            using var ownedRadii = new NativeArray<double>(Settings.MaxPlanets + Settings.MaxBelts + 1, Allocator.Temp);
            var radii = ownedRadii; radii[0] = Settings.FirstOrbitRadiusM;
            for (int i = 0; i < radii.Length - 1; i++)
                radii[i + 1] = radii[i] + math.lerp(Settings.OrbitSpacingMinM, Settings.OrbitSpacingMaxM, rng.NextFloat());
            using var ownedMoonRolls = new NativeArray<float>(Settings.MaxPlanets, Allocator.Temp);
            using var ownedMoonCounts = new NativeArray<int>(Settings.MaxPlanets, Allocator.Temp);
            var moonRolls = ownedMoonRolls; var moonCounts = ownedMoonCounts;
            for (int i = 0; i < Settings.MaxPlanets; i++)
            { moonRolls[i] = rng.NextFloat(); moonCounts[i] = rng.NextInt(0, Settings.MaxMoonsPerPlanet + 1); }
            using var ownedUsage = new NativeArray<int>(Pools.Length, Allocator.Temp);
            used = ownedUsage;
            for (ushort p = 0; p < planets; p++)
            {
                var planet = BodyGeneration.Create(System, BodyRole.Planet, p, 0, out var rolls);
                if (!Select(ref planet, Settings.PlanetPool, rolls)) continue;
                planet.Orbit = Moving(radii[p], rolls.Phase, false);
                Output.Add(planet);
                if (Templates[planet.TemplateIndex].CanHaveMoons == 0 || moonRolls[p] >= Settings.MoonChance) continue;
                for (ushort m = 0; m < moonCounts[p]; m++)
                {
                    var moon = BodyGeneration.Create(System, BodyRole.Moon, p, m, out var mr);
                    if (!Select(ref moon, Settings.MoonPool, mr)) continue;
                    double radius = planet.RadiusM + Settings.MoonOrbitMinM + (Settings.MoonOrbitMaxM - Settings.MoonOrbitMinM) * (m + mr.ExtraA) / math.max(1, Settings.MaxMoonsPerPlanet);
                    moon.Orbit = Moving(radius, mr.Phase, true);
                    moon.ParentKey = planet.Id.LocalKey; moon.ParentOrbit = planet.Orbit;
                    Output.Add(moon);
                }
            }
            for (ushort k = 0; k < belts; k++)
            {
                var belt = BodyGeneration.Create(System, BodyRole.Belt, k, 0, out var rolls);
                if (!Select(ref belt, Settings.BeltPool, rolls)) continue;
                belt.Orbit = new OrbitElements { Mode = OrbitMode.Fixed, RadiusM = radii[planets + k] };
                BodyGeneration.Radius(ref belt, Templates[belt.TemplateIndex], rolls.Radius);
                Output.Add(belt);
            }
            FixedBodies(BodyRole.Station, stations, Settings.StationPool, Settings.StationRadiusMinM, Settings.StationRadiusMaxM);
            FixedBodies(BodyRole.PointOfInterest, pois, Settings.PoiPool, Settings.PoiRadiusMinM, Settings.PoiRadiusMaxM);
        }

        bool Select(ref BodySpec body, int2 pool, BodyGeneration.Draws rolls)
        {
            body.TemplateIndex = WeightedPick.Pick(Pools, pool, rolls.Template, used);
            if (body.TemplateIndex == GalaxyConstants.NoIndex) { Warning.Value = 1; return false; }
            BodyGeneration.Radius(ref body, Templates[body.TemplateIndex], rolls.Radius);
            return true;
        }
        OrbitElements Moving(double radius, float phase, bool moon)
            => new OrbitElements { Mode = OrbitMode.Circular, RadiusM = radius, PhaseRad = 2 * math.PI_DBL * phase,
                PeriodS = BodyGeneration.Period(radius, moon ? Settings.MoonPeriodAtReferenceS : Settings.OrbitPeriodAtReferenceS,
                    moon ? Settings.MoonReferenceRadiusM : Settings.OrbitReferenceRadiusM) };

        void FixedBodies(BodyRole role, int count, int2 pool, float minRadius, float maxRadius)
        {
            for (ushort slot = 0; slot < count; slot++)
            {
                var body = BodyGeneration.Create(System, role, slot, 0, out var rolls);
                if (!Select(ref body, pool, rolls)) continue;
                for (int candidate = 0; candidate < 8; candidate++)
                {
                    float u = rolls.ExtraA, v = rolls.Phase;
                    if (candidate > 0)
                    {
                        var rng = RngStreams.For(body.Seed, RngPurpose.BodyGeneration, (ulong)candidate);
                        u = rng.NextFloat(); v = rng.NextFloat();
                    }
                    double radius = math.lerp(minRadius, maxRadius, u), angle = 2 * math.PI_DBL * v;
                    body.Orbit = new OrbitElements { Mode = OrbitMode.Fixed, RadiusM = radius, PhaseRad = angle };
                    double2 position = radius * new double2(math.cos(angle), math.sin(angle));
                    bool clear = true;
                    foreach (var previous in Output)
                    {
                        if (previous.Orbit.Mode != OrbitMode.Fixed) continue;
                        double2 prior = previous.Orbit.RadiusM * new double2(math.cos(previous.Orbit.PhaseRad), math.sin(previous.Orbit.PhaseRad));
                        if (math.distance(position, prior) < Settings.MinBodySeparationM) { clear = false; break; }
                    }
                    if (clear) break;
                }
                body.YawRad = 2 * math.PI * rolls.ExtraB;
                Output.Add(body);
            }
        }
    }
}
