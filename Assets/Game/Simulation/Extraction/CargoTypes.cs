using Unity.Entities;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 decision 4: what a ship's hold can carry through a gate, in tonnes. Writers: the place/den setup from the ship's
    /// cargo blocks (CargoUnits × TonnesPerCargoUnit) and StowApi (StowedT). No CargoHold: capacity 0.</summary>
    public struct CargoHold : IComponentData
    {
        public float CapacityT;
        public float StowedT;
        public float FreeT => CapacityT - StowedT;
    }

    /// <summary>On a ship a gate refused this tick or earlier (authority; PortalDecideSystem). The HUD toasts it once per Tick.</summary>
    public struct CargoRefused : IComponentData
    {
        public uint Tick;
        public float LoadT, FreeT;
    }

    /// <summary>On a load stowed in a hold (the entity is Disabled while stowed: out of physics and out of sight).</summary>
    public struct Stowed : IComponentData
    {
        public Entity Ship;
        public float MassT;
    }

    public static class CargoRule
    {
        /// <summary>One cargo unit (SPEC-04 CargoModule) carries this many tonnes through a gate: block_cargo_small (50 units) takes 50 t.</summary>
        public const float TonnesPerCargoUnit = 1f;

        /// <summary>A load passes a gate when the hold's free tonnes take it.</summary>
        public static bool Allows(float loadT, float freeT) => loadT <= freeT + 1e-3f;
    }
}
