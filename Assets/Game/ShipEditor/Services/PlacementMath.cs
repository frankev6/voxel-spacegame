using Unity.Mathematics;

namespace Game.ShipEditor.Services
{
    public static class PlacementMath
    {
        /// <summary>SPEC-51 §5.10: align the rotated footprint's near face to the placement cell.</summary>
        public static int3 OriginFor(int3 target, int3 normal, int3 aabbMin, int3 aabbMax)
            => target - math.select(aabbMin, aabbMax, normal < 0);
    }
}
