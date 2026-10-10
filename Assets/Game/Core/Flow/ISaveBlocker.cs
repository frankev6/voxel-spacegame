namespace Game.Core.Flow
{
    /// <summary>Vetoes saving (combat, death). SaveBlockReason.None = not blocking. Registered with SPEC-01 GameSession. Implements SPEC-02 §4.8.</summary>
    public interface ISaveBlocker { Game.Core.Save.SaveBlockReason GetSaveBlock(); }
}
