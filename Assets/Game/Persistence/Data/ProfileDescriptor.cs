namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.6. profile.json.</summary>
    public sealed class ProfileDescriptor
    {
        public int MetaVersion = 1;
        public string Id;
        public string DisplayName;                     // 1..32 chars after trim; default "Commander"
        public System.DateTime CreatedUtc;
        public string LastSlotId;                      // null until the first save/load
    }
}
