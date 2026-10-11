namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5.</summary>
    public enum ContainerReadStatus : byte { Ok = 0, CorruptChunks = 1, CorruptHeader = 2, NewerFormat = 3, NotAContainer = 4 }
}
