namespace Game.Data.Hazards
{
    /// <summary>What a zone does (SPEC-91 §4.3, ADR-0118 decision 5). Append only.</summary>
    public enum ZoneEffectKind : byte
    {
        Accelerate = 0, Drag = 1, Repel = 2, Gate = 3, Bounds = 4,
        Attract = 5,   // ADR-0119: A = GM (m³/s²), B = minimum radius m (softening)
        Pulse = 6,     // ADR-0119: A = damage per pulse, B = shield drain per pulse, C = source (0 = point at the zone centre, 1 = direction: the zone yaw)
    }
}
