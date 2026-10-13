namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public enum LogicDependencyKind : byte { Fact = 0, Item = 1, Credits = 2, Quest = 3, Location = 4, Time = 5, Relation = 6 }
}
