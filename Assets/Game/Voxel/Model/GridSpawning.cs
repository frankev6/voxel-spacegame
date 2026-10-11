using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4. Enabled until every brick exists.</summary>
    public struct GridSpawning : IComponentData, IEnableableComponent { public int NextBrick; }
}
