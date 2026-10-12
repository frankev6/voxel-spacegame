using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    internal partial struct CellWriter
    {
        struct BlastCell { public int3 Cell; public float Damage; }
        public void BlastDamage()
        {
            float radius = math.min(Event.Radius / Access.Definition.CellSize, Config.MaxBlastRadiusCells);
            if (radius < .5f) { PointDamage(); return; }
            float3 centre = Event.LocalPoint / Access.Definition.CellSize;
            int3 lo = math.max(Access.Min, (int3)math.floor(centre - radius));
            int3 hi = math.min(Access.Max - 1, (int3)math.floor(centre + radius));
            using var work = new NativeList<BlastCell>(Allocator.Temp);
            for (int z = lo.z; z <= hi.z; z++) for (int y = lo.y; y <= hi.y; y++) for (int x = lo.x; x <= hi.x; x++)
            {
                int3 c = new int3(x, y, z); float distance = math.distance((float3)c + .5f, centre);
                if (distance >= radius || !Access.TryGetCell(c, out _, out _, out _)) continue;
                float damage = Event.Amount * DamageMath.BlastFalloff(distance, radius);
                if (Config.OcclusionFactor > 0 && radius <= Config.MaxOcclusionRadiusCells)
                {
                    var dda = new GridDda(); var first = (int3)math.floor(centre);
                    if (dda.Init(centre, math.normalizesafe((float3)c + .5f - centre), Access.Min, Access.Max))
                        while (dda.Next(out var between))
                        {
                            if (math.all(between == c)) break;
                            if (math.all(between == first) || !Access.TryGetCell(between, out var b, out _, out _)) continue;
                            float multiplier = Config.Multiplier(Event.Type, Access.ArmorClass(b));
                            damage -= multiplier <= 0 ? float.MaxValue : Config.OcclusionFactor * DamageMath.HpUnits(b.Hp, Access.CellHp(b)) / multiplier;
                            if (damage <= 0) break;
                        }
                }
                if (damage > 0) work.Add(new BlastCell { Cell = c, Damage = damage });
            }
            foreach (var entry in work)
                if (Access.TryGetCell(entry.Cell, out var cell, out _, out _))
                { ApplyCellDamage(entry.Cell, entry.Damage * Config.Multiplier(Event.Type, Access.ArmorClass(cell))); CheckInstance(cell); }
        }
    }
}
