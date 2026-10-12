using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Simulation.Weapons;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Presentation.Vfx
{
    // TODO(VFX-006): final tracer batching/style variants and required tests. TODO(VFX-005): separate debris particle path remains unimplemented.
    /// <summary>FND-020 live projectile visualization, reading the canonical store. VFX-006 adds full tracer batching/style variants.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup))]
    public partial class ProjectileTracerBridgeSystem : SystemBase
    {
        FiringRangeConfigDefinition config; Mesh mesh;
        readonly Matrix4x4[] matrices = new Matrix4x4[1023];
        public void Bind(FiringRangeConfigDefinition definition) { config = definition; }
        protected override void OnCreate()
        {
            RequireForUpdate<ProjectileStore>();
            mesh = new Mesh { name = "Projectile tracer prism" };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            mesh.triangles = new[] { 0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,2,3,7,2,7,6,0,4,7,0,7,3,1,2,6,1,6,5 };
            mesh.RecalculateBounds(); mesh.UploadMeshData(true);
        }
        protected override void OnUpdate()
        {
            if (config == null || config.TracerMaterial == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var store = SystemAPI.GetSingleton<ProjectileStore>(); store.CompleteAccess();
            int count = 0; Bounds bounds = default;
            for (int i = 0; i < store.Rows.Length; i++)
            {
                var row = store.Rows[i]; Vector3 direction = ((Vector3)row.Velocity).normalized;
                Vector3 position = (Vector3)row.Position - direction * (config.TracerLength * .5f);
                matrices[count++] = Matrix4x4.TRS(position, Quaternion.LookRotation(direction), new Vector3(config.TracerWidth, config.TracerWidth, config.TracerLength));
                if (count == 1) bounds = new Bounds(position, Vector3.one * config.TracerLength); else bounds.Encapsulate(new Bounds(position, Vector3.one * config.TracerLength));
                if (count == matrices.Length) { Draw(count, bounds); count = 0; }
            }
            if (count > 0) Draw(count, bounds);
        }
        void Draw(int count, Bounds bounds)
        {
            var parameters = new RenderParams(config.TracerMaterial) { worldBounds = bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            Graphics.RenderMeshInstanced(parameters, mesh, 0, matrices, count);
        }
        protected override void OnDestroy() { if (mesh != null) Object.DestroyImmediate(mesh); mesh = null; config = null; }
    }
}
