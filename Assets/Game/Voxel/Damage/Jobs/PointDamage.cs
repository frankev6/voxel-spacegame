using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    internal partial struct CellWriter
    {
        public void PointDamage()
        {
            if (math.lengthsq(Event.LocalDir) < 1e-12f)
            {
                var c = DamageMath.CellOf(Event.LocalPoint, Access.Definition.CellSize);
                if (Access.TryGetCell(c, out var cell, out _, out _))
                { ApplyCellDamage(c, Event.Amount * Config.Multiplier(Event.Type, Access.ArmorClass(cell))); CheckInstance(cell); }
                return;
            }
            float3 direction = math.normalize(Event.LocalDir);
            var dda = new GridDda();
            if (!dda.Init(Event.LocalPoint / Access.Definition.CellSize - direction * Config.EntryBacktrackCells, direction, Access.Min, Access.Max)) return;
            float remaining = Event.Amount, left = math.max(1, Event.Penetration); int steps = 0;
            while (steps++ < Config.MaxDdaCells && dda.Next(out var c))
            {
                if (!Access.TryGetCell(c, out var cell, out _, out _)) continue;
                float multiplier = Config.Multiplier(Event.Type, Access.ArmorClass(cell));
                if (multiplier <= 0) break;
                float hp = DamageMath.HpUnits(cell.Hp, Access.CellHp(cell));
                if (remaining * multiplier < hp)
                { ApplyCellDamage(c, remaining * multiplier); CheckInstance(cell); break; }
                RemoveCell(c, CellRemovedReason.Destroyed); CheckInstance(cell);
                remaining -= hp / multiplier; left--;
                if (left <= 0 || remaining <= .001f) break;
            }
        }
    }
}
