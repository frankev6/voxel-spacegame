namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-63 §4; shared content boundary per ADR-0050.</summary>
    public enum FxMode : byte
    {
        Batched = 0,   // one VisualEffect, events via GraphicsBuffer (debris, hits, small explosions, muzzle, shield sparks)
        Pooled = 1,    // poolSize VisualEffect instances, one per event (large explosions, warp)
        Instanced = 2, // mesh + material drawn with Graphics.RenderMeshPrimitives from ECS state (tracer, beam, plume, shield)
    }
}
