using System;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5.</summary>
    [Flags] public enum BrickDirtyFlags : byte { None = 0, Mesh = 1, Collider = 2 }
}
