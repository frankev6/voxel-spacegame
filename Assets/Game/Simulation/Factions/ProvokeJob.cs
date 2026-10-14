using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Factions
{
    [BurstCompile]
    internal struct ProvokeJob : IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<Allegiance> Allegiances;
        [ReadOnly] public ComponentTypeHandle<GridDamageStats> Damage;
        [ReadOnly] public NativeList<Entity> Players;
        public NativeArray<byte> Provoked;
        public uint Tick;
        public int Count;
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (Players.Length == 0 || Tick == 0) return;
            var allegiances = chunk.GetNativeArray(ref Allegiances);
            var damage = chunk.GetNativeArray(ref Damage);
            var iterator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (iterator.NextEntityIndex(out int i))
            {
                byte faction = allegiances[i].Faction;
                if (faction > 0 && faction < Count && damage[i].LastDamageTick == Tick - 1 && damage[i].LastDamageInstigator == Players[0])
                    Provoked[faction] = 1;
            }
        }
    }
}
