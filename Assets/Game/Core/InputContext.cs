namespace Game.Core
{
    /// <summary>Input contexts (architecture §6.1). Each enables exactly its action map plus Global (SPEC-03). Implements SPEC-02 §4.4.</summary>
    public enum InputContext : byte { None = 0, Flight = 1, Editor = 2, Menu = 3, Dialog = 4, TextEntry = 5 }
}
