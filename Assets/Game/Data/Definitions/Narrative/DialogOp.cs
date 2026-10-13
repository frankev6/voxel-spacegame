namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-41 §4.8.</summary>
    public enum DialogOp : byte { Line = 0, Option = 1, ShowOptions = 2, Jump = 3, Detour = 4, Return = 5, Stop = 6, BranchIfFalse = 7, Goto = 8, SetBool = 9, SetInt = 10, Command = 11, EndNode = 12 }
}
