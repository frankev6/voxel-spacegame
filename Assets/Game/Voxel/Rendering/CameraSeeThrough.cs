using Unity.Entities;

namespace Game.Voxel.Rendering
{
    /// <summary>DEV-023: on a grid root, the flight camera does not pull in when this grid stands between it and the ship (the
    /// Hollow's rock: the camera rides above it). Hidden grids (GridRenderHidden) are see-through as well.</summary>
    public struct CameraSeeThrough : IComponentData { }
}
