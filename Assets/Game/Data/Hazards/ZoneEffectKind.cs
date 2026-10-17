namespace Game.Data.Hazards
{
    /// <summary>What a zone does (SPEC-91 §4.3, ADR-0118 decision 5). Append only.</summary>
    public enum ZoneEffectKind : byte { Accelerate = 0, Drag = 1, Repel = 2, Gate = 3, Bounds = 4 }
}
