using Game.Data.Blocks;
using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. Singleton buffer (ADR-0013); Module = None → every functional entity (ADR-0016).</summary>
    public struct FunctionalComponentRegistration : IBufferElementData { public BlockModuleId Module; public ComponentType Type; }
}
