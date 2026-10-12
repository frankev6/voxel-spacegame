using Game.Core;
using Game.Presentation.Services;
using UnityEngine;

namespace Game.App.Boot
{
    /// <summary>World's authored DESIGN-GUIDE §4.1 lights keep their azimuths relative to the flight camera.</summary>
    public sealed class WorldLightingRig : MonoBehaviour
    {
        CameraRigService cameraRig;
        void Start() => Game.Core.Services.TryGet(out cameraRig);
        void LateUpdate()
        {
            if (cameraRig?.Camera != null)
                transform.rotation = Quaternion.Euler(0, cameraRig.Camera.transform.eulerAngles.y, 0);
        }
    }
}
