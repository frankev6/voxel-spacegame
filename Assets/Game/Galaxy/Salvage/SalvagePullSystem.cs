using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Galaxy.Salvage
{
    /// <summary>QA-001 salvage pull: one frame before the despawn, the wreck's exposed cells become debris aimed at the
    /// collector. Each piece lives FxCollect.Seconds, the time it needs to reach the collector, so the wreck reads as
    /// being sucked into the ship. Presentation only: cells, cargo and despawn stay with their owners.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameInitGroup)), UpdateBefore(typeof(GridSpawnSystem))]
    public partial struct SalvagePullSystem : ISystem
    {
        const int ParticleCap = 1500;
        EntityQuery pulled;
        PullJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            pulled = new EntityQueryBuilder(Allocator.Temp).WithAll<SalvageCollected, VoxelGrid, LocalTransform>().Build(ref state);
            state.RequireForUpdate(pulled); state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<BlockDefsRef>();
            job = new PullJob
            {
                Reader = GridCellReader.Create(ref state), Bricks = state.GetComponentLookup<Brick>(true),
                Transforms = state.GetComponentLookup<LocalTransform>(true), Velocities = state.GetComponentLookup<PhysicsVelocity>(true),
                Collected = state.GetComponentLookup<SalvageCollected>(true), Palettes = state.GetBufferLookup<PaletteEntry>(true),
                Instances = state.GetBufferLookup<BlockInstance>(true)
            };
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Reader.Update(ref state); job.Bricks.Update(ref state); job.Transforms.Update(ref state); job.Velocities.Update(ref state);
            job.Collected.Update(ref state); job.Palettes.Update(ref state); job.Instances.Update(ref state);
            var fx = SystemAPI.GetSingleton<FxEventQueue>();
            job.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; job.Fx = fx.Queue.AsParallelWriter();
            job.Roots = pulled.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var listed);
            var handle = job.Schedule(fx.Access.BeginRead(listed));
            fx.Access.EndRead(handle);
            state.Dependency = job.Roots.Dispose(handle);
        }
        [BurstCompile] struct PullJob : Unity.Jobs.IJob
        {
            public GridCellReader Reader;
            [ReadOnly] public ComponentLookup<Brick> Bricks;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            [ReadOnly] public ComponentLookup<SalvageCollected> Collected;
            [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
            [ReadOnly] public BufferLookup<BlockInstance> Instances;
            [ReadOnly] public NativeList<Entity> Roots;
            public BlobAssetReference<BlockDefsBlob> Blocks;
            public NativeQueue<FxEvent>.ParallelWriter Fx;
            public void Execute()
            {
                foreach (var root in Roots)
                {
                    var collector = Collected[root].Collector;
                    if (!Transforms.HasComponent(collector) || !Reader.Grids.HasComponent(root) || !Reader.Slots.HasBuffer(root)) continue;
                    var target = Transforms[collector].Position;
                    var carry = Velocities.HasComponent(collector) ? Velocities[collector].Linear : float3.zero;
                    var slots = Reader.Slots[root]; var grid = Reader.Grids[root]; var transform = Transforms[root];
                    int exposed = 0;
                    for (int s = 0; s < slots.Length; s++)
                        if (Bricks.HasComponent(slots[s].Brick)) exposed += math.min((int)Bricks[slots[s].Brick].SolidCount, 1536);
                    int stride = math.max(1, (exposed - 1) / ParticleCap + 1), ordinal = 0, emitted = 0;
                    for (int s = 0; s < slots.Length && emitted < ParticleCap; s++)
                    {
                        Entity brick = slots[s].Brick;
                        if (!Bricks.HasComponent(brick)) continue;
                        int3 coord = Bricks[brick].Coord;
                        for (int local = 0; local < 4096 && emitted < ParticleCap; local++)
                        {
                            int3 c = BrickMath.CellOf(coord, local);
                            if (!Reader.TryGetCell(root, c, out var cell) || !Exposed(root, c) || ++ordinal % stride != 0) continue;
                            ushort block; byte color;
                            if (cell.IsInstance) { var value = Instances[root][cell.InstanceIndex]; block = value.Block; color = value.Color; }
                            else { var value = Palettes[root][cell.PaletteIndex]; block = value.Block; color = value.Color; }
                            float3 point = transform.TransformPoint(BrickMath.CellCenter(c, grid.CellSize));
                            // Arrive at the collector's current position after FxCollect.Seconds, inheriting its motion.
                            float3 velocity = (target - point) / FxCollect.Seconds + carry;
                            Fx.Enqueue(new FxEvent { Kind = FxKind.Debris, Flags = FxEventFlags.PaletteColor | FxEventFlags.PlayerSource | FxEventFlags.Collected,
                                Pos = point, Dir = math.normalizesafe(target - point, new float3(0, 1, 0)), Vel = velocity, Scale = grid.CellSize,
                                Color = (uint)(color | (Blocks.Value.Blocks[block].MaterialLayer << 8)) });
                            emitted++;
                        }
                    }
                }
            }
            bool Exposed(Entity root, int3 c) => !Reader.IsSolid(root, c + new int3(1, 0, 0)) || !Reader.IsSolid(root, c - new int3(1, 0, 0)) ||
                !Reader.IsSolid(root, c + new int3(0, 1, 0)) || !Reader.IsSolid(root, c - new int3(0, 1, 0)) ||
                !Reader.IsSolid(root, c + new int3(0, 0, 1)) || !Reader.IsSolid(root, c - new int3(0, 0, 1));
        }
    }
}
