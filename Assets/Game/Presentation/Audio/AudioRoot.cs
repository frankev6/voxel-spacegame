using UnityEngine;
namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §5.2: one late-frame driver owned by AudioService, under the persistent GameRoot.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class AudioRoot : MonoBehaviour
    {
        internal AudioService Service;
        void LateUpdate() => Service?.Update(Time.unscaledDeltaTime);
        void OnDestroy() => Service?.Dispose();
    }
}
