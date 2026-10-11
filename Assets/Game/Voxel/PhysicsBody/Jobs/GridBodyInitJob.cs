using Game.Core;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.6: serial body adoption/initialization and rebuild reset under queue/cache ownership.</summary>
    [BurstCompile]
    internal struct GridBodyInitJob : IJob
    {
        [ReadOnly] public NativeList<Entity> InitRoots, RebuildRoots;
        [ReadOnly] public NativeList<BrickBuildItem> BatchBricks;
        [ReadOnly] public NativeList<AssemblyItem> BatchAssemblies;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<GridSource> Sources;
        [ReadOnly] public ComponentLookup<GridMass> Masses;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PhysicsCollider> Colliders;
        [ReadOnly] public ComponentLookup<PhysicsDamping> Damping;
        [ReadOnly] public ComponentLookup<Brick> Bricks;
        [ReadOnly] public BufferLookup<BrickSlot> Slots;
        public ComponentLookup<GridBodyInit> Inits;
        public ComponentLookup<GridRebuilt> Rebuilt;
        public ComponentLookup<GridMassRecompute> Recompute;
        public ComponentLookup<GridColliderState> States;
        public ColliderCacheContainers Cache;
        public NativeList<ColliderRequest> Requests;
        public EntityCommandBuffer Ecb;
        public PhysicsVoxelConfig Config;
        public uint Tick;
        struct Root : System.IComparable<Root>
        {
            public Entity Entity;
            public uint Serial;
            public int CompareTo(Root other) => Serial.CompareTo(other.Serial);
        }
        public void Execute()
        {
            using var roots = new NativeList<Root>(InitRoots.Length + RebuildRoots.Length, Allocator.Temp);
            for (int i = 0; i < InitRoots.Length; i++) roots.Add(new Root { Entity = InitRoots[i], Serial = Grids[InitRoots[i]].Serial });
            for (int i = 0; i < RebuildRoots.Length; i++) roots.Add(new Root { Entity = RebuildRoots[i], Serial = Grids[RebuildRoots[i]].Serial });
            roots.Sort();
            for (int i = 0; i < roots.Length; i++)
                if (i == 0 || roots[i].Entity != roots[i - 1].Entity) Process(roots[i].Entity);
        }
        void Process(Entity root)
        {
            var grid = Grids[root]; var source = Sources[root]; bool isRebuild = Rebuilt.IsComponentEnabled(root);
            if (isRebuild && Cache.GridIndex.TryGetValue(grid.Serial, out int existing))
            {
                Reset(existing);
                var state = States[root]; state.Phase = GridColliderPhase.Provisional; States[root] = state;
                Recompute.SetComponentEnabled(root, true); Enqueue(root, grid.Serial, ColliderPriority.Spawn);
                Rebuilt.SetComponentEnabled(root, false); return;
            }
            var init = Inits[root]; var massData = Masses[root].Data; var transform = Transforms[root];
            bool hasBody = Colliders.HasComponent(root), planar = (source.Flags & GridSpawnFlags.PlanarInertia) != 0;
            bool preview = (source.Flags & GridSpawnFlags.EditorPreview) != 0;
            if (init.Motion == GridMotion.Dynamic && !hasBody && massData.CellCount == 0)
            { Recompute.SetComponentEnabled(root, true); return; }
            int index;
            if (Cache.GridIndex.TryGetValue(grid.Serial, out index)) Reset(index);
            else index = ColliderPipeline.AddRecord(ref Cache, root, grid.Serial, grid.CellSize, init.Motion, preview ? (byte)1 : (byte)0);
            var blob = hasBody ? Colliders[root].Value : GridBodyFactory.CreateProvisionalCollider(grid.CellMin, grid.CellMax, grid.CellSize, preview);
            var record = Cache.Grids[index];
            if (record.Installed != blob)
            {
                var stats = Cache.Stats.Value; stats.InstalledBytes += ColliderPipeline.Bytes(blob) - ColliderPipeline.Bytes(record.Installed); Cache.Stats.Value = stats;
                ColliderPipeline.Retire(ref Cache, record.Installed); record.Installed = blob;
            }
            record.Motion = init.Motion; record.CellSize = grid.CellSize; record.IsEditorPreview = preview ? (byte)1 : (byte)0; Cache.Grids[index] = record;
            if (!hasBody)
            {
                bool dynamic = init.Motion == GridMotion.Dynamic, damping = Damping.HasComponent(root);
                Ecb.AddComponent(root, init.Motion == GridMotion.Static ? GridBodyFactory.StaticBodyTypes() :
                    dynamic && !damping ? GridBodyFactory.DynamicBodyTypes() : GridBodyFactory.KinematicBodyTypes());
                Ecb.SetComponent(root, new PhysicsCollider { Value = blob }); Ecb.AddSharedComponent(root, GridBodyFactory.WorldIndex());
                if (init.Motion != GridMotion.Static)
                {
                    var mass = dynamic ? GridMassMath.CreateDynamicMass(massData, grid.CellMin, grid.CellMax, grid.CellSize, planar) : GridMassMath.CreateKinematicMass(massData);
                    var velocity = new PhysicsVelocity { Linear = init.LinearVelocity,
                        Angular = math.rotate(math.inverse(math.mul(transform.Rotation, mass.InertiaOrientation)), init.AngularVelocity) };
                    if (dynamic && planar) { velocity.Angular.x = 0; velocity.Angular.z = 0; }
                    Ecb.SetComponent(root, mass); Ecb.SetComponent(root, velocity);
                    if (dynamic && !damping) Ecb.SetComponent(root, GridBodyFactory.DefaultDamping(Config));
                    Ecb.SetComponent(root, new PhysicsGraphicalSmoothing { ApplySmoothing = 1 });
                    Ecb.SetComponent(root, new PhysicsGraphicalInterpolationBuffer
                    { PreviousTransform = new RigidTransform(transform.Rotation, transform.Position), PreviousVelocity = velocity });
                }
            }
            States[root] = new GridColliderState { Phase = GridColliderPhase.Provisional, Motion = init.Motion, IsEditorPreview = preview ? (byte)1 : (byte)0,
                InstalledTick = Tick, BoxCount = 1 };
            Enqueue(root, grid.Serial, preview ? ColliderPriority.Edit : hasBody ? ColliderPriority.Split : ColliderPriority.Spawn);
            Inits.SetComponentEnabled(root, false); if (isRebuild) Rebuilt.SetComponentEnabled(root, false);
        }
        void Enqueue(Entity root, uint serial, ColliderPriority priority)
        {
            var slots = Slots[root]; int count = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                var brick = slots[i].Brick; if (brick == Entity.Null) continue; count++;
                Requests.Add(new ColliderRequest { Grid = root, Serial = serial, Brick = brick, Coord = Bricks[brick].Coord, Priority = priority });
            }
            if (count == 0)
            {
                FixedString128Bytes message = "PHY: grid without bricks";
                GameLog.Burst.Warn(message);
            }
        }
        void Reset(int index)
        {
            var record = Cache.Grids[index]; var stats = Cache.Stats.Value;
            for (int i = 0; i < record.Children.Length; i++)
            {
                var child = record.Children[i]; stats.CachedBricks--; stats.CachedBoxes -= child.BoxCount; stats.CachedBrickBytes -= ColliderPipeline.Bytes(child.Blob);
            }
            Cache.Stats.Value = stats;
            for (int i = 0; i < record.Children.Length; i++) ColliderPipeline.Retire(ref Cache, record.Children[i].Blob);
            record.Children.Clear(); Cache.Grids[index] = record;
            for (int i = record.Updates.Length - 1; i >= 0; i--)
            {
                record = Cache.Grids[index]; int update = record.Updates[i]; bool referenced = false;
                for (int b = 0; b < BatchBricks.Length; b++) if (BatchBricks[b].Update == update) { referenced = true; break; }
                for (int b = 0; b < BatchAssemblies.Length && !referenced; b++) if (BatchAssemblies[b].Update == update) referenced = true;
                if (referenced)
                {
                    // Finalization retires these results instead of applying stale geometry to the rebuilt record (Q23).
                    var old = Cache.Updates[update]; old.State = UpdateState.Discarded; Cache.Updates[update] = old;
                    record.Updates.RemoveAt(i); Cache.Grids[index] = record;
                }
                else ColliderPipeline.FreeUpdate(ref Cache, update);
            }
        }
    }
}
