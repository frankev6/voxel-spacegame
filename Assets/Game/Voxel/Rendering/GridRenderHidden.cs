using Unity.Entities;

namespace Game.Voxel.Rendering
{
    /// <summary>DEV-023: on a grid root, its bricks are not drawn (nor cast shadows) while the tag is present; simulation, physics and
    /// meshes are untouched, so removing it shows the grid again at once. The Hollow's cutaway hides the rock above the flight plane
    /// while the player is inside.</summary>
    public struct GridRenderHidden : IComponentData { }
}
