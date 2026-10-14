using System;
using Game.Presentation.Data;
using Game.Presentation.Services;
using Game.Presentation.Systems;
using Game.Simulation.Commands;
using Game.Tests.Common;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Presentation
{
    public sealed class CameraOcclusionTests
    {
        internal sealed class Fixture : IDisposable
        {
            internal readonly EcsTestWorld Test = EcsTestWorld.Create(typeof(CameraTargetSyncSystem));
            readonly GameObject prefab = new("Occlusion camera", typeof(Camera));
            readonly CameraConfig config = ScriptableObject.CreateInstance<CameraConfig>();
            internal readonly CameraRigService Rig;
            internal readonly CameraTargetSyncSystem Sync;
            internal readonly Entity Player;
            Entity physicsEntity;
            PhysicsWorld physics;
            BlobAssetReference<Unity.Physics.Collider> collider;
            bool hasPhysics;
            internal Fixture()
            {
                prefab.GetComponent<Camera>().enabled = false; config.CameraPrefab = prefab;
                Rig = new CameraRigService(config, null, null); Rig.SetMode(CameraMode.Flight, true);
                Player = Test.EntityManager.CreateEntity(typeof(PlayerShip), typeof(LocalToWorld));
                Test.EntityManager.SetComponentData(Player, new LocalToWorld { Value = float4x4.identity });
                Sync = Test.World.GetExistingSystemManaged<CameraTargetSyncSystem>(); Sync.Bind(Rig, null); Sync.Update();
            }
            internal float Distance => math.distance(Rig.Focus, (float3)Rig.Camera.transform.position);
            internal void Box(bool own = false)
            {
                float3 direction = math.normalize(Rig.DesiredFlightPosition() - Rig.Focus);
                collider = Unity.Physics.BoxCollider.Create(new BoxGeometry { Size = new float3(2), Orientation = quaternion.identity }, PhysicsLayers.Grid);
                physics = new PhysicsWorld(1, 0, 0); hasPhysics = true;
                var bodies = physics.StaticBodies;
                bodies[0] = new RigidBody { Entity = own ? Player : Test.EntityManager.CreateEntity(), Collider = collider,
                    WorldFromBody = new RigidTransform(quaternion.LookRotationSafe(direction, math.up()), Rig.Focus + direction * 60), Scale = 1 };
                var build = physics; build.CollisionWorld.BuildBroadphase(ref build, 1f / 60, float3.zero);
                physicsEntity = Test.EntityManager.CreateSingleton(new PhysicsWorldSingleton { PhysicsWorld = physics });
            }
            internal void RemoveBox() { if (physicsEntity != Entity.Null) { Test.EntityManager.DestroyEntity(physicsEntity); physicsEntity = Entity.Null; } }
            public void Dispose()
            {
                RemoveBox(); Test.Dispose(); if (hasPhysics) physics.Dispose(); if (collider.IsCreated) collider.Dispose();
                Rig.Dispose(); Object.DestroyImmediate(prefab); Object.DestroyImmediate(config);
            }
        }
        [Test] public void BoxBetween_PullsIn()
        { using var f = new Fixture(); f.Box(); f.Sync.Update(); Assert.AreEqual(58, f.Distance, .01f); Assert.AreEqual(150, f.Rig.CurrentDistance, .01f); }
        [Test] public void BoxRemoved_ReleasesSmoothly()
        {
            using var f = new Fixture(); f.Box(); f.Sync.Update(); Assert.AreEqual(58, f.Distance, .01f);
            f.RemoveBox(); f.Sync.Update(); float before = f.Distance; Assert.Less(before, 150);
            for (int i = 0; i < 3; i++) f.Rig.Tick(.1f); Assert.AreEqual((before + 150) * .5f, f.Distance, .02f);
        }
        [Test] public void OwnGrid_Ignored()
        { using var f = new Fixture(); f.Box(true); f.Sync.Update(); Assert.AreEqual(150, f.Distance, .01f); }
        [Test] public void NoPhysicsWorld_NoOcclusion()
        { using var f = new Fixture(); f.Sync.Update(); Assert.AreEqual(150, f.Distance, .01f); }
    }
}
