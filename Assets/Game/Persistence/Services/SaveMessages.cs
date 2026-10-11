using Game.Core.Save;

namespace Game.Persistence.Services
{
    /// <summary>Player-facing English texts (SPEC-05 §5.8).</summary>
    public static class SaveMessages
    {
        public static string Blocked(SaveBlockReason reason)
        {
            switch (reason)
            {
                case SaveBlockReason.NoSession: return "There is no game to save.";
                case SaveBlockReason.InCombat: return "You can't save during combat.";
                case SaveBlockReason.PlayerDead: return "You can't save while your ship is destroyed.";
                case SaveBlockReason.EditorOpen: return "Close the ship editor before saving.";
                default: return "Saving isn't possible right now.";
            }
        }

        public static string IoError(string exceptionMessage) => "The game could not be saved: " + exceptionMessage;
        public const string DiskFull = "Not enough disk space to save.";
        public static string ParticipantFailed(string key) => $"The game could not be saved because of an internal error ({key}). Your previous save is unchanged.";
        public const string SlotLimit = "No free save slots. Delete a save first.";
        public const string NotFound = "This save could not be found.";
        public const string Unreadable = "This save file is damaged and cannot be loaded.";
        public const string NewerFormat = "This save was made by a newer version of the game.";
        public const string Saved = "Game saved.";
        public const string Autosaved = "Autosaved.";
        public const string TooManyQueued = "Too many save operations queued";
        public static string Loaded(int warnings) => warnings > 0 ? $"Game loaded with {warnings} warnings." : "Game loaded.";
    }
}
