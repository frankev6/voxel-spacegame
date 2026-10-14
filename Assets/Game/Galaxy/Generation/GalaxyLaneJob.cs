using System.Collections.Generic;
using Game.Core;
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
    internal struct GalaxyLaneJob : IJob
    {
        internal ulong Seed;
        internal float MaxLength, ExtraChance;
        internal int CandidateNeighbours, MaxDegree, StartIndex;
        internal ushort DefaultGate;
        [ReadOnly] internal NativeArray<StarSystemDef> Systems;
        [ReadOnly] internal NativeArray<byte> Participants;
        [ReadOnly] internal NativeArray<LaneDef> Authored;
        internal NativeList<LaneDef> Output;

        public void Execute()
        {
            Output.Clear();
            using var authoredPairs = new NativeHashSet<ulong>(math.max(1, Authored.Length), Allocator.Temp);
            foreach (var lane in Authored) { Output.Add(lane); authoredPairs.Add(Key(lane.A.Value, lane.B.Value)); }
            using var grid = new NativeParallelMultiHashMap<int2, int>(math.max(1, Systems.Length), Allocator.Temp);
            for (int i = 0; i < Systems.Length; i++)
                if (Participants[i] != 0) grid.Add(Cell(Systems[i].PositionLy), i);
            using var candidates = new NativeList<Candidate>(Allocator.Temp);
            using var nearby = new NativeList<Candidate>(Allocator.Temp);
            using var pairs = new NativeHashSet<ulong>(math.max(1, Systems.Length), Allocator.Temp);
            for (int i = 0; i < Systems.Length; i++)
            {
                if (Participants[i] == 0) continue;
                nearby.Clear();
                int2 cell = Cell(Systems[i].PositionLy);
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                    {
                        if (!grid.TryGetFirstValue(cell + new int2(x, y), out int j, out var iterator)) continue;
                        do
                        {
                            double distance = math.distance(Systems[i].PositionLy, Systems[j].PositionLy);
                            if (distance > 0 && distance <= MaxLength)
                                nearby.Add(new Candidate { A = i, B = j, Length = distance });
                        } while (grid.TryGetNextValue(out j, ref iterator));
                    }
                nearby.AsArray().Sort(new ByDistance());
                for (int n = 0; n < math.min(CandidateNeighbours, nearby.Length); n++)
                {
                    var edge = nearby[n];
                    if (edge.A > edge.B) { int swap = edge.A; edge.A = edge.B; edge.B = swap; }
                    ulong key = Key(Systems[edge.A].Id.Value, Systems[edge.B].Id.Value);
                    if (!authoredPairs.Contains(key) && pairs.Add(key)) candidates.Add(edge);
                }
            }
            candidates.AsArray().Sort(new ByDistance());
            using var ownedForest = new UnionFind(Systems.Length, Allocator.Temp);
            var forest = ownedForest;
            using var ownedDegrees = new NativeArray<int>(Systems.Length, Allocator.Temp);
            var degrees = ownedDegrees;
            using var accepted = new NativeHashSet<ulong>(math.max(1, candidates.Length), Allocator.Temp);
            foreach (var edge in candidates)
                if (forest.Join(edge.A, edge.B))
                {
                    Add(edge, degrees);
                    accepted.Add(Key(Systems[edge.A].Id.Value, Systems[edge.B].Id.Value));
                }
            var rng = RngStreams.For(Seed, RngPurpose.GalaxyLanes, 0);
            foreach (var edge in candidates)
            {
                if (accepted.Contains(Key(Systems[edge.A].Id.Value, Systems[edge.B].Id.Value))) continue;
                float roll = rng.NextFloat();
                if (roll < ExtraChance && degrees[edge.A] < MaxDegree && degrees[edge.B] < MaxDegree) Add(edge, degrees);
            }
            int anchor = StartIndex;
            if (anchor < 0 || Participants[anchor] == 0)
            {
                anchor = -1;
                for (int i = 0; i < Systems.Length; i++) if (Participants[i] != 0) { anchor = i; break; }
            }
            while (anchor >= 0)
            {
                int component = -1;
                for (int i = 0; i < Systems.Length; i++)
                    if (Participants[i] != 0 && forest.Find(i) != forest.Find(anchor)) { component = forest.Find(i); break; }
                if (component < 0) break;
                var best = new Candidate { A = -1, B = -1, Length = double.PositiveInfinity };
                for (int i = 0; i < Systems.Length; i++)
                {
                    if (Participants[i] == 0 || forest.Find(i) != component) continue;
                    for (int j = 0; j < Systems.Length; j++)
                    {
                        if (Participants[j] == 0 || forest.Find(j) == component) continue;
                        var candidate = new Candidate { A = i, B = j, Length = math.distance(Systems[i].PositionLy, Systems[j].PositionLy) };
                        if (new ByDistance().Compare(candidate, best) < 0) best = candidate;
                    }
                }
                forest.Join(best.A, best.B);
                Add(best, degrees);
            }
            // Authored pairs retain their original flags and gate metadata exactly once.
            for (int i = Output.Length - 1; i >= Authored.Length; i--)
                if (authoredPairs.Contains(Key(Output[i].A.Value, Output[i].B.Value))) Output.RemoveAt(i);
            Output.AsArray().Sort(new ByPair());
        }

        void Add(Candidate edge, NativeArray<int> degrees)
        {
            uint a = Systems[edge.A].Id.Value, b = Systems[edge.B].Id.Value;
            Output.Add(new LaneDef { A = new StarSystemId(math.min(a, b)), B = new StarSystemId(math.max(a, b)),
                LengthLy = (float)edge.Length, Kind = LaneKind.Hyperlane, GateTemplateA = DefaultGate, GateTemplateB = DefaultGate });
            degrees[edge.A]++; degrees[edge.B]++;
        }
        int2 Cell(double2 p) => (int2)math.floor(p / MaxLength);
        static ulong Key(uint a, uint b) => ((ulong)math.min(a, b) << 32) | math.max(a, b);
        struct Candidate { internal int A, B; internal double Length; }
        struct ByDistance : IComparer<Candidate>
        {
            public int Compare(Candidate a, Candidate b)
            {
                int result = a.Length.CompareTo(b.Length);
                if (result == 0) result = a.A.CompareTo(b.A);
                if (result == 0) result = a.B.CompareTo(b.B);
                return result;
            }
        }
        struct ByPair : IComparer<LaneDef>
        {
            public int Compare(LaneDef a, LaneDef b)
            { int result = a.A.Value.CompareTo(b.A.Value); return result != 0 ? result : a.B.Value.CompareTo(b.B.Value); }
        }
    }
}
