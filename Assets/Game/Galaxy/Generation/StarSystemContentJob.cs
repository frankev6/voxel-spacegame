using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    [BurstCompile]
    internal struct StarSystemContentJob : IJob
    {
        internal StarSystemDef System;
        internal StarSystemTemplateParams Settings;
        [ReadOnly] internal NativeArray<WeightedIndex> Pools;
        [ReadOnly] internal NativeArray<BodyTemplateParams> Templates;
        [ReadOnly] internal NativeArray<AuthoredBodyParams> Authored;
        internal int2 AuthoredRange;
        [ReadOnly] internal NativeArray<LaneDef> Lanes;
        [ReadOnly] internal NativeArray<int> Adjacency;
        [ReadOnly] internal NativeArray<StarSystemDef> Systems;
        [ReadOnly] internal NativeHashMap<uint, int> IndexById;
        internal NativeList<BodySpec> Output;
        internal NativeReference<double2> Extents;
        internal NativeReference<int> Warning, Error;

        public void Execute()
        {
            Output.Clear(); Warning.Value = 0; Error.Value = 0;
            var star = BodyGeneration.Create(System, BodyRole.Star, 0, 0, out var draws);
            star.TemplateIndex = System.StarTemplateIndex;
            BodyGeneration.Radius(ref star, Templates[star.TemplateIndex], draws.Radius);
            Output.Add(star);
            if (System.AuthoredIndex > 0)
                BodyGeneration.Authored(System, Settings, Authored, AuthoredRange, Templates, Output);
            else
                new ProceduralBodies { System = System, Settings = Settings, Pools = Pools,
                    Templates = Templates, Output = Output, Warning = Warning }.Generate();
            double ring = math.max(Settings.MinLaneRingRadiusM, BodyGeneration.Extent(Output) + Settings.LaneRingMarginM);
            Extents.Value = new double2(ring, ring + Settings.ExtentMarginM);
            for (ushort slot = 0; slot < System.LaneCount; slot++)
            {
                int laneIndex = Adjacency[System.FirstAdjacency + slot];
                var lane = Lanes[laneIndex];
                var neighbour = lane.A.Equals(System.Id) ? lane.B : lane.A;
                BodyGeneration.Gate(System, slot, (ushort)laneIndex, lane, Systems[IndexById[neighbour.Value]].PositionLy,
                    ring, Settings.MinBodySeparationM, Templates, Output);
            }
            for (int i = 1; i < Output.Length; i++)
            {
                var value = Output[i]; int j = i - 1;
                while (j >= 0 && Output[j].Id.LocalKey > value.Id.LocalKey) { Output[j + 1] = Output[j]; j--; }
                Output[j + 1] = value;
            }
            if (Output.Length > GalaxyConstants.MaxBodiesPerStarSystem)
            { Output.ResizeUninitialized(GalaxyConstants.MaxBodiesPerStarSystem); Error.Value = 1; }
        }
    }

    internal static class StarSystemContentBuilder
    {
        internal static StarSystemContent Build(GalaxyDef galaxy, StarSystemId id, out bool warning, out bool error)
        {
            var system = galaxy.Get(id);
            for (int slot = 0; slot < system.LaneCount; slot++)
                if (galaxy.Adjacency[system.FirstAdjacency + slot] >= GalaxyConstants.NoIndex)
                    throw new GalaxyGenerationException("Adjacent lane index exceeds BodySpec ushort capacity.");
            var result = new StarSystemContent(id);
            try
            {
                using var extents = new NativeReference<double2>(Allocator.TempJob);
                using var warnings = new NativeReference<int>(Allocator.TempJob);
                using var errors = new NativeReference<int>(Allocator.TempJob);
                bool authored = system.AuthoredIndex > 0;
                new StarSystemContentJob
                {
                    System = system, Settings = authored ? galaxy.AuthoredTemplateParams[system.AuthoredIndex - 1] : galaxy.TemplateParams[system.TemplateIndex],
                    Pools = galaxy.PoolEntries, Templates = galaxy.BodyTemplateParams, Authored = galaxy.AuthoredBodies,
                    AuthoredRange = authored ? galaxy.AuthoredBodyRanges[system.AuthoredIndex - 1] : default,
                    Lanes = galaxy.Lanes, Adjacency = galaxy.Adjacency, Systems = galaxy.StarSystems, IndexById = galaxy.IndexById,
                    Output = result.Bodies, Extents = extents, Warning = warnings, Error = errors
                }.Schedule().Complete();
                result.LaneRingRadiusM = extents.Value.x; result.ExtentM = extents.Value.y;
                result.GeneratedHash = StarSystemContentHash.Compute(result.Bodies.AsArray(), galaxy.GeneratorVersion);
                warning = warnings.Value != 0; error = errors.Value != 0;
                return result;
            }
            catch { result.Dispose(); throw; }
        }
    }
}
