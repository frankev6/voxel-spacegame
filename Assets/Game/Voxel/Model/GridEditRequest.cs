using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.4. M3: enable after filling GridEditedBrick.</summary>
    public struct GridEditRequest : IComponentData, IEnableableComponent { }
}
