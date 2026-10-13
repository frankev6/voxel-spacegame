using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Galaxy.Data;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    /// <summary>SPEC-30 §5.1: copies managed content once, then schedules deterministic generation.</summary>
    public static class GalaxyDefBuilder
    {
        public static GalaxyDef Build(ContentCatalog catalog, ulong seed, int generatorVersion)
        {
            if (generatorVersion != GalaxyConstants.CurrentGeneratorVersion)
                throw new GalaxyGenerationException("unsupported generator version " + generatorVersion);
            var definitions = catalog.All<GalaxyGenerationDefinition>();
            if (definitions.Count != 1) throw new GalaxyGenerationException("Expected exactly one galaxy definition.");
            var gen = definitions[0];
            var bodies = catalog.All<BodyTemplateDefinition>().ToArray();
            var templates = gen.TemplatePool.Select(x => x.template).ToArray();
            var authored = gen.AuthoredStarSystems.OrderBy(x => x.IdString, StringComparer.Ordinal).ToArray();
            if (bodies.Length > GalaxyConstants.NoIndex || templates.Length > GalaxyConstants.NoIndex)
                throw new GalaxyGenerationException("Template count exceeds supported index range.");
            var indices = new Dictionary<BodyTemplateDefinition, ushort>();
            for (int i = 0; i < bodies.Length; i++) indices.Add(bodies[i], checked((ushort)i));
            var galaxy = new GalaxyDef(seed, generatorVersion, templates, authored, bodies,
                StarSystemId.FromAuthored(gen.StartStarSystem.Id.Hash));
            try
            {
                galaxy.BodyTemplateParams = new NativeArray<BodyTemplateParams>(bodies.Length, Allocator.Persistent);
                for (int i = 0; i < bodies.Length; i++)
                {
                    var b = bodies[i];
                    galaxy.BodyTemplateParams[i] = new BodyTemplateParams
                    { Role = b.Role, RadiusMinM = b.RadiusMinM, RadiusMaxM = b.RadiusMaxM, BeltWidthM = b.BeltWidthM, CanHaveMoons = (byte)(b.CanHaveMoons ? 1 : 0) };
                }
                var pools = new List<WeightedIndex>();
                galaxy.TemplateParams = new NativeArray<StarSystemTemplateParams>(templates.Length, Allocator.Persistent);
                using var ownedWeights = new NativeArray<WeightedIndex>(templates.Length, Allocator.TempJob);
                var weights = ownedWeights;
                for (int i = 0; i < templates.Length; i++)
                {
                    galaxy.TemplateParams[i] = GalaxyParameterPacking.Template(templates[i], indices, pools);
                    weights[i] = new WeightedIndex { Index = (ushort)i, Weight = gen.TemplatePool[i].weight };
                }
                galaxy.AuthoredTemplateParams = new NativeArray<StarSystemTemplateParams>(authored.Length, Allocator.Persistent);
                galaxy.AuthoredBodyRanges = new NativeArray<int2>(authored.Length, Allocator.Persistent);
                var packedBodies = new List<AuthoredBodyParams>();
                using var ownedAuthoredSystems = new NativeArray<StarSystemDef>(authored.Length, Allocator.TempJob);
                var authoredSystems = ownedAuthoredSystems;
                var ids = new HashSet<uint>();
                for (int i = 0; i < authored.Length; i++)
                {
                    var a = authored[i];
                    var id = StarSystemId.FromAuthored(a.Id.Hash);
                    if (!ids.Add(id.Value)) throw new GalaxyGenerationException("Authored star-system ID collision.");
                    galaxy.AuthoredTemplateParams[i] = GalaxyParameterPacking.Template(a.Template, indices, pools);
                    int start = packedBodies.Count;
                    GalaxyParameterPacking.Bodies(a, indices, packedBodies);
                    galaxy.AuthoredBodyRanges[i] = new int2(start, packedBodies.Count - start);
                    StarSystemFlags flags = StarSystemFlags.Authored;
                    if (a == gen.StartStarSystem) flags |= StarSystemFlags.Start;
                    if (a.IsStory) flags |= StarSystemFlags.Story;
                    if (a.HiddenUntilKnown) flags |= StarSystemFlags.HiddenUntilKnown;
                    if (a.NoEncounters) flags |= StarSystemFlags.NoEncounters;
                    if (a.IsHub) flags |= StarSystemFlags.Hub;
                    authoredSystems[i] = new StarSystemDef
                    {
                        Id = id, PositionLy = new double2(a.PositionLy.x, a.PositionLy.y),
                        AuthoredIndex = checked((ushort)(i + 1)), TemplateIndex = GalaxyConstants.NoIndex,
                        StarTemplateIndex = indices[a.Star], Danger = (byte)a.Danger, Flags = flags
                    };
                }
                galaxy.AuthoredBodies = new NativeArray<AuthoredBodyParams>(packedBodies.ToArray(), Allocator.Persistent);
                galaxy.PoolEntries = new NativeArray<WeightedIndex>(pools.ToArray(), Allocator.Persistent);
                using var output = new NativeList<StarSystemDef>(math.max(1, authored.Length + gen.ProceduralStarSystemCount), Allocator.TempJob);
                using var skipped = new NativeReference<int>(Allocator.TempJob);
                new GalaxyLayoutJob
                {
                    Seed = seed, RadiusLy = gen.GalaxyRadiusLy, SpacingLy = gen.MinStarSystemSpacingLy,
                    ProceduralCount = gen.ProceduralStarSystemCount, Attempts = gen.PlacementAttemptsPerStarSystem,
                    Authored = authoredSystems, TemplateWeights = weights, Templates = galaxy.TemplateParams,
                    Pools = galaxy.PoolEntries, Output = output, Skipped = skipped
                }.Schedule().Complete();
                galaxy.StarSystems = new NativeArray<StarSystemDef>(output.AsArray(), Allocator.Persistent);
                galaxy.SkippedProceduralCount = skipped.Value;
                galaxy.IndexById = new NativeHashMap<uint, int>(math.max(1, output.Length), Allocator.Persistent);
                for (int i = 0; i < output.Length; i++) galaxy.IndexById.Add(output[i].Id.Value, i);
                // TODO(GAL-005): generate lanes, adjacency, managed gate specs and galaxy hash.
                return galaxy;
            }
            catch { galaxy.Dispose(); throw; }
        }
    }

    public sealed class GalaxyGenerationException : Exception
    {
        public GalaxyGenerationException(string message) : base(message) { }
    }
}
