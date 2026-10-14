#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using PhysicsMath = Unity.Physics.Math;
namespace Game.App.DevRuntime.Overlays
{
    [BurstCompile]
    public unsafe struct ColliderOverlayJob : IJob
    {
        [ReadOnly] public NativeArray<Entity> Entities;
        [ReadOnly] public NativeArray<PhysicsCollider> Colliders;
        [ReadOnly] public NativeParallelHashMap<Entity, DevOverlayRoot> Roots;
        public DevLineBatch Lines;
        public void Execute()
        {
            for (int i = 0; i < Entities.Length; i++)
            {
                if (!Roots.TryGetValue(Entities[i], out var root) || !Colliders[i].IsValid) continue;
                var collider = Colliders[i].Value.GetUnsafePtr();
                var collector = new Boxes { Lines = Lines, RootTransform = root.Transform, ParentTransform = PhysicsMath.MTransform.Identity,
                    Color = ((Unity.Physics.Collider*)collider)->Type == ColliderType.Box ? new Color32(0, 255, 255, 255) : new Color32(0, 255, 0, 255) };
                if (((Unity.Physics.Collider*)collider)->Type == ColliderType.Box)
                {
                    var leaf = new ChildCollider((Unity.Physics.Collider*)collider, RigidTransform.identity);
                    collector.AddLeaf(ColliderKey.Empty, ref leaf);
                }
                else ((Unity.Physics.Collider*)collider)->GetLeaves(ref collector);
                if (Lines.Truncated) return;
            }
        }
        struct Boxes : ILeafColliderCollector
        {
            public DevLineBatch Lines;
            public float4x4 RootTransform;
            public PhysicsMath.MTransform ParentTransform;
            public Color32 Color;
            public void AddLeaf(ColliderKey key, ref ChildCollider leaf)
            {
                if (leaf.Collider->Type != ColliderType.Box || Lines.Truncated) return;
                var geometry = ((Unity.Physics.BoxCollider*)leaf.Collider)->Geometry;
                var parent = new float4x4(ParentTransform.Rotation, ParentTransform.Translation);
                var transform = math.mul(math.mul(RootTransform, parent), new float4x4(leaf.TransformFromChild));
                transform = math.mul(transform, float4x4.TRS(geometry.Center, geometry.Orientation, new float3(1)));
                Lines.Box(-geometry.Size * .5f, geometry.Size * .5f, transform, Color);
            }
            public void PushCompositeCollider(ColliderKeyPath key, PhysicsMath.MTransform parentFromComposite, out PhysicsMath.MTransform worldFromParent)
            { worldFromParent = ParentTransform; ParentTransform = PhysicsMath.Mul(ParentTransform, parentFromComposite); }
            public void PopCompositeCollider(uint bits, PhysicsMath.MTransform worldFromParent) => ParentTransform = worldFromParent;
        }
    }
}
#endif
