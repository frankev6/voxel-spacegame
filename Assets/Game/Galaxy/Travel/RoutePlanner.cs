using System;
using Game.Galaxy.Data;
using Unity.Jobs;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §5.6 route search over caller-populated visibility and gate buffers.</summary>
    public static class RoutePlanner
    {
        public static bool TryFindRoute(GalaxyDef g, RouteScratch scratch, int sourceIndex, int targetIndex, bool ignoreLocks,
            System.Collections.Generic.List<int> lanesOut, out double totalLy)
        {
            if (g == null) throw new ArgumentNullException(nameof(g));
            if (scratch == null) throw new ArgumentNullException(nameof(scratch));
            if (lanesOut == null) throw new ArgumentNullException(nameof(lanesOut));
            lanesOut.Clear(); totalLy = 0;
            scratch.EnsureCapacity(g.StarSystems.Length, g.LaneGates.Count, g.Lanes.Length);
            new RouteSearchJob
            {
                Systems = g.StarSystems, Lanes = g.Lanes, Adjacency = g.Adjacency,
                SystemFlags = scratch.SystemFlags, GateOpen = scratch.GateOpen,
                Source = sourceIndex, Target = targetIndex, IgnoreLocks = (byte)(ignoreLocks ? 1 : 0),
                Dist = scratch.Dist, Hops = scratch.Hops, PrevLane = scratch.PrevLane, PrevSystem = scratch.PrevSystem,
                Settled = scratch.Settled, Heap = scratch.Heap, PathLanes = scratch.PathLanes, TotalLy = scratch.TotalLy, Found = scratch.Found
            }.Run();
            if (scratch.Found.Value == 0) return false;
            totalLy = scratch.TotalLy.Value;
            for (int i = 0; i < scratch.PathLanes.Length; i++) lanesOut.Add(scratch.PathLanes[i]);
            return true;
        }
    }
}
