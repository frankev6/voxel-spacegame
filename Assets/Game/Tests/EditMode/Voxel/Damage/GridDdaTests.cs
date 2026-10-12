using System.Collections.Generic;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public sealed class GridDdaTests
    {
        [Test] public void MatchesBruteForce_SeededRays()
        {
            var random = Random.CreateFromIndex(418);
            int3 min = new int3(-4), max = new int3(4);
            for (int ray = 0; ray < 500; ray++)
            {
                float3 start = random.NextFloat3(-6f, 6f), dir = random.NextFloat3Direction();
                if (ray < 6) { start = new float3(.25f); dir = 0; dir[ray / 2] = ray % 2 == 0 ? 1 : -1; }
                // Stratify 10,000 samples by geometric plane crossings. Uniform time samples can miss
                // arbitrarily short cell intervals (seeded ray 296); the reference must not reject them.
                var planes = new List<double> { 0, 20 };
                for (int axis = 0; axis < 3; axis++) if (dir[axis] != 0)
                    for (int boundary = min[axis]; boundary <= max[axis]; boundary++)
                    {
                        double t = (boundary - (double)start[axis]) / dir[axis];
                        if (t > 0 && t < 20) planes.Add(t);
                    }
                planes.Sort();
                var expected = new List<int3>();
                for (int sample = 0; sample < 10000; sample++)
                {
                    int interval = sample * (planes.Count - 1) / 10000;
                    double t = (planes[interval] + planes[interval + 1]) * .5;
                    int3 c = (int3)math.floor((double3)start + (double3)dir * t);
                    if (math.all(c >= min) && math.all(c < max) && (expected.Count == 0 || !expected[expected.Count - 1].Equals(c))) expected.Add(c);
                }
                var actual = new List<int3>(); var dda = new GridDda();
                if (dda.Init(start, dir, min, max)) while (dda.Next(out var c)) actual.Add(c);
                CollectionAssert.AreEqual(expected, actual, $"ray {ray}: {start}, {dir}");
            }
        }
        [Test] public void MissesAabb_ReturnsFalse()
        {
            var dda = new GridDda();
            Assert.IsFalse(dda.Init(new float3(-10, 0, 0), new float3(-1, 0, 0), new int3(-4), new int3(4)));
            Assert.IsFalse(dda.Next(out _));
            Assert.IsFalse(dda.Init(new float3(4, 0, 0), new float3(0, 1, 0), new int3(-4), new int3(4)));
        }
    }
}
