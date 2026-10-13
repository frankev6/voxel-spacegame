namespace Game.Galaxy.Services
{
    /// <summary>GLXY participant contract over the session's galaxy state (SPEC-30 §4.8, §5.11).</summary>
    public sealed class GalaxySaveParticipant : Game.Core.Save.ISaveParticipant
    {
        public string Key => "GLXY";
        public int Version => 1;

        // TODO(GAL-010): capture and restore GalaxyState using the canonical GLXY schema.
        public void Capture(Game.Core.Save.SaveWriter writer) => throw new System.NotImplementedException("GAL-010");
        public void Restore(Game.Core.Save.SaveReader reader, int fromVersion) => throw new System.NotImplementedException("GAL-010");
    }
}
