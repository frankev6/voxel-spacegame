using System;
using System.Collections.Generic;
using Game.App.DevRuntime;
using Game.Core;
using Game.Core.Components;
using Game.Core.Settings;
using Game.Data.Definitions;
using Game.Input;
using Game.Presentation.Components;
using Game.Presentation.Data;
using Game.Presentation.Input;
using Game.Presentation.Services;
using Game.Presentation.Vfx;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.DevTools
{
    /// <summary>Small isolated sandbox: real input sampling, command application, firing and FX drain; direct updates only.</summary>
    internal sealed class CombatCommandFixture : IDisposable
    {
        sealed class ServiceSlot<T> : IDisposable where T : class
        {
            readonly T previous, current;
            internal ServiceSlot(T value)
            {
                current = value; Services.TryGet(out previous);
                if (previous != null) Services.Unregister(previous);
                Services.Register(current);
            }
            public void Dispose() { Services.Unregister(current); if (previous != null) Services.Register(previous); }
        }
        internal readonly EcsTestWorld Test;
        internal EntityManager Em => Test.EntityManager;
        internal readonly DevRuntimeHost Host;
        internal readonly InputService Input;
        internal readonly CameraRigService Rig;
        internal readonly Entity Player, Hostile;
        internal Entity Mount;
        internal double Now = Time.unscaledTimeAsDouble;
        readonly List<IDisposable> services = new();
        readonly DevRuntimeHost previousHost;
        readonly GameObject prefab;
        readonly CameraConfig config;
        BlobAssetReference<WeaponDefsBlob> weapons;
        FactionRelations relations;
        internal CombatCommandFixture(bool firing = false)
        {
            InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>();
            var systems = new List<Type> { typeof(PlayerInputSampleSystem), typeof(PlayerCommandApplySystem) };
            if (firing) systems.AddRange(new[] { typeof(WeaponFireSystem), typeof(FxEventQueueCreateSystem), typeof(SfxEventQueueCreateSystem), typeof(FxUploadSystem) });
            Test = EcsTestWorld.Create(systems.ToArray()); Test.AddGameplayActive(); Test.AddWorldSceneActive(); Test.AddSimClock();
            Player = Grid(1, float3.zero, 0); Em.AddComponent<PlayerShip>(Player); Em.AddComponent<ShipCommand>(Player);
            Hostile = Grid(20, new float3(0, 0, 3), 1);
            Input = new InputService(new ControlSettingsData()); Input.SetContext(InputContext.Flight);
            Test.World.GetExistingSystemManaged<PlayerInputSampleSystem>().Bind(Input, null);
            prefab = new GameObject("CombatCommandTestCamera"); prefab.AddComponent<Camera>().enabled = false;
            config = ScriptableObject.CreateInstance<CameraConfig>(); config.CameraPrefab = prefab;
            Rig = new CameraRigService(config, null, null);
            Rig.Camera.pixelRect = new Rect(0, 0, 640, 480);
            Rig.Camera.transform.SetPositionAndRotation(new Vector3(0, 20, -20), Quaternion.LookRotation(new Vector3(0, -20, 20)));
            Em.CreateSingleton(new MainCameraState { ScreenSizePx = new float2(640, 480),
                ViewProjection = (float4x4)(Rig.Camera.projectionMatrix * Rig.Camera.worldToCameraMatrix) });
            services.Add(new ServiceSlot<InputService>(Input)); services.Add(new ServiceSlot<CameraRigService>(Rig));
            Services.TryGet(out previousHost); if (previousHost != null) Services.Unregister(previousHost);
            Host = DevRuntimeHost.Create(null); Host.enabled = false;
            if (firing)
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var blob = ref builder.ConstructRoot<WeaponDefsBlob>();
                var rows = builder.Allocate(ref blob.Weapons, 2);
                rows[1] = new WeaponDef { Index = 1, Delivery = WeaponDelivery.Projectile, Damage = 1,
                    TriggerIntervalTicks = 1, BurstCount = 1, PelletsPerRound = 1 };
                weapons = builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.Persistent);
                relations = FactionRelations.CreateDefault(Allocator.Persistent);
                Em.CreateSingleton(new WeaponDefsRef { Value = weapons }); Em.CreateSingleton(relations); Em.CreateSingleton(new WeaponTuning());
                Em.AddComponent<WeaponControl>(Player); Em.AddComponent<ShipTarget>(Player);
                Mount = Em.CreateEntity(typeof(FunctionalBlock), typeof(WeaponMount), typeof(WeaponAim), typeof(WeaponState));
                Em.SetComponentData(Mount, new FunctionalBlock { Grid = Player });
                Em.SetComponentData(Mount, new WeaponMount { Initialized = 1, WeaponIndex = 1, ForwardLocal = new float3(0, 0, 1), UpLocal = new float3(0, 1, 0) });
                Em.SetComponentData(Mount, new WeaponAim { HasAim = 1, Aligned = 1, InRange = 1, FireDirLocal = new float3(0, 0, 1) });
                Em.SetComponentData(Mount, new WeaponState { Group = 1 });
            }
        }
        internal Entity Grid(ulong id, float3 position, byte team)
        {
            var entity = Em.CreateEntity(typeof(VoxelGrid), typeof(LocalTransform), typeof(PersistentId), typeof(Allegiance));
            Em.SetComponentData(entity, new VoxelGrid { CellSize = .5f, Kind = GridKind.Ship });
            Em.SetComponentData(entity, LocalTransform.FromPosition(position)); Em.SetComponentData(entity, new PersistentId(id));
            Em.SetComponentData(entity, new Allegiance { Team = team, Faction = 255 }); return entity;
        }
        internal DevCommandResult Command(string text) => Host.Commands.Execute(text, Host.Context);
        internal void AssertMouse(Entity target)
        {
            var grid = Em.GetComponentData<VoxelGrid>(target);
            var center = Em.GetComponentData<LocalTransform>(target).TransformPoint(((float3)grid.CellMin + (float3)(grid.CellMax + 1)) * (.5f * grid.CellSize));
            Vector3 screen = Rig.Camera.WorldToScreenPoint(center);
            Assert.Less(Vector2.Distance(new Vector2(screen.x, screen.y), Host.Input.MouseDevice.position.ReadValue()), 2);
        }
        public void Dispose()
        {
            Em.CompleteAllTrackedJobs(); Host.Dispose();
            if (previousHost != null) Services.Register(previousHost);
            for (int i = services.Count - 1; i >= 0; i--) services[i].Dispose();
            Test.World.GetExistingSystemManaged<PlayerInputSampleSystem>().Unbind();
            Input.Dispose(); Rig.Dispose(); Test.Dispose();
            if (weapons.IsCreated) weapons.Dispose(); if (relations.Stance.IsCreated) relations.Dispose();
            Object.DestroyImmediate(prefab); Object.DestroyImmediate(config);
        }
    }
}
