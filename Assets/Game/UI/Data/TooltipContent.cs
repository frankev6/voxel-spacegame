namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    public sealed class TooltipContent
    {
        public string Title;
        public string Subtitle;
        public string Body;
        public readonly System.Collections.Generic.List<TooltipStat> Stats = new System.Collections.Generic.List<TooltipStat>(8);
        public void Clear() { Title = null; Subtitle = null; Body = null; Stats.Clear(); }
    }
}
