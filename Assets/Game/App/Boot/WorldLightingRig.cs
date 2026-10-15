using Game.Core;
using Game.Presentation.Services;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.App.Boot
{
    /// <summary>World's authored DESIGN-GUIDE §4.1 lights. The rim and fill keep their azimuths relative to the flight camera,
    /// so silhouettes always read. QA-001 (owner: "sun direction changing shadow direction? we're in space it shouldn't change
    /// based on the camera"): the shadow-casting key is the star and stays fixed in the world. It shines from the system's
    /// star (the star-system origin) at the guide's authored pitch, or from its authored world azimuth where no system is
    /// loaded.</summary>
    public sealed class WorldLightingRig : MonoBehaviour
    {
        CameraRigService cameraRig;
        Transform key;
        float keyPitch, keyYaw;
        EntityQuery origins, bodies;
        World queryWorld;

        void Start()
        {
            Game.Core.Services.TryGet(out cameraRig);
            foreach (var light in GetComponentsInChildren<Light>(true))
                if (light.name == "Key") { key = light.transform; var e = key.localEulerAngles; keyPitch = e.x; keyYaw = e.y; }
        }
        void LateUpdate()
        {
            if (cameraRig?.Camera != null)
                transform.rotation = Quaternion.Euler(0, cameraRig.Camera.transform.eulerAngles.y, 0);
            if (key != null) key.rotation = Quaternion.Euler(keyPitch, StarYaw(out float yaw) ? yaw : keyYaw, 0);
        }

        bool StarYaw(out float yaw)
        {
            yaw = 0;
            var world = World.DefaultGameObjectInjectionWorld;
            if (cameraRig == null || world == null || !world.IsCreated) return false;
            if (queryWorld != world)
            {
                origins = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Game.Galaxy.Components.FrameOrigin>());
                bodies = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Game.Galaxy.Bodies.StarSystemBody>());
                queryWorld = world;
            }
            if (bodies.IsEmptyIgnoreFilter || origins.IsEmptyIgnoreFilter) return false;
            double3 origin = origins.GetSingleton<Game.Galaxy.Components.FrameOrigin>().OriginInStarSystem;
            float3 star = (float3)(-origin), away = cameraRig.Focus - star;
            if (math.lengthsq(away.xz) < 1) return false;
            yaw = math.degrees(math.atan2(away.x, away.z));
            return true;
        }
    }
}
