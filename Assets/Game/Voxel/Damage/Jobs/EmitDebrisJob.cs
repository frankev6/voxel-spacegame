using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    [BurstCompile] internal unsafe struct EmitDebrisJob : IJob
    {
        [ReadOnly] public NativeList<CellRemoved> Removed;
        [ReadOnly] public NativeList<GridRemovedRange> Ranges;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        public BlobAssetReference<BlockDefsBlob> Blocks;
        public DamageConfig Config;
        // Atomic presentation writes; FxEventQueue.Access protects drain/disposal.
        [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Fx;
        public NativeQueue<SfxEvent>.ParallelWriter Sfx;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;
        static bool Debris(CellRemovedReason reason) => reason == CellRemovedReason.Destroyed || reason == CellRemovedReason.DetachedParticle || reason == CellRemovedReason.Dissolved;
        public void Execute()
        {
            int count = 0; foreach (var cell in Removed) if (Debris(cell.Reason)) count++;
            if (Config.MaxDebrisParticlesPerTick > 0 && count > 0)
            {
                int stride = (count - 1) / Config.MaxDebrisParticlesPerTick + 1, ordinal = 0;
                float scale = math.pow(stride, 1f / 3);
                foreach (var cell in Removed)
                {
                    if (!Debris(cell.Reason)) continue;
                    if (ordinal++ % stride != 0 || !Grids.HasComponent(cell.Grid)) continue;
                    Fx.Enqueue(new FxEvent { Kind = FxKind.Debris, Flags = FxEventFlags.PaletteColor, Pos = cell.WorldPos, Dir = math.normalizesafe(cell.Kick),
                        Vel = cell.WorldVel + cell.Kick, Scale = Grids[cell.Grid].CellSize * scale,
                        Color = (uint)(cell.Color | (Blocks.Value.Blocks[cell.Block].MaterialLayer << 8)) });
                    Data->Current.DebrisParticles++;
                }
            }
            if (Config.CueCellBreak.Value == 0) return;
            foreach (var range in Ranges)
            {
                int destroyed = 0; float3 first = default;
                for (int i = range.Start; i < range.Start + range.Count; i++)
                    if (Removed[i].Reason == CellRemovedReason.Destroyed) { if (destroyed++ == 0) first = Removed[i].WorldPos; }
                if (destroyed > 0) Sfx.Enqueue(new SfxEvent { Cue = Config.CueCellBreak, Pos = first, Volume = math.clamp(.2f + .1f * math.log2(destroyed), .2f, 1) });
            }
        }
    }
}
