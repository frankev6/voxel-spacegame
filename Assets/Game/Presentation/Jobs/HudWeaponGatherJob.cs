using Game.Presentation.Components;
using Game.Simulation.Weapons;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Presentation.Jobs
{
    /// <summary>SPEC-61 §5.1: single-threaded gather into the three HUD fire-group accumulators.</summary>
    [BurstCompile]
    public struct HudWeaponGatherJob : IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<FunctionalBlock> Blocks;
        [ReadOnly] public ComponentTypeHandle<WeaponState> Weapons;
        public Entity Player;
        public NativeArray<HudWeaponGroupState> Groups;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var blocks = chunk.GetNativeArray(ref Blocks);
            var weapons = chunk.GetNativeArray(ref Weapons);
            var iterator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (iterator.NextEntityIndex(out int i))
            {
                if (blocks[i].Grid != Player) continue;
                var weapon = weapons[i];
                float ready = 1 - weapon.CooldownRemainingTicks / math.max(1, weapon.CooldownTicks);
                for (int g = 0; g < 3; g++)
                {
                    if ((weapon.Group & (1 << g)) == 0) continue;
                    var group = Groups[g];
                    // The public HUD count is a byte; saturation avoids wrapping a large group to zero.
                    group.WeaponCount = (byte)math.min(255, group.WeaponCount + 1);
                    group.Ready01 = math.min(group.Ready01, ready);
                    group.Firing |= weapon.Firing;
                    Groups[g] = group;
                }
            }
        }
    }
}
