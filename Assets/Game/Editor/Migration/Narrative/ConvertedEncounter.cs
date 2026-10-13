using System.Collections.Generic;

namespace Game.Editor.Migration.Narrative
{
    /// <summary>Logic-ready memory only. NAR-015 owns text escaping, line IDs, nested form and all writes.</summary>
    internal sealed class ConvertedEncounter
    {
        public string BeatId;
        public LegacyEncounterDto Source;
        public readonly List<Node> Nodes = new List<Node>();

        internal sealed class Node
        {
            public string Title;
            public string[] Tags;
            public LegacyPageDto Source;
            public readonly List<Option> Options = new List<Option>();
            public readonly List<string> Statements = new List<string>();
        }

        internal sealed class Option
        {
            public LegacyOptionDto Source;
            public string ShowCondition;
            public string EnableCondition;
            public readonly List<string> Statements = new List<string>();
            public readonly List<string> Commands = new List<string>();
            public readonly List<string> Conditions = new List<string>();
            public bool HasFlow;
            public bool Terminal;
            public bool Unavailable;
        }

        internal readonly struct LogEntry
        {
            public readonly string QuestId, Id, Text;
            public LogEntry(string questId, string id, string text) { QuestId = questId; Id = id; Text = text; }
        }
    }
}
