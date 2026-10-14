using Game.Core;
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
    /// <summary>QA-001 salvage pull (fire-and-forget debris). A collected wreck's surface cubes leave in staggered waves over
    /// FxCollect.StaggerSeconds, nearest to the collector first with per-cell jitter, each aimed at its own point on the
    /// collector's hull and living FxCollect.Seconds, the flight time. After the last wave the normal despawn is requested.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameInitGroup)), UpdateBefore(typeof(GridSpawnSystem))]
    public partial struct SalvagePullSystem : ISystem
    {
        const int ParticleCap = 1500;
        const float Jitter = .2f;   // fraction of the wave order randomised per cell, so neighbours do not leave together
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
                Collected = state.GetComponentLookup<SalvageCollected>(), Despawns = state.GetComponentLookup<GridDespawnRequest>(),
                Palettes = state.GetBufferLookup<PaletteEntry>(true), Instances = state.GetBufferLookup<BlockInstance>(true)
            };
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Reader.Update(ref state); job.Bricks.Update(ref state); job.Transforms.Update(ref state); job.Velocities.Update(ref state);
            job.Collected.Update(ref state); job.Despawns.Update(ref state); job.Palettes.Update(ref state); job.Instances.Update(ref state);
            var fx = SystemAPI.GetSingleton<FxEventQueue>();
            job.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; job.Fx = fx.Queue.AsParallelWriter();
            job.DeltaTime = SystemAPI.Time.DeltaTime;
            job.Roots = pulled.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var listed);
            var handle = job.Schedule(fx.Access.BeginRead(listed));
            fx.Access.EndRead(handle);
            state.Dependency = job.Roots.Dispose(handle);
        }
        [BurstCompile] struct PullJob : IJob
        {
            public GridCellReader Reader;
            [ReadOnly] public ComponentLookup<Brick> Bricks;
            [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
            [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
            public ComponentLookup<SalvageCollected> Collected;
            public ComponentLookup<GridDespawnRequest> Despawns;
            [ReadOnly] public BufferLookup<PaletteEntry> Palettes;
            [ReadOnly] public BufferLookup<BlockInstance> Instances;
            [ReadOnly] public NativeList<Entity> Roots;
            public BlobAssetReference<BlockDefsBlob> Blocks;
            public NativeQueue<FxEvent>.ParallelWriter Fx;
            public float DeltaTime;
            public void Execute()
            {
                foreach (var root in Roots)
                {
                    var state = Collected[root];
                    float before = state.Elapsed / FxCollect.StaggerSeconds;
                    state.Elapsed += math.max(0, DeltaTime); Collected[root] = state;
                    float after = state.Elapsed / FxCollect.StaggerSeconds;
                    bool last = after >= 1;
                    if (Transforms.HasComponent(state.Collector) && Reader.Grids.HasComponent(root) && Reader.Slots.HasBuffer(root))
                        Emit(root, state.Collector, before, last ? float.MaxValue : after);
                    if (last && Despawns.HasComponent(root)) Despawns.SetComponentEnabled(root, true);   // GridSpawnApi.RequestDespawn
                }
            }
            /// Emits the surface cells whose wave order lies in [from, to).
            void Emit(Entity root, Entity collector, float from, float to)
            {
                var target = Transforms[collector]; float3 carry = Velocities.HasComponent(collector) ? Velocities[collector].Linear : float3.zero;
                var slots = Reader.Slots[root]; var grid = Reader.Grids[root]; var transform = Transforms[root];
                float near = float.MaxValue, far = 0; int exposed = 0;
                for (int pass = 0; pass < 2; pass++)
                {
                    int ordinal = 0, stride = math.max(1, (exposed - 1) / ParticleCap + 1);
                    for (int s = 0; s < slots.Length; s++)
                    {
                        Entity brick = slots[s].Brick;
                        if (!Bricks.HasComponent(brick)) continue;
                        int3 coord = Bricks[brick].Coord;
                        for (int local = 0; local < 4096; local++)
                        {
                            int3 c = BrickMath.CellOf(coord, local);
                            if (!Reader.TryGetCell(root, c, out var cell) || !Exposed(root, c)) continue;
                            float3 point = transform.TransformPoint(BrickMath.CellCenter(c, grid.CellSize));
                            float distance = math.distance(point, target.Position);
                            if (pass == 0) { near = math.min(near, distance); far = math.max(far, distance); exposed++; continue; }
                            if (ordinal++ % stride != 0) continue;
                            var random = Random.CreateFromIndex((uint)StableHash.Combine((ulong)root.Index, DamageMathPacked(c)));
                            float order = math.saturate((distance - near) / math.max(.001f, far - near)) * (1 - Jitter) + random.NextFloat() * Jitter;
                            if (order < from || order >= to) continue;
                            ushort block; byte color;
                            if (cell.IsInstance) { var value = Instances[root][cell.InstanceIndex]; block = value.Block; color = value.Color; }
                            else { var value = Palettes[root][cell.PaletteIndex]; block = value.Block; color = value.Color; }
                            // Each cube aims at its own point on the collector so the stream lands as separate pieces.
                            float3 aim = target.Position + random.NextFloat3Direction() * random.NextFloat(.5f, 3f) * target.Scale;
                            float3 velocity = (aim - point) / FxCollect.Seconds + carry;
                            Fx.Enqueue(new FxEvent { Kind = FxKind.Debris, Flags = FxEventFlags.PaletteColor | FxEventFlags.PlayerSource | FxEventFlags.Collected,
                                Pos = point, Dir = math.normalizesafe(aim - point, new float3(0, 1, 0)), Vel = velocity, Scale = grid.CellSize,
                                Color = (uint)(color | (Blocks.Value.Blocks[block].MaterialLayer << 8)) });
                        }
                    }
                }
            }
            static ulong DamageMathPacked(int3 c) => ((ulong)(uint)c.x << 42) ^ ((ulong)(uint)c.y << 21) ^ (uint)c.z;
            bool Exposed(Entity root, int3 c) => !Reader.IsSolid(root, c + new int3(1, 0, 0)) || !Reader.IsSolid(root, c - new int3(1, 0, 0)) ||
                !Reader.IsSolid(root, c + new int3(0, 1, 0)) || !Reader.IsSolid(root, c - new int3(0, 1, 0)) ||
                !Reader.IsSolid(root, c + new int3(0, 0, 1)) || !Reader.IsSolid(root, c - new int3(0, 0, 1));
        }
    }
}
