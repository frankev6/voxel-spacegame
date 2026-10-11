using System;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1.</summary>
    [Flags] public enum GridSpawnFlags : ushort
    {
        None = 0,
        WriteBack = 1,        // the source structure is owned by this grid: despawn and save write damage back into it
        PlanarInertia = 2,    // SPEC-12 locks rotation to the Y axis (ADR-0003); set together with SPEC-20's PlanarConstraint. GridSpawnSystem ORs it in for
                              // the flying profiles PlayerShip, NpcShip, SmallCraft and Dummy (§5.13), so requesters need not remember it
        NoRender = 4,         // create bricks without render components (headless tools and tests)
        EditorPreview = 8,    // grid edited through GridEditRequest; SPEC-13 never damages it
        Transient = 16,       // never saved (ADR-0015): PersistentId may be 0 without a warning; no save path captures it
    }
}
