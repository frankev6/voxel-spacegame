using System;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>Feel prototype (owner, 2026-10-21): with auto-aim on, an aim point on a moving ship is led like a locked target.</summary>
    public sealed class AimAssistTests
    {
        [Test] public void AimOnAMovingShip_IsLed()
        {
            using var f = new Fixture();
            f.Aim(new float3(0, 0, 100), ShipCommandFlags.AimAssist);
            // speed 100 m/s over 100 m: about 1 s of flight, so a 20 m/s crossing target is led ~20 m, about 11°
            Assert.AreEqual(math.degrees(math.atan2(20, 100)), math.degrees(f.DesiredYaw), 1.5f);
        }

        [Test] public void WithoutAssist_TheAimPointIsUsedAsIs()
        {
            using var f = new Fixture();
            f.Aim(new float3(0, 0, 100), ShipCommandFlags.None);
            Assert.AreEqual(0, math.degrees(f.DesiredYaw), 1e-3f);
        }

        [Test] public void AimInOpenSpace_IsNotLed()
        {
            using var f = new Fixture();
            f.Aim(new float3(0, 0, 60), ShipCommandFlags.AimAssist);
            Assert.AreEqual(0, math.degrees(f.DesiredYaw), 1e-3f);
        }

        sealed class Fixture : IDisposable
        {
            readonly World world = new World("Aim assist");
            readonly SystemHandle hash, aim;
            readonly BlobAssetReference<WeaponDefsBlob> weapons;
            readonly Entity root, mount;
            EntityManager Em => world.EntityManager;
            public float DesiredYaw => Em.GetComponentData<WeaponAim>(mount).DesiredYaw;

            public Fixture()
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var blob = ref builder.ConstructRoot<WeaponDefsBlob>();
                var rows = builder.Allocate(ref blob.Weapons, 2);
                rows[1] = new WeaponDef { Index = 1, Delivery = WeaponDelivery.Projectile, TraverseHalfArcRad = math.radians(45),
                    TraverseRateRad = math.radians(60), MaxElevationRad = math.radians(20), FireConeRad = math.radians(3),
                    ProjectileSpeed = 100, LifetimeMaxTicks = 600, Range = 1000 };
                weapons = builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.Persistent);
                Em.CreateEntity(typeof(GameplayActive)); Em.CreateSingleton(default(SimClock));
                Em.CreateSingleton(new WeaponDefsRef { Value = weapons });
                Em.CreateSingleton(new WeaponTuning { FixedConvergenceRad = math.radians(3), MaxTargetRange = 1000, UnderCursorRadius = 30, MaxCandidates = 4 });
                Em.AddBuffer<RootComponentRegistration>(Em.CreateEntity()); Em.CreateSingleton(default(OriginShiftRecord));
                hash = world.CreateSystem<SpatialHashBuildSystem>();
                aim = world.CreateSystem<WeaponAimSystem>();
                root = Em.CreateEntity(typeof(LocalTransform), typeof(PhysicsVelocity), typeof(ShipCommand), typeof(WeaponControl), typeof(ShipTarget));
                Em.SetComponentData(root, LocalTransform.Identity);
                // the target: a small ship 100 m ahead, crossing at 20 m/s
                var target = Em.CreateEntity(typeof(VoxelGrid), typeof(LocalTransform), typeof(Allegiance), typeof(PhysicsVelocity));
                Em.SetComponentData(target, new VoxelGrid { CellMin = new int3(-20), CellMax = new int3(19), CellSize = .25f, Kind = GridKind.Ship });
                Em.SetComponentData(target, LocalTransform.FromPosition(0, 0, 100));
                Em.SetComponentData(target, new Allegiance { Faction = 1 });
                Em.SetComponentData(target, new PhysicsVelocity { Linear = new float3(20, 0, 0) });
                mount = Em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponAim), typeof(WeaponState));
                Em.SetComponentData(mount, new FunctionalBlock { Grid = root });
                Em.SetComponentData(mount, new WeaponMount { Initialized = 1, WeaponIndex = 1, ForwardLocal = new float3(0, 0, 1), UpLocal = new float3(0, 1, 0) });
            }

            public void Aim(float3 point, ShipCommandFlags flags)
            {
                Em.SetComponentData(root, new ShipCommand { AimValid = 1, AimWorld = point, Flags = flags });
                world.SetTime(new Unity.Core.TimeData(1d / 60, 1f / 60));
                hash.Update(world.Unmanaged);
                using (var query = Em.CreateEntityQuery(ComponentType.ReadOnly<SpatialHash>())) query.GetSingleton<SpatialHash>().CompleteAccess();
                Em.CompleteAllTrackedJobs();
                aim.Update(world.Unmanaged); Em.CompleteAllTrackedJobs();
            }

            public void Dispose() { Em.CompleteAllTrackedJobs(); world.Dispose(); weapons.Dispose(); }
        }
    }
}
