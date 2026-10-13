using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Generation
{
    internal static class GalaxyLaneBuilder
    {
        internal static void Build(GalaxyDef galaxy, GalaxyGenerationDefinition generation)
        {
            using var ownedParticipants = new NativeArray<byte>(galaxy.StarSystems.Length, Allocator.TempJob);
            var participants = ownedParticipants;
            for (int i = 0; i < participants.Length; i++)
            {
                var system = galaxy.StarSystems[i];
                participants[i] = (byte)(system.AuthoredIndex == 0 || galaxy.Authored[system.AuthoredIndex - 1].ConnectProcedurally ? 1 : 0);
            }
            using var ownedAuthored = new NativeArray<LaneDef>(generation.AuthoredLanes.Count, Allocator.TempJob);
            var authored = ownedAuthored;
            var gates = (List<LaneGateSpec>)galaxy.LaneGates;
            for (int i = 0; i < authored.Length; i++)
            {
                var source = generation.AuthoredLanes[i];
                var a = StarSystemId.FromAuthored(source.a.Id.Hash);
                var b = StarSystemId.FromAuthored(source.b.Id.Hash);
                var defaultGate = source.kind == LaneKind.Hyperlane ? generation.DefaultHyperlaneGate : generation.DefaultAncientGate;
                ushort gateA = galaxy.BodyTemplateIndexOf(source.gateTemplateA != null ? source.gateTemplateA : defaultGate);
                ushort gateB = galaxy.BodyTemplateIndexOf(source.gateTemplateB != null ? source.gateTemplateB : defaultGate);
                if (a.Value > b.Value) { (a, b) = (b, a); (gateA, gateB) = (gateB, gateA); }
                ushort gateIndex = 0;
                if (!string.IsNullOrEmpty(source.condition.Source))
                {
                    gates.Add(new LaneGateSpec { Condition = source.condition, LockedMessage = source.lockedMessage });
                    gateIndex = checked((ushort)gates.Count);
                }
                authored[i] = new LaneDef
                {
                    A = a, B = b, LengthLy = (float)math.distance(galaxy.Get(a).PositionLy, galaxy.Get(b).PositionLy),
                    Kind = source.kind, Flags = LaneFlags.Authored | (source.hiddenUntilKnown ? LaneFlags.HiddenUntilKnown : LaneFlags.None),
                    GateIndex = gateIndex, GateTemplateA = gateA, GateTemplateB = gateB
                };
            }
            using var output = new NativeList<LaneDef>(Allocator.TempJob);
            galaxy.TryGetIndex(galaxy.StartStarSystem, out int start);
            new GalaxyLaneJob
            {
                Seed = galaxy.Seed, MaxLength = generation.MaxLaneLengthLy, ExtraChance = generation.ExtraLaneChance,
                CandidateNeighbours = generation.LaneCandidateNeighbours, MaxDegree = generation.MaxLaneDegree,
                StartIndex = start, DefaultGate = galaxy.BodyTemplateIndexOf(generation.DefaultHyperlaneGate),
                Systems = galaxy.StarSystems, Participants = participants, Authored = authored, Output = output
            }.Schedule().Complete();
            galaxy.Lanes = new NativeArray<LaneDef>(output.AsArray(), Allocator.Persistent);
            var adjacency = new List<int>[galaxy.StarSystems.Length];
            for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<int>();
            for (int i = 0; i < output.Length; i++)
            {
                galaxy.TryGetIndex(output[i].A, out int a); galaxy.TryGetIndex(output[i].B, out int b);
                adjacency[a].Add(i); adjacency[b].Add(i);
            }
            galaxy.Adjacency = new NativeArray<int>(checked(output.Length * 2), Allocator.Persistent);
            int offset = 0;
            for (int i = 0; i < adjacency.Length; i++)
            {
                var system = galaxy.StarSystems[i];
                adjacency[i].Sort((a, b) => GalaxyDef.Neighbour(galaxy.Lanes[a], system.Id).Value.CompareTo(GalaxyDef.Neighbour(galaxy.Lanes[b], system.Id).Value));
                system.FirstAdjacency = offset; system.LaneCount = checked((ushort)adjacency[i].Count);
                foreach (int lane in adjacency[i]) galaxy.Adjacency[offset++] = lane;
                galaxy.StarSystems[i] = system;
            }
            galaxy.Hash = GalaxyHash.Compute(galaxy.StarSystems, galaxy.Lanes, galaxy.GeneratorVersion);
        }
    }
}
