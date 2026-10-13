using System;
using Game.Presentation.Vfx;
using Game.Voxel.PresentationStreams;
using Unity.Collections;

namespace Game.Presentation.Services
{
    /// <summary>SPEC-62 §10 Q4: borrows the uploader's completed drain without owning or consuming its events.</summary>
    public sealed class FxFrameReader : IFxFrameReader
    {
        readonly FxUploadSystem source;
        public FxFrameReader(FxUploadSystem source) => this.source = source ?? throw new ArgumentNullException(nameof(source));
        public NativeArray<FxEvent>.ReadOnly Current => source.FrameEvents;
    }
}
