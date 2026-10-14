using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    // QA-001: synchronous, strict Burst, like ShapeFillJob. Asynchronous compilation let early Editor runs execute this job
    // as Mono, whose trig differs from Burst's in the last bits, so the galaxy hash depended on Burst cache timing.
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
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
            using var pending = new PendingStarSystemContent(galaxy, id, true);
            return pending.Take(out warning, out error);
        }
    }

    /// <summary>Owns all asynchronous job outputs until the cache takes the completed content.</summary>
    internal sealed class PendingStarSystemContent : System.IDisposable
    {
        StarSystemContent content;
        NativeReference<double2> extents;
        NativeReference<int> warnings, errors;
        readonly int version;
        internal JobHandle Handle { get; private set; }
        internal PendingStarSystemContent(GalaxyDef galaxy, StarSystemId id, bool synchronous = false)
        {
            var system = galaxy.Get(id);
            for (int slot = 0; slot < system.LaneCount; slot++)
                if (galaxy.Adjacency[system.FirstAdjacency + slot] >= GalaxyConstants.NoIndex)
                    throw new GalaxyGenerationException("Adjacent lane index exceeds BodySpec ushort capacity.");
            version = galaxy.GeneratorVersion;
            try
            {
                content = new StarSystemContent(id);
                extents = new NativeReference<double2>(Allocator.Persistent);
                warnings = new NativeReference<int>(Allocator.Persistent);
                errors = new NativeReference<int>(Allocator.Persistent);
                bool authored = system.AuthoredIndex > 0;
                var job = new StarSystemContentJob
                {
                    System = system, Settings = authored ? galaxy.AuthoredTemplateParams[system.AuthoredIndex - 1] : galaxy.TemplateParams[system.TemplateIndex],
                    Pools = galaxy.PoolEntries, Templates = galaxy.BodyTemplateParams, Authored = galaxy.AuthoredBodies,
                    AuthoredRange = authored ? galaxy.AuthoredBodyRanges[system.AuthoredIndex - 1] : default,
                    Lanes = galaxy.Lanes, Adjacency = galaxy.Adjacency, Systems = galaxy.StarSystems, IndexById = galaxy.IndexById,
                    Output = content.Bodies, Extents = extents, Warning = warnings, Error = errors
                };
                if (synchronous) job.Run(); else Handle = job.Schedule();
            }
            catch { Dispose(); throw; }
        }
        internal StarSystemContent Take(out bool warning, out bool error)
        {
            Handle.Complete();
            if (content == null) throw new System.InvalidOperationException("Content was already transferred or disposed.");
            content.LaneRingRadiusM = extents.Value.x; content.ExtentM = extents.Value.y;
            content.GeneratedHash = StarSystemContentHash.Compute(content.Bodies.AsArray(), version);
            warning = warnings.Value != 0; error = errors.Value != 0;
            var result = content; content = null; return result;
        }
        public void Dispose()
        {
            Handle.Complete();
            content?.Dispose(); content = null;
            if (extents.IsCreated) extents.Dispose();
            if (warnings.IsCreated) warnings.Dispose();
            if (errors.IsCreated) errors.Dispose();
        }
    }
}
