using UnityEngine;

namespace Game.Presentation.Services
{
    /// <summary>Context-gated frame input sampled once by the rig (SPEC-62 §4.1).</summary>
    public interface ICameraInput
    {
        Vector2 OrbitDelta { get; }
        Vector2 PanDelta { get; }
        float ZoomSteps { get; }
        bool RecenterPressed { get; }
        bool FocusPressed { get; }
        bool ViewIsoPressed { get; }
        bool ViewTopPressed { get; }
        bool ViewFrontPressed { get; }
        bool ViewSidePressed { get; }
        void Sample();
    }
}
