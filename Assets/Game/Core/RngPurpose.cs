namespace Game.Core
{
    /// <summary>Random stream purposes (SPEC-02 §4.2, §5.2). Append only; never reorder or renumber.</summary>
    public enum RngPurpose : ushort
    {
        None = 0,                    // invalid: RngStreams.For throws
        GalaxyLayout = 1,
        StarSystemGeneration = 2,
        BodyGeneration = 3,
        AsteroidField = 4,
        EncounterDirector = 5,
        Loot = 6,
        Merchant = 7,
        AiDecision = 8,
        WeaponSpread = 9,
        Damage = 10,
        Debris = 11,
        Narrative = 12,
        NameGeneration = 13,
        CosmeticFx = 14,
        Audio = 15,
        ShipEditor = 16,
        NewGameSeed = 17,
        GalaxyLanes = 18,            // SPEC-30 procedural lane graph
        StarSystemLayout = 19,       // SPEC-30 body counts and orbit spacing
        AsteroidShape = 20,          // SPEC-31 per-asteroid shape noise
        StationShape = 21,           // SPEC-31 procedural station shapes
        EncounterFleet = 22,         // SPEC-33 fleet composition
        EncounterPlacement = 23,     // SPEC-33 spawn placement
        Territory = 24,              // SPEC-33 faction territory
        Salvage = 25,                // SPEC-50 salvage yields
        Test = 1000,
    }
}
