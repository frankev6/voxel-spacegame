namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    public sealed class TextPromptRequest
    {
        public string Title;
        public string Label;
        public string InitialText = "";
        public int MaxLength = 32;
        public string OkLabel = "OK";
        public string CancelLabel = "Cancel";
        public System.Func<string, string> Validate;  // input is trimmed; returns error text or null when valid. null delegate = non-empty check only
    }
}
