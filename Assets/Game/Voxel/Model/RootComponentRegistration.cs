using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. Singleton buffer (ADR-0015).</summary>
    public struct RootComponentRegistration : IBufferElementData { public GridProfile Profile; public ComponentType Type; }
}
