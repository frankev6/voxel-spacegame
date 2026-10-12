namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    public struct ConfirmOption
    {
        public string Label;          // button text
        public string ElementName;    // button name, e.g. "confirm-commit"; null → "confirm-option-<index>"
        public ButtonVariant Variant;
        public bool Enabled;
        public string DisabledReason; // shown as the button tooltip when Enabled == false
    }
}
