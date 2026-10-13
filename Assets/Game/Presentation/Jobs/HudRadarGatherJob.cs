using Game.Presentation.Components;
using Game.Simulation;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Presentation.Jobs
{
    public struct HudRadarCandidate
    {
        public HudRadarBlip Blip;
        public float DistanceSq;
        public int EntityIndex;
        public static bool Farther(in HudRadarCandidate a, in HudRadarCandidate b) =>
            a.DistanceSq > b.DistanceSq || (a.DistanceSq == b.DistanceSq && a.EntityIndex > b.EntityIndex);
    }

    /// <summary>SPEC-61 §5.1: serial query passes share one bounded max-heap of the nearest contacts.</summary>
    [BurstCompile]
    public struct HudRadarGatherJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle Entities;
        [ReadOnly] public ComponentTypeHandle<LocalToWorld> Transforms;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<Allegiance> Allegiances;
        [ReadOnly] public FactionRelations Relations;
        public float3 PlayerPosition;
        public byte PlayerFaction;
        public Entity Target;
        public Entity Waypoint;
        public byte Kind;
        public float Range;
        public NativeArray<HudRadarCandidate> Heap;
        public NativeReference<int> Count;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(Entities);
            var transforms = chunk.GetNativeArray(ref Transforms);
            var iterator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (iterator.NextEntityIndex(out int i))
            {
                Entity entity = entities[i];
                float2 offset = (transforms[i].Position - PlayerPosition).xz;
                float distanceSq = math.lengthsq(offset);
                byte faction = Allegiances.HasComponent(entity) ? Allegiances[entity].Faction : (byte)255;
                byte kind = Kind;
                if (kind == 0 && Grids.HasComponent(entity) && Grids[entity].Kind == GridKind.Station) kind = 1;
                byte flags = (byte)(entity == Target ? 1 : 0);
                if (entity == Waypoint && Waypoint != Entity.Null) flags |= 4;
                if (distanceSq > Range * Range) { offset *= Range * math.rsqrt(distanceSq); flags |= 2; }
                var candidate = new HudRadarCandidate
                {
                    EntityIndex = entity.Index, DistanceSq = distanceSq,
                    Blip = new HudRadarBlip { OffsetXZ = offset, Kind = kind, Flags = flags,
                        Relation = (byte)Relations.Get(PlayerFaction, faction) }
                };
                int count = Count.Value;
                if (count < Heap.Length)
                {
                    int at = count; Count.Value = count + 1;
                    while (at > 0)
                    {
                        int parent = (at - 1) / 2;
                        if (!HudRadarCandidate.Farther(candidate, Heap[parent])) break;
                        Heap[at] = Heap[parent]; at = parent;
                    }
                    Heap[at] = candidate;
                }
                else if (count > 0 && HudRadarCandidate.Farther(Heap[0], candidate))
                {
                    int at = 0;
                    while (at * 2 + 1 < count)
                    {
                        int child = at * 2 + 1;
                        if (child + 1 < count && HudRadarCandidate.Farther(Heap[child + 1], Heap[child])) child++;
                        if (!HudRadarCandidate.Farther(Heap[child], candidate)) break;
                        Heap[at] = Heap[child]; at = child;
                    }
                    Heap[at] = candidate;
                }
            }
        }
    }
}
