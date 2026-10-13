namespace Game.Galaxy.Bodies
{
    /// <summary>Implements SPEC-31 §4.2. Anchor streaming flags.</summary>
    [System.Flags] public enum BodyFlags : byte { None = 0, HasGrid = 1, GridPending = 2, Destroyed = 4, HasCollider = 8, OnRails = 16, Asteroid = 32 }
}
