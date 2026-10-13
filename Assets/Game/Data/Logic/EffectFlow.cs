namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public enum EffectFlow : byte { Immediate = 0, Blocking = 1, Terminal = 2 }
}
