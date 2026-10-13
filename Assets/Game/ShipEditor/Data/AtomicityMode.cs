namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public enum AtomicityMode : byte
    {
        AllOrNothing = 0, PerGroup = 1
    }
}
