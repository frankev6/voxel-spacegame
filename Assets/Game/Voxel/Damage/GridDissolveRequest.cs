using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Request entity (one per grid). Consumer: ShipDeathSystem, which destroys the request entity.</summary>
    public struct GridDissolveRequest : IComponentData { public Entity Grid; public GridDissolveMode Mode; }
}
