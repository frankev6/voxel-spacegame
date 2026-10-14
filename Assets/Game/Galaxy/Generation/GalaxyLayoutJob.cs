using System.Collections.Generic;
using Game.Core;
using Game.Galaxy.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    /// <summary>SPEC-30 §5.2. One ordered job keeps rejection draws independent of worker scheduling.</summary>
    // QA-001: synchronous, strict Burst, like ShapeFillJob. Asynchronous compilation let early Editor runs execute this job
    // as Mono, whose trig differs from Burst's in the last bits, so the galaxy hash depended on Burst cache timing.
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    internal struct GalaxyLayoutJob : IJob
    {
        internal ulong Seed;
        internal double RadiusLy, SpacingLy;
        internal int ProceduralCount, Attempts;
        [ReadOnly] internal NativeArray<StarSystemDef> Authored;
        [ReadOnly] internal NativeArray<WeightedIndex> TemplateWeights;
        [ReadOnly] internal NativeArray<StarSystemTemplateParams> Templates;
        [ReadOnly] internal NativeArray<WeightedIndex> Pools;
        internal NativeList<StarSystemDef> Output;
        internal NativeReference<int> Skipped;

        public void Execute()
        {
            Output.Clear();
            using var grid = new NativeParallelMultiHashMap<int2, int>(math.max(1, Authored.Length + ProceduralCount), Allocator.Temp);
            for (int i = 0; i < Authored.Length; i++)
            {
                var system = Authored[i];
                system.Seed = StableHash.Combine(Seed, system.Id.Value);
                Output.Add(system);
                grid.Add(Cell(system.PositionLy), i);
            }
            var rng = RngStreams.For(Seed, RngPurpose.GalaxyLayout, 0);
            int accepted = 0;
            for (int i = 0; i < ProceduralCount; i++)
            {
                for (int attempt = 0; attempt < Attempts; attempt++)
                {
                    double u = rng.NextDouble(), t = rng.NextDouble();
                    double radius = RadiusLy * math.sqrt(u), angle = 2 * math.PI_DBL * t;
                    double2 position = radius * new double2(math.cos(angle), math.sin(angle));
                    int2 cell = Cell(position);
                    if (Crowded(grid, cell, position)) continue;
                    var id = StarSystemId.FromProceduralIndex(accepted);
                    ulong objectSeed = StableHash.Combine(Seed, id.Value);
                    var attributes = RngStreams.For(objectSeed, RngPurpose.StarSystemGeneration, 0);
                    float templateRoll = attributes.NextFloat(), dangerRoll = attributes.NextFloat(), starRoll = attributes.NextFloat();
                    ushort templateIndex = WeightedPick.Pick(TemplateWeights, new int2(0, TemplateWeights.Length), templateRoll);
                    var template = Templates[templateIndex];
                    var system = new StarSystemDef
                    {
                        Id = id, PositionLy = position, Seed = objectSeed, TemplateIndex = templateIndex,
                        StarTemplateIndex = WeightedPick.Pick(Pools, template.StarPool, starRoll),
                        Danger = (byte)math.min(template.DangerMax, template.DangerMin + (int)math.floor(dangerRoll * (template.DangerMax - template.DangerMin + 1)))
                    };
                    grid.Add(cell, Output.Length);
                    Output.Add(system);
                    accepted++;
                    break;
                }
            }
            Skipped.Value = ProceduralCount - accepted;
            Output.AsArray().Sort(new ById());
        }

        int2 Cell(double2 position) => (int2)math.floor(position / SpacingLy);

        bool Crowded(NativeParallelMultiHashMap<int2, int> grid, int2 cell, double2 position)
        {
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    if (!grid.TryGetFirstValue(cell + new int2(x, y), out int index, out var iterator)) continue;
                    do
                    {
                        if (math.lengthsq(Output[index].PositionLy - position) < SpacingLy * SpacingLy) return true;
                    } while (grid.TryGetNextValue(out index, ref iterator));
                }
            return false;
        }

        struct ById : IComparer<StarSystemDef>
        {
            public int Compare(StarSystemDef a, StarSystemDef b) => a.Id.Value.CompareTo(b.Id.Value);
        }
    }
}
