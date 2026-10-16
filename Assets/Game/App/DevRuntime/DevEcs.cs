#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.Text;
using Game.Core.Components;
using Game.Galaxy.Components;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.GraphicsIntegration;
using Unity.Transforms;

namespace Game.App.DevRuntime
{
    /// <summary>SPEC-70 authorized dev teleport and read-only grid inspection, between world updates.</summary>
    public static class DevEcs
    {
        public static void TeleportRoot(EntityManager em, Entity root, float3 pos, quaternion rot)
        {
            em.CompleteAllTrackedJobs();
            if (!em.Exists(root) || !em.HasComponent<LocalTransform>(root)) throw new InvalidOperationException("player transform unavailable");
            if (!math.all(math.isfinite(pos)) || !math.all(math.isfinite(rot.value))) throw new ArgumentException("teleport pose must be finite");
            using var originQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FrameOrigin>());
            if (!originQuery.TryGetSingleton<FrameOrigin>(out var frame)) throw new InvalidOperationException("frame origin unavailable");
            var pose = em.GetComponentData<LocalTransform>(root); pose.Position = pos; pose.Rotation = rot;
            em.SetComponentData(root, pose);
            if (em.HasComponent<UniversePosition>(root)) em.SetComponentData(root, new UniversePosition { Value = frame.OriginInStarSystem + (double3)pos });
            if (em.HasComponent<PhysicsVelocity>(root)) em.SetComponentData(root, default(PhysicsVelocity));
            if (em.HasComponent<PhysicsGraphicalInterpolationBuffer>(root)) em.SetComponentData(root,
                new PhysicsGraphicalInterpolationBuffer { PreviousTransform = new RigidTransform(rot, pos), PreviousVelocity = default });
            if (em.HasComponent<PhysicsGraphicalSmoothing>(root)) em.SetComponentData(root,
                new PhysicsGraphicalSmoothing { CurrentVelocity = default, ApplySmoothing = 0 });
        }
        public static bool TryGetUniversePosition(DevContext ctx, Entity root, out double3 position)
        {
            position = default;
            var em = ctx.EntityManager;
            if (root == Entity.Null || !em.Exists(root)) return false;
            if (em.HasComponent<UniversePosition>(root)) { position = em.GetComponentData<UniversePosition>(root).Value; return true; }
            if (!em.HasComponent<LocalTransform>(root) || !ctx.TryGetFrame(out var frame)) return false;
            position = frame.OriginInStarSystem + (double3)em.GetComponentData<LocalTransform>(root).Position;
            return true;
        }
        public static string Format(double3 v) => "(" + v.x.ToString("R", CultureInfo.InvariantCulture) + ", " +
            v.y.ToString("R", CultureInfo.InvariantCulture) + ", " + v.z.ToString("R", CultureInfo.InvariantCulture) + ")";
        public static string Format(float3 v) => Format((double3)v);
        public static int SolidCells(EntityManager em, Entity root)
        {
            int count = 0;
            if (!em.HasBuffer<BrickSlot>(root)) return count;
            foreach (var slot in em.GetBuffer<BrickSlot>(root, true))
                if (em.Exists(slot.Brick) && em.HasComponent<Brick>(slot.Brick)) count += em.GetComponentData<Brick>(slot.Brick).SolidCount;
            return count;
        }
        public static Entity FindGrid(DevContext ctx, ulong id)
        {
            if (id == 0 || ctx.World == null || !ctx.World.IsCreated) return Entity.Null;
            using var roots = ctx.GridEntities();
            foreach (var entity in roots)
                if (ctx.EntityManager.HasComponent<PersistentId>(entity) && ctx.EntityManager.GetComponentData<PersistentId>(entity).Value == id) return entity;
            return Entity.Null;
        }
        public static Entity NearestGrid(DevContext ctx)
        {
            Entity player = ctx.PlayerShipRoot;
            if (player == Entity.Null || !ctx.EntityManager.HasComponent<LocalTransform>(player)) return Entity.Null;
            var em = ctx.EntityManager; var position = em.GetComponentData<LocalTransform>(player).Position;
            Entity found = Entity.Null; double best = double.PositiveInfinity; ulong bestId = ulong.MaxValue;
            using var roots = ctx.GridEntities();
            foreach (var root in roots)
            {
                if (root == player || !em.HasComponent<LocalTransform>(root) || !em.HasComponent<PersistentId>(root)) continue;
                double distance = math.lengthsq((double3)em.GetComponentData<LocalTransform>(root).Position - (double3)position);
                ulong id = em.GetComponentData<PersistentId>(root).Value;
                if (distance < best || (distance == best && id < bestId)) { found = root; best = distance; bestId = id; }
            }
            return found;
        }
        public static string DescribeGrid(DevContext ctx, Entity root, bool bricks)
        {
            var em = ctx.EntityManager; em.CompleteAllTrackedJobs();
            if (root == Entity.Null || !em.Exists(root) || !em.HasComponent<VoxelGrid>(root)) throw new InvalidOperationException("grid unavailable");
            var grid = em.GetComponentData<VoxelGrid>(root); var sb = new StringBuilder();
            int brickCount = 0, meshDirty = 0, colliderDirty = 0, bothDirty = 0, meshes = 0, instances = 0;
            var lines = bricks ? new StringBuilder() : null;
            if (em.HasBuffer<BrickSlot>(root))
                foreach (var slot in em.GetBuffer<BrickSlot>(root, true))
                {
                    if (!em.Exists(slot.Brick) || !em.HasComponent<Brick>(slot.Brick)) continue;
                    var brick = em.GetComponentData<Brick>(slot.Brick); brickCount++;
                    var flags = em.HasComponent<BrickDirty>(slot.Brick) && em.IsComponentEnabled<BrickDirty>(slot.Brick)
                        ? em.GetComponentData<BrickDirty>(slot.Brick).Flags : BrickDirtyFlags.None;
                    bool mesh = (flags & BrickDirtyFlags.Mesh) != 0, collider = (flags & BrickDirtyFlags.Collider) != 0;
                    if (mesh) meshDirty++; if (collider) colliderDirty++; if (mesh && collider) bothDirty++;
                    bool hasMesh = em.HasComponent<BrickRenderState>(slot.Brick) && em.GetComponentData<BrickRenderState>(slot.Brick).HasMesh != 0;
                    if (hasMesh) meshes++;
                    if (bricks && brickCount <= 200) lines.AppendLine($"brick {brick.Coord}: solid={brick.SolidCount} dirty={flags} mesh={hasMesh}");
                }
            if (em.HasBuffer<BlockInstance>(root)) foreach (var instance in em.GetBuffer<BlockInstance>(root, true)) if (instance.Block != 0 && instance.CellsAlive != 0) instances++;
            var velocity = em.HasComponent<PhysicsVelocity>(root) ? em.GetComponentData<PhysicsVelocity>(root) : default;
            string mass = "unavailable";
            if (em.HasComponent<PhysicsMass>(root))
            {
                float inverse = em.GetComponentData<PhysicsMass>(root).InverseMass;
                mass = inverse == 0 ? "infinite" : (1.0 / inverse).ToString("R", CultureInfo.InvariantCulture);
            }
            sb.AppendLine("PersistentId=" + (em.HasComponent<PersistentId>(root) ? em.GetComponentData<PersistentId>(root).Value.ToString(CultureInfo.InvariantCulture) : "unavailable"));
            sb.AppendLine($"Entity={root} GridKind={grid.Kind} Hulk={em.HasComponent<Hulk>(root)} Dormant={em.HasComponent<Dormant>(root)}");
            sb.AppendLine($"CellSize={grid.CellSize.ToString("R", CultureInfo.InvariantCulture)} BrickMin={grid.BrickMin} BrickDims={grid.BrickDims} Revision={grid.Revision}");
            sb.AppendLine($"bricks={brickCount} solid cells={SolidCells(em, root)} block instances={instances}");
            sb.AppendLine($"dirty bricks: mesh={meshDirty} collider={colliderDirty} both={bothDirty}; applied meshes={meshes}");
            sb.AppendLine("collider=" + Collider(em, root) + " mass=" + mass);
            sb.AppendLine("linear speed=" + math.length(velocity.Linear).ToString("R", CultureInfo.InvariantCulture) +
                " angular speed=" + math.length(velocity.Angular).ToString("R", CultureInfo.InvariantCulture));
            sb.AppendLine("UniversePosition=" + (TryGetUniversePosition(ctx, root, out var up) ? Format(up) : "unavailable"));
            if (bricks) { sb.Append(lines); if (brickCount > 200) sb.AppendLine("brick detail truncated at 200"); }
            return sb.ToString().TrimEnd();
        }
        internal static unsafe string Collider(EntityManager em, Entity root)
        {
            if (!em.HasComponent<PhysicsCollider>(root)) return "none";
            var blob = em.GetComponentData<PhysicsCollider>(root).Value;
            if (!blob.IsCreated) return "none";
            if (blob.Value.Type == ColliderType.Compound) return "compound " + ((CompoundCollider*)blob.GetUnsafePtr())->NumChildren + " children";
            if (blob.Value.Type == ColliderType.Box) return "provisional box";
            return blob.Value.Type.ToString();
        }
    }
}
#endif
