using Game.Voxel.PresentationStreams;
using Unity.Collections;

namespace Game.Presentation.Services
{
    /// <summary>SPEC-62 §6: borrowed FX events, readable only in this frame's PresentationBridgeGroup.</summary>
    public interface IFxFrameReader
    {
        NativeArray<FxEvent>.ReadOnly Current { get; }
    }
}
