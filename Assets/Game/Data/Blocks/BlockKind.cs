namespace Game.Data.Blocks
{
    /// <summary>How a block is represented (architecture §6.2, binding values). Implements SPEC-04 §4.2.</summary>
    public enum BlockKind : byte { Structural = 0, Prop = 1, Functional = 2 }        // binding (architecture §6.2)
}
