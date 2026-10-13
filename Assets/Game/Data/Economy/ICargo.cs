namespace Game.Data.Economy
{
    /// <summary>SPEC-50 §4.3: narrow cargo access for modules below Game.Economy.</summary>
    public interface ICargo
    {
        int Capacity { get; }
        int CountOf(int itemIndex);
        int AddUpTo(int itemIndex, int count, Game.Core.Events.AcquireSource source);
    }
}
