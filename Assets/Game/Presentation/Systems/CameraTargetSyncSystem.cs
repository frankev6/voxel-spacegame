using Game.Core;
using Game.Core.Systems;
using Game.Galaxy.Components;
using Game.Presentation.Components;
using Game.Presentation.Services;
using Game.Presentation.Vfx;
using Game.Simulation.Commands;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace Game.Presentation.Systems
{
    /// <summary>Managed camera bridge and sole MainCameraState writer (SPEC-62 §5.1).</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    [UpdateAfter(typeof(FxUploadSystem))]
    [UpdateBefore(typeof(BrickPoolRenderSystem))]
    [UpdateAfter(typeof(Game.Presentation.Audio.AudioEventDrainSystem))]
    // HudSnapshotSystem runs after this system and HudSyncSystem after that snapshot.
    public partial class CameraTargetSyncSystem : SystemBase
    {
        static readonly ProfilerMarker TargetMarker = new ProfilerMarker(ProfilerCategory.Scripts, "CameraTargetSyncSystem.ReadTarget");
        static readonly ProfilerMarker OriginMarker = new ProfilerMarker(ProfilerCategory.Scripts, "CameraTargetSyncSystem.Origin");
        static readonly ProfilerMarker OcclusionMarker = new ProfilerMarker(ProfilerCategory.Scripts, "CameraTargetSyncSystem.Occlusion");
        static readonly ProfilerMarker ShakeMarker = new ProfilerMarker(ProfilerCategory.Scripts, "CameraTargetSyncSystem.Shake");
        EntityQuery players, viewers, renderCameras, origins, physics;
        double3 lastOrigin;
        bool hasLastOrigin;
        Entity viewEntity, lastTarget;
        float3 lastTargetPos;
        CameraRigService rig;
        IFxFrameReader fx;

        protected override void OnCreate()
        {
            viewEntity = EntityManager.CreateSingleton<MainCameraState>();
            physics = GetEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            origins = GetEntityQuery(ComponentType.ReadOnly<FrameOrigin>());
            players = GetEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalToWorld>());
            viewers = GetEntityQuery(ComponentType.ReadWrite<VoxelViewer>());
            renderCameras = GetEntityQuery(ComponentType.ReadWrite<VoxelRenderCamera>());
            RequireForUpdate<MainCameraState>();
        }
        public void Bind(CameraRigService rig, IFxFrameReader fx)
        {
            this.rig = rig; this.fx = fx; lastTarget = Entity.Null; hasLastOrigin = false;
        }
        protected override void OnUpdate()
        {
            if (rig == null || rig.Camera == null) return;
            float dt = math.clamp(UnityEngine.Time.unscaledDeltaTime, 0, .1f);
            float3 warpDelta = 0;
            using (OriginMarker.Auto())
            {
                if (origins.IsEmptyIgnoreFilter) hasLastOrigin = false;
                else
                {
                    EntityManager.CompleteDependencyBeforeRO<FrameOrigin>();
                    var current = origins.GetSingleton<FrameOrigin>().OriginInStarSystem;
                    if (hasLastOrigin && math.any(current != lastOrigin))
                    {
                        warpDelta = (float3)(lastOrigin - current);
                        rig.Warp(warpDelta);
                    }
                    lastOrigin = current; hasLastOrigin = true;
                }
            }
            using (TargetMarker.Auto())
            {
                int count = players.CalculateEntityCount();
                if (count == 0) { rig.ClearTarget(); rig.SetBoost(false); lastTarget = Entity.Null; }
                else
                {
                    Entity target;
                    if (count == 1) target = players.GetSingletonEntity();
                    else
                    {
                        using var entities = players.ToEntityArray(Allocator.Temp);
                        target = entities[0];
                        for (int i = 1; i < entities.Length; i++) if (entities[i].Index < target.Index) target = entities[i];
                        GameLog.WarnOnce("camera.multiplayership", "Camera", "Multiple player ships; using the lowest entity index.");
                    }
                    var ltw = EntityManager.GetComponentData<LocalToWorld>(target);
                    float3 pos = ltw.Position;
                    var rotation = new quaternion(math.orthonormalize(new float3x3(ltw.Value)));
                    float3 velocity = EntityManager.HasComponent<PhysicsVelocity>(target) ? EntityManager.GetComponentData<PhysicsVelocity>(target).Linear : float3.zero;
                    float radius = 10; float3 boundsMin = 0, boundsMax = 0;
                    bool hasBounds = EntityManager.HasComponent<VoxelGrid>(target);
                    if (hasBounds)
                    {
                        var grid = EntityManager.GetComponentData<VoxelGrid>(target);
                        boundsMin = (float3)grid.CellMin * grid.CellSize;
                        boundsMax = ((float3)grid.CellMax + 1) * grid.CellSize;
                        radius = math.max(2, .5f * math.length(boundsMax - boundsMin));
                    }
                    bool cut = target != lastTarget || math.distance(pos, lastTargetPos + warpDelta) > rig.Config.TeleportCutDistance;
                    rig.SetTarget(pos, rotation, velocity, radius, cut);
                    // QA-001: boost gets a visible response (FOV kick); the command flag is what the player asked for.
                    rig.SetBoost(EntityManager.HasComponent<ShipCommand>(target) &&
                        (EntityManager.GetComponentData<ShipCommand>(target).Flags & ShipCommandFlags.Boost) != 0);
                    if (hasBounds) rig.SetTargetBounds(boundsMin, boundsMax, ltw.Value);
                    lastTarget = target; lastTargetPos = pos;
                }
            }
            using (ShakeMarker.Auto())
            {
                if (rig.Mode == Data.CameraMode.Flight && fx != null && rig.Config.ShakeRules != null)
                {
                    var events = fx.Current;
                    int count = math.min(256, events.Length);
                    var rules = rig.Config.ShakeRules;
                    // Peak hold (QA-001): one collision emits a contact event per tick and pair, so summing every event
                    // pinned trauma at one. The frame's strongest falloff-weighted impulse raises trauma to at least itself.
                    float strongest = 0; float3 focus = rig.Focus;
                    for (int i = 0; i < count; i++)
                    {
                        var e = events[i];
                        for (int j = 0; j < rules.Length; j++)
                            if (rules[j].Kind == e.Kind)
                            {
                                float radius = rules[j].RadiusMeters;
                                float value = radius > 0 ? rules[j].Strength * math.max(e.Scale, 0) * math.saturate(1 - math.distance(e.Pos, focus) / radius) : 0;
                                if (math.isfinite(value) && value > strongest) strongest = value;
                                break;
                            }
                    }
                    if (strongest > rig.Trauma) rig.AddImpulse(focus, strongest - rig.Trauma, 1);
                }
            }
            using (OcclusionMarker.Auto())
            {
                float limit = float.PositiveInfinity;
                if (rig.Mode == Data.CameraMode.Flight && rig.HasTarget && !physics.IsEmptyIgnoreFilter)
                {
                    CompleteDependency(); EntityManager.CompleteDependencyBeforeRO<PhysicsWorldSingleton>();
                    float3 start = rig.Focus, end = rig.DesiredFlightPosition();
                    var ray = new RaycastInput { Start = start, End = end, Filter = PhysicsLayers.CameraOcclusionQuery };
                    var collector = new IgnoreEntityClosestHitCollector(lastTarget);
                    var collision = physics.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
                    if (collision.CastRay(ray, ref collector))
                        limit = math.max(rig.Config.OcclusionMinDistance, collector.ClosestHit.Fraction * math.distance(start, end) - rig.Config.OcclusionMargin);
                }
                rig.SetOcclusionDistance(limit);
            }
            rig.Tick(dt);
            var camera = rig.Camera;
            if (!renderCameras.IsEmptyIgnoreFilter) renderCameras.SetSingleton(new VoxelRenderCamera { Value = camera });
            EntityManager.SetComponentData(viewEntity, new MainCameraState
            {
                Position = camera.transform.position, Rotation = camera.transform.rotation,
                VerticalFovDeg = camera.fieldOfView, Aspect = camera.aspect,
                NearClip = camera.nearClipPlane, FarClip = camera.farClipPlane,
                ScreenSizePx = new float2(camera.pixelWidth, camera.pixelHeight),
                ViewProjection = camera.projectionMatrix * camera.worldToCameraMatrix,
                Mode = rig.Mode, FrameIndex = (uint)UnityEngine.Time.frameCount
            });
            if (!viewers.IsEmptyIgnoreFilter)
                viewers.SetSingleton(new VoxelViewer
                {
                    CameraPosition = camera.transform.position, AnchorPosition = rig.HasTarget ? rig.TargetPosition : rig.Focus,
                    Valid = (byte)(rig.Mode == Data.CameraMode.Flight && rig.HasTarget ? 1 : 0)
                });
        }
    }
}
