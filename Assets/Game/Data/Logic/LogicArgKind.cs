namespace Game.Data.Logic
{
    /// <summary>Argument kinds in the shared narrative vocabulary (SPEC-40 §4.6–§4.7).</summary>
    public enum LogicArgKind : byte
    {
        Int = 0,
        Bool = 1,
        Fact = 2,
        Ref = 3,
        Text = 4,
        Tag = 5
    }
}
