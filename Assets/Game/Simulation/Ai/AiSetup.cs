using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Simulation.Commands;
using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: AiSetup.</summary>
    public static class AiSetup
    {
        /// <summary>Appends AI brains and Station-only commands once from AiAnchorShiftSystem.OnCreate (ADR-0015/0071).</summary>
        public static void Register(DynamicBuffer<RootComponentRegistration> registrations)
        {
            var type = ComponentType.ReadWrite<AiBrain>();
            registrations.Add(new RootComponentRegistration { Profile = GridProfile.NpcShip, Type = type });
            registrations.Add(new RootComponentRegistration { Profile = GridProfile.Station, Type = type });
            // Stations deliberately skip FlightSetup but still consume turret aim and one-tick ability commands.
            registrations.Add(new RootComponentRegistration { Profile = GridProfile.Station, Type = ComponentType.ReadWrite<ShipCommand>() });
            registrations.Add(new RootComponentRegistration { Profile = GridProfile.SmallCraft, Type = type });
        }

        /// <summary>Managed spawner lookup: None or unknown selects the default copy at index 0; unknown IDs warn once per session.</summary>
        public static ushort ProfileIndexOf(ContentCatalog catalog, ContentId profile)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (profile.IsNone) return 0;
            if (catalog.TryGet<AiProfileDefinition>(profile, out var definition)) return catalog.IndexOf(definition);
            GameLog.WarnOnce("AiSetup.UnknownProfile:" + profile.Value, LogCategories.AI,
                "Unknown AI profile '" + profile.Value + "'; using the default profile.");
            return 0;
        }
    }
}
