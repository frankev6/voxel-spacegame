using Game.Core;
using Game.Core.Systems;
using Game.Presentation.Components;
using Game.Presentation.Services;
using Game.Simulation.Commands;
using Game.Voxel.Model;
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
    [UpdateBefore(typeof(BrickPoolRenderSystem))]
    // TODO(AUD-006): UpdateAfter AudioEventDrainSystem when that system exists.
    // TODO(HUD-002): UpdateBefore HudSyncSystem when that system exists.
    public partial class CameraTargetSyncSystem : SystemBase
    {
        static readonly ProfilerMarker TargetMarker = new ProfilerMarker(ProfilerCategory.Scripts, "CameraTargetSyncSystem.ReadTarget");
        // TODO(CAM-006): Origin marker with origin warp.
        // TODO(CAM-007): Occlusion marker with the physics ray.
        // TODO(CAM-008): Shake marker with typed FX input.
        EntityQuery players, viewers, renderCameras;
        Entity viewEntity, lastTarget;
        float3 lastTargetPos;
        CameraRigService rig;

        protected override void OnCreate()
        {
            viewEntity = EntityManager.CreateSingleton<MainCameraState>();
            players = GetEntityQuery(ComponentType.ReadOnly<PlayerShip>(), ComponentType.ReadOnly<LocalToWorld>());
            viewers = GetEntityQuery(ComponentType.ReadWrite<VoxelViewer>());
            renderCameras = GetEntityQuery(ComponentType.ReadWrite<VoxelRenderCamera>());
            RequireForUpdate<MainCameraState>();
        }
        public void Bind(CameraRigService rig, IFxFrameReader fx)
        {
            this.rig = rig; lastTarget = Entity.Null;
            // TODO(CAM-008): retain and consume fx.Current after its VFX contract lands.
        }
        protected override void OnUpdate()
        {
            if (rig == null || rig.Camera == null) return;
            float dt = math.clamp(UnityEngine.Time.unscaledDeltaTime, 0, .1f);
            using (TargetMarker.Auto())
            {
                int count = players.CalculateEntityCount();
                if (count == 0) { rig.ClearTarget(); lastTarget = Entity.Null; }
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
                    float radius = 10;
                    if (EntityManager.HasComponent<VoxelGrid>(target))
                    {
                        var grid = EntityManager.GetComponentData<VoxelGrid>(target);
                        radius = math.max(2, .5f * math.length((float3)grid.BrickDims * 16 * grid.CellSize));
                    }
                    // TODO(CAM-006): subtract the same-frame origin warp before measuring teleport distance.
                    bool cut = target != lastTarget || math.distance(pos, lastTargetPos) > rig.Config.TeleportCutDistance;
                    rig.SetTarget(pos, rotation, velocity, radius, cut);
                    lastTarget = target; lastTargetPos = pos;
                }
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
