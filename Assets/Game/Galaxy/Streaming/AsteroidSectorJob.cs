using Game.Core;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Streaming
{
    internal struct AsteroidSpawnSpec
    {
        public BodyId Id;
        public double3 PositionM;
        public float RadiusM, YawRad;
        public ulong ShapeSeed;
    }

    /// <summary>SPEC-31 §5.5: deterministic descriptors; destruction filtering happens after every draw.</summary>
    [BurstCompile(FloatMode = FloatMode.Strict)]
    internal struct AsteroidSectorJob : IJob
    {
        public BodySpec Belt;
        public int Sector, MinCount, MaxCount;
        public float SectorSizeM, MinRadiusM, MaxRadiusM;
        public NativeList<AsteroidSpawnSpec> Output;

        public void Execute()
        {
            Output.Clear();
            BeltSectorGeometry.Decode(Belt, SectorSizeM, Sector, out int ring, out int angular);
            BeltSectorGeometry.Ring(Belt, SectorSizeM, ring, out double inner, out double outer, out int sectors);
            var rng = RngStreams.For(Belt.Seed, RngPurpose.AsteroidShape, (ulong)Sector);
            int count = rng.NextInt(MinCount, MaxCount + 1);
            for (int k = 0; k < count; k++)
            {
                // Square-root radius is uniform over the annular sector's area.
                double radius = math.sqrt(math.lerp(inner * inner, outer * outer, rng.NextFloat()));
                double angle = (angular + (double)rng.NextFloat()) * (BeltSectorGeometry.Tau / sectors);
                float height = rng.NextFloat(-.25f, .25f) * (Belt.BeltOuterRadiusM - Belt.BeltInnerRadiusM) * .1f;
                float sizeDraw = rng.NextFloat();
                float size = math.lerp(MinRadiusM, MaxRadiusM, sizeDraw * sizeDraw);
                float yaw = rng.NextFloat(0, 2 * math.PI);
                ulong seed = ((ulong)rng.NextUInt() << 32) | rng.NextUInt();
                Output.Add(new AsteroidSpawnSpec
                {
                    Id = new BodyId(Belt.Id.System, BodyRole.Asteroid, Belt.Id.Slot, (ushort)(Sector * 8 + k)),
                    PositionM = new double3(math.cos(angle) * radius, height, math.sin(angle) * radius),
                    RadiusM = size, YawRad = yaw, ShapeSeed = seed
                });
            }
        }
    }
}
