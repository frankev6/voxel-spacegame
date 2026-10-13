using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.4.</summary>
    public struct StarSystemTemplateParams
    {
        public int MinPlanets, MaxPlanets, MaxMoonsPerPlanet, MinBelts, MaxBelts, MinStations, MaxStations, MinPois, MaxPois, DangerMin, DangerMax;
        public float MoonChance, FirstOrbitRadiusM, OrbitSpacingMinM, OrbitSpacingMaxM, OrbitReferenceRadiusM,
                     MoonOrbitMinM, MoonOrbitMaxM, MoonReferenceRadiusM, StationRadiusMinM, StationRadiusMaxM,
                     PoiRadiusMinM, PoiRadiusMaxM, MinBodySeparationM, LaneRingMarginM, MinLaneRingRadiusM, ExtentMarginM;
        public double OrbitPeriodAtReferenceS, MoonPeriodAtReferenceS;
        public int2 StarPool, PlanetPool, MoonPool, BeltPool, StationPool, PoiPool;  // (start, count) into GalaxyDef.PoolEntries
    }
}
