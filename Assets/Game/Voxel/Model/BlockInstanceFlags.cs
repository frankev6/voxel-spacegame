using System;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1. Destroyed: function lost (SPEC-13); cells may remain.</summary>
    [Flags] public enum BlockInstanceFlags : byte { None = 0, Destroyed = 1 }
}
