using System;
using System.Collections.Generic;
using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Services;
using Game.Presentation.Components;
using Game.Presentation.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace Game.Presentation.World
{
    /// <summary>SPEC-31 §5.9/ADR-0087: shared registered definitions, loader-created children, render-only compression.</summary>
    [CreateAfter(typeof(EntitiesGraphicsSystem))]
    [UpdateInGroup(typeof(PresentationBridgeGroup)), UpdateAfter(typeof(CameraTargetSyncSystem))]
    public partial class CelestialVisualSystem : SystemBase, ICelestialVisualFactory
    {
        sealed class Registration
        {
            internal BatchMeshID Mesh;
            internal BatchMaterialID Material;
            internal Entity Prototype;
        }
        static readonly ProfilerMarker Register = new(ProfilerCategory.Scripts, "CelestialVisualSystem.Register");
        static readonly ProfilerMarker Gather = new(ProfilerCategory.Scripts, "CelestialVisualSystem.Gather");
        static readonly ProfilerMarker Compress = new(ProfilerCategory.Scripts, "CelestialVisualSystem.Compress");
        readonly Dictionary<BodyVisualDefinition, Registration> definitions = new();
        EntitiesGraphicsSystem graphics;
        EntityQuery anchors, children;
        ComponentLookup<CelestialVisual> visuals;
        EntityStorageInfoLookup storage;
        internal int RegisteredDefinitionCount => definitions.Count;
        protected override void OnCreate()
        {
            anchors = GetEntityQuery(ComponentType.ReadOnly<StarSystemBody>(), ComponentType.ReadOnly<LocalTransform>());
            children = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<CelestialVisual>(),
                ComponentType.ReadWrite<LocalTransform>(), ComponentType.ReadWrite<LocalToWorld>(), ComponentType.ReadWrite<MaterialMeshInfo>() },
                Options = EntityQueryOptions.IgnoreComponentEnabledState });
            visuals = GetComponentLookup<CelestialVisual>(); storage = GetEntityStorageInfoLookup();
            RequireForUpdate<MainCameraState>(); RequireForUpdate<StreamingConfig>();
            RequireForUpdate(children);
        }
        Registration RegisterDefinition(BodyVisualDefinition definition)
        {
            using var marker = Register.Auto();
            var result = new Registration();
            try
            {
                result.Mesh = graphics.RegisterMesh(definition.Mesh);
                result.Material = graphics.RegisterMaterial(definition.Material);
                if (result.Mesh.Equals(BatchMeshID.Null) || result.Material.Equals(BatchMaterialID.Null))
                    throw new InvalidOperationException("Celestial definition could not register mesh/material: " + definition.IdString);
                result.Prototype = BuildPrototype(definition, result);
                definitions.Add(definition, result); return result;
            }
            catch
            {
                if (EntityManager.Exists(result.Prototype)) EntityManager.DestroyEntity(result.Prototype);
                if (!result.Material.Equals(BatchMaterialID.Null)) graphics.UnregisterMaterial(result.Material);
                if (!result.Mesh.Equals(BatchMeshID.Null)) graphics.UnregisterMesh(result.Mesh);
                throw;
            }
        }
        Entity BuildPrototype(BodyVisualDefinition definition, Registration registration)
        {
            var em = EntityManager;
            var prototype = em.CreateEntity(typeof(Prefab), typeof(Parent), typeof(LocalTransform), typeof(LocalToWorld),
                typeof(CelestialVisual), typeof(URPMaterialPropertyBaseColor), typeof(URPMaterialPropertyEmissionColor));
            try
            {
                RenderMeshUtility.AddComponents(prototype, em,
                    new RenderMeshDescription(definition.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows: true),
                    new MaterialMeshInfo(registration.Material, registration.Mesh));
                var bounds = definition.Mesh.bounds;
                em.SetComponentData(prototype, new RenderBounds { Value = new AABB { Center = bounds.center, Extents = bounds.extents } });
                var color = definition.BaseColor; var emission = definition.EmissionColor;
                em.SetComponentData(prototype, new URPMaterialPropertyBaseColor { Value = new float4(color.r, color.g, color.b, color.a) });
                // Initial emission only. TRV-009 owns subsequent lane-gate emission; frame updates never touch it.
                em.SetComponentData(prototype, new URPMaterialPropertyEmissionColor { Value = new float4(emission.r, emission.g, emission.b, emission.a) });
                em.SetComponentData(prototype, new CelestialVisual { HideWithGrid = (byte)(definition.HideWhenGridPresent ? 1 : 0) });
                return prototype;
            }
            catch { if (em.Exists(prototype)) em.DestroyEntity(prototype); throw; }
        }
        public Entity Create(Entity anchor, BodyVisualDefinition definition, float radiusM)
        {
            if (definition == null || definition.Mesh == null || definition.Material == null) return Entity.Null;
            if (!math.isfinite(radiusM) || radiusM <= 0 || !math.isfinite(definition.MeshRadius) || definition.MeshRadius <= 0 ||
                !math.isfinite(radiusM / definition.MeshRadius)) throw new ArgumentOutOfRangeException(nameof(radiusM));
            if (!EntitiesGraphicsSystem.EntitiesGraphicsEnabled) return Entity.Null;
            graphics ??= World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null) return Entity.Null;
            var em = EntityManager; Dependency.Complete();
            if (!em.Exists(anchor) || !em.HasComponent<StarSystemBody>(anchor) || !em.HasComponent<LocalTransform>(anchor))
                throw new ArgumentException("Celestial visual requires a live body anchor.", nameof(anchor));
            if ((em.GetComponentData<StarSystemBody>(anchor).Flags & BodyFlags.Destroyed) != 0) return Entity.Null;
            if (!definitions.TryGetValue(definition, out var registration)) registration = RegisterDefinition(definition);
            // SceneFlow can remove non-boot prototypes; registered IDs remain World-owned and are reused.
            if (!em.Exists(registration.Prototype)) registration.Prototype = BuildPrototype(definition, registration);
            Entity child = Entity.Null;
            try
            {
                child = em.Instantiate(registration.Prototype);
                var state = em.GetComponentData<CelestialVisual>(child); var pose = em.GetComponentData<LocalTransform>(anchor);
                state.Anchor = anchor; state.BaseScale = radiusM / definition.MeshRadius;
                state.Position = pose.Position; state.Rotation = pose.Rotation; state.AnchorScale = pose.Scale;
                em.SetComponentData(child, state); em.SetComponentData(child, new Parent { Value = anchor });
                em.SetComponentData(child, LocalTransform.FromScale(state.BaseScale));
                em.SetComponentData(child, new LocalToWorld { Value = math.mul(pose.ToMatrix(), float4x4.Scale(state.BaseScale)) });
                return child;
            }
            catch { if (em.Exists(child)) em.DestroyEntity(child); throw; }
        }
        protected override void OnUpdate()
        {
            visuals.Update(this); storage.Update(this);
            using (Gather.Auto()) Dependency = new GatherJob { Visuals = visuals }.ScheduleParallel(anchors, Dependency);
            var camera = SystemAPI.GetSingleton<MainCameraState>(); var config = SystemAPI.GetSingleton<StreamingConfig>();
            using (Compress.Auto()) Dependency = new ApplyJob { Camera = camera.Position, MaxDistance = config.MaxRenderDistanceM,
                Storage = storage }.ScheduleParallel(children, Dependency);
        }
        [BurstCompile]
        partial struct GatherJob : IJobEntity
        {
            // The loader assigns exactly one distinct visual per anchor; each iteration writes its own child.
            [NativeDisableParallelForRestriction] public ComponentLookup<CelestialVisual> Visuals;
            void Execute(Entity anchor, in StarSystemBody body, in LocalTransform pose)
            {
                if (body.Visual == Entity.Null || !Visuals.HasComponent(body.Visual)) return;
                var state = Visuals[body.Visual]; if (state.Anchor != anchor) return;
                state.Position = pose.Position; state.Rotation = pose.Rotation; state.AnchorScale = pose.Scale; state.Flags = body.Flags;
                Visuals[body.Visual] = state;
            }
        }
        [BurstCompile]
        partial struct ApplyJob : IJobEntity
        {
            public float3 Camera; public float MaxDistance;
            [ReadOnly] public EntityStorageInfoLookup Storage;
            void Execute(ref LocalTransform local, ref LocalToWorld matrix, EnabledRefRW<MaterialMeshInfo> visible, in CelestialVisual state)
            {
                visible.ValueRW = Storage.Exists(state.Anchor) && (state.Flags & BodyFlags.Destroyed) == 0 &&
                    (state.HideWithGrid == 0 || (state.Flags & BodyFlags.HasGrid) == 0);
                float distance = math.distance(Camera, state.Position);
                float k = distance > MaxDistance ? MaxDistance / distance : 1;
                float3 rendered = Camera + (state.Position - Camera) * k;
                local = new LocalTransform { Position = math.rotate(math.conjugate(state.Rotation), rendered - state.Position) / state.AnchorScale,
                    Rotation = quaternion.identity, Scale = state.BaseScale * k };
                // Presentation follows TransformSystemGroup: publish this frame's matrix directly. WriteGroup excludes the default writer.
                matrix.Value = float4x4.TRS(rendered, state.Rotation, new float3(local.Scale * state.AnchorScale));
            }
        }
        protected override void OnDestroy()
        {
            Dependency.Complete();
            foreach (var registration in definitions.Values)
            {
                if (EntityManager.Exists(registration.Prototype)) EntityManager.DestroyEntity(registration.Prototype);
                if (graphics == null) continue;
                graphics.UnregisterMaterial(registration.Material); graphics.UnregisterMesh(registration.Mesh);
            }
            definitions.Clear();
        }
    }
}
