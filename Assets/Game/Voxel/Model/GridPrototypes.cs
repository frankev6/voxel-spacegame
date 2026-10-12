using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. Singleton; internal to GridSpawnSystem, read by SPEC-13.</summary>
    public struct GridPrototypes : IComponentData
    { public Entity Brick; public NativeHashMap<ulong, Entity> FunctionalByMask, FunctionalByMaskNoRender; /* key = BlockDef.ModuleMask; prototypes created on first use */ public int RegistrationCount; }
}
