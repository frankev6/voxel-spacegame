namespace Game.Data.Blocks
{
    /// <summary>Display-only grouping: editor palette tabs (SPEC-51), function colours and glyphs (SPEC-80, DESIGN-SYSTEM). Gameplay never reads it.
    /// Implements SPEC-04 §4.6.</summary>
    public enum BlockCategory : byte { Structure = 0, Detail = 1, Core = 2, Propulsion = 3, Power = 4, Weapon = 5, Shield = 6, Utility = 7 }   // add at end only
}
