using Unity.Entities;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: AiSetup.</summary>
    public static class AiSetup
    {
        public static void Register(DynamicBuffer<Game.Voxel.Model.RootComponentRegistration> registrations) => throw new System.NotImplementedException();   // called by AiAnchorShiftSystem.OnCreate
        /// Managed: IndexOf<AiProfileDefinition>(profile); ContentId.None or unknown → 0 (default; unknown also logs GameLog.Warn once).
        public static ushort ProfileIndexOf(Game.Data.Services.ContentCatalog catalog, Game.Core.ContentId profile) => throw new System.NotImplementedException();
    }
}
