using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Galaxy.Racing
{
    /// <summary>RACE-017: one point of a racing-line leg, on the race entity (relative to the track origin, like the gates).</summary>
    [InternalBufferCapacity(0)]
    public struct RaceLegPoint : IBufferElementData
    {
        public float2 Position; public float HalfWidth;
        public float PushSpeed, PushAccel;   // a boost strip here: the speed it pushes ships to, and how hard (m/s, m/s²); 0 = none
    }

    /// <summary>RACE-017: the racing line from gate From to gate To along the route centrelines: Count points from Start in
    /// RaceLegPoint, first at From, last at To. One per edge of the gate graph. Written by TrackSpawnService.</summary>
    [InternalBufferCapacity(0)]
    public struct RaceLeg : IBufferElementData { public byte From, To; public int Start, Count; public float Length; }

    /// <summary>RACE-017: builds the racing line of a track. The route centrelines (TrackDefinition.Paths) become one directed graph in
    /// race direction: each path runs point to point (a closed path wraps), an open path's first point joins from the nearest point of
    /// any other path within JoinDistance, and its last point joins on to one. Every gate sits on its nearest point; each gate edge's
    /// leg is the shortest walk between the two. Pure, so tests check it against the real track.</summary>
    public static class RaceLines
    {
        public const float JoinDistance = 40;

        public struct Leg { public int From, To; public float Length; public List<(float2 position, float halfWidth)> Points; }

        /// <summary>The strongest ship push (boost strip) at p, as (speed it pushes to, acceleration); zero for none.</summary>
        public static float2 Push(TrackDefinition track, float2 p)
        {
            float2 best = 0;
            foreach (var z in track.Zones ?? Array.Empty<TrackZone>())
            {
                if (z.Zone == null || z.Zone.Shape != Game.Data.Hazards.ZoneShape.Box) continue;
                float2 he = z.HalfExtents.x > 0 ? (float2)z.HalfExtents : (float2)z.Zone.HalfExtents;
                float2 d = p - (float2)z.Position, f = new float2(math.sin(z.Yaw), math.cos(z.Yaw)), r = new float2(f.y, -f.x);
                if (math.abs(math.dot(d, r)) > he.x || math.abs(math.dot(d, f)) > he.y) continue;
                foreach (var e in z.Zone.Effects)
                    if (e.Kind == Game.Data.Hazards.ZoneEffectKind.Accelerate && (e.Flags & Game.Data.Hazards.ZoneEffectFlags.AffectsShips) != 0 && e.A > best.x)
                        best = new float2(e.A, e.B);
            }
            return best;
        }

        public static List<Leg> Build(TrackDefinition track)
        {
            var legs = new List<Leg>();
            var paths = track.Paths ?? Array.Empty<TrackPath>();
            var gates = track.Gates ?? Array.Empty<TrackGate>();
            var nodes = new List<(float2 p, float half)>();
            var next = new List<List<int>>();
            var ranges = new List<(int start, int count)>();
            foreach (var path in paths)
            {
                int start = nodes.Count, n = path.Points?.Length ?? 0;
                ranges.Add((start, n));
                for (int i = 0; i < n; i++) { nodes.Add((path.Points[i], path.Width * .5f)); next.Add(new List<int>()); }
                for (int i = 0; i + 1 < n; i++) next[start + i].Add(start + i + 1);
                if (path.Closed && n > 2) next[start + n - 1].Add(start);
            }
            if (nodes.Count == 0) return legs;
            for (int a = 0; a < paths.Length; a++)
            {
                var (start, n) = ranges[a];
                if (paths[a].Closed || n == 0) continue;
                for (int b = 0; b < paths.Length; b++)
                {
                    if (b == a || ranges[b].count == 0) continue;
                    int into = Nearest(nodes, ranges[b], nodes[start].p, out float dIn);
                    if (dIn < JoinDistance) next[into].Add(start);
                    int last = start + n - 1, onto = Nearest(nodes, ranges[b], nodes[last].p, out float dOut);
                    if (dOut < JoinDistance) next[last].Add(onto);
                }
            }
            var gateNode = new int[gates.Length];
            for (int g = 0; g < gates.Length; g++) gateNode[g] = Nearest(nodes, (0, nodes.Count), gates[g].Position, out _);
            for (int g = 0; g < gates.Length; g++)
            {
                if (gates[g].Next == null) continue;
                foreach (int to in gates[g].Next)
                {
                    if (to < 0 || to >= gates.Length) continue;
                    var walk = Walk(nodes, next, gateNode[g], gateNode[to]);
                    if (walk == null) continue;
                    var points = new List<(float2, float)>(walk.Count);
                    float length = 0;
                    for (int i = 0; i < walk.Count; i++)
                    {
                        points.Add((nodes[walk[i]].p, nodes[walk[i]].half));
                        if (i > 0) length += math.distance(nodes[walk[i - 1]].p, nodes[walk[i]].p);
                    }
                    legs.Add(new Leg { From = g, To = to, Length = length, Points = points });
                }
            }
            return legs;
        }

        static int Nearest(List<(float2 p, float half)> nodes, (int start, int count) range, float2 p, out float distance)
        {
            int best = range.start; float bestSq = float.MaxValue;
            for (int i = range.start; i < range.start + range.count; i++)
            {
                float d = math.distancesq(nodes[i].p, p);
                if (d < bestSq) { bestSq = d; best = i; }
            }
            distance = math.sqrt(bestSq);
            return best;
        }

        /// <summary>Dijkstra from a to b (b ≠ a: a walk that returns to its start must leave it first).</summary>
        static List<int> Walk(List<(float2 p, float half)> nodes, List<List<int>> next, int a, int b)
        {
            var dist = new float[nodes.Count]; var prev = new int[nodes.Count];
            for (int i = 0; i < dist.Length; i++) { dist[i] = float.MaxValue; prev[i] = -1; }
            var open = new SortedSet<(float d, int n)>();
            foreach (int s in next[a]) { float d = math.distance(nodes[a].p, nodes[s].p); if (d < dist[s]) { dist[s] = d; prev[s] = a; open.Add((d, s)); } }
            while (open.Count > 0)
            {
                var (d, u) = open.Min; open.Remove(open.Min);
                if (d > dist[u]) continue;
                if (u == b) break;
                foreach (int v in next[u])
                {
                    float nd = d + math.distance(nodes[u].p, nodes[v].p);
                    if (nd < dist[v]) { if (dist[v] != float.MaxValue) open.Remove((dist[v], v)); dist[v] = nd; prev[v] = u; open.Add((nd, v)); }
                }
            }
            if (dist[b] == float.MaxValue) return null;
            var walk = new List<int>();
            for (int at = b; ; at = prev[at])
            {
                walk.Add(at);
                if (at == a && walk.Count > 1) break;
                if (prev[at] < 0) return null;
            }
            walk.Reverse();
            return walk;
        }
    }
}
