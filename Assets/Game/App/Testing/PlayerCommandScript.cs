#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Simulation.Commands;
using Unity.Mathematics;

namespace Game.App.Testing
{
    /// <summary>SPEC-71 §5.4 sparse command text; FrameIndex is transient metadata (ADR-0051).</summary>
    public sealed class PlayerCommandScript
    {
        readonly struct Change
        {
            public readonly uint Tick;
            public readonly PlayerCommand Command;
            public Change(uint tick, in PlayerCommand command) { Tick = tick; Command = command; }
        }
        readonly List<Change> changes = new List<Change>();
        bool fixedStart, hasSet;
        uint lastSet;
        ulong? seed, content;
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }
        public bool ChangesOnlyAtEvenTicks
        {
            get { foreach (var change in changes) if ((change.Tick & 1) != 0) return false; return true; }
        }
        public void Set(uint tick, in PlayerCommand command)
        {
            if (tick == uint.MaxValue || (hasSet && tick < lastSet) || (fixedStart && tick < StartTick))
                throw new ArgumentOutOfRangeException(nameof(tick), "Ticks must be ascending and have a representable exclusive end.");
            if (!math.all(math.isfinite(command.Thrust)) || !math.isfinite(command.Turn) || !math.all(math.isfinite(command.AimWorld)))
                throw new ArgumentException("Command text requires finite floats.", nameof(command));
            if (!fixedStart && !hasSet) StartTick = tick;
            hasSet = true; lastSet = tick; EndTick = Math.Max(EndTick, tick + 1);
            if (changes.Count > 0 && changes[changes.Count - 1].Tick == tick) changes.RemoveAt(changes.Count - 1);
            var value = command; value.FrameIndex = 0;
            var previous = changes.Count == 0 ? default : changes[changes.Count - 1].Command;
            if (!Same(previous, value)) changes.Add(new Change(tick, value));
        }
        public PlayerCommand At(uint tick)
        {
            int low = 0, high = changes.Count - 1;
            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                if (changes[mid].Tick <= tick) low = mid + 1; else high = mid - 1;
            }
            return tick < StartTick || high < 0 ? default : changes[high].Command;
        }
        internal void Begin(uint tick) { fixedStart = true; StartTick = EndTick = tick; }
        internal void Finish(uint tick)
        {
            EndTick = Math.Max(StartTick, tick);
            while (changes.Count > 0 && changes[changes.Count - 1].Tick >= EndTick) changes.RemoveAt(changes.Count - 1);
        }
        static bool Same(in PlayerCommand a, in PlayerCommand b) =>
            math.all(math.asuint(a.Thrust) == math.asuint(b.Thrust)) && math.asuint(a.Turn) == math.asuint(b.Turn) &&
            math.all(math.asuint(a.AimWorld) == math.asuint(b.AimWorld)) && a.AimValid == b.AimValid && a.Flags == b.Flags &&
            a.FireGroups == b.FireGroups && a.Edges.Interact == b.Edges.Interact && a.Edges.Ability1 == b.Edges.Ability1 &&
            a.Edges.Ability2 == b.Edges.Ability2 && a.Edges.Ability3 == b.Edges.Ability3 && a.Edges.Ability4 == b.Edges.Ability4 &&
            a.Edges.TargetNext == b.Edges.TargetNext && a.Edges.TargetUnderCursor == b.Edges.TargetUnderCursor &&
            a.Edges.TargetClear == b.Edges.TargetClear && a.Edges.AutofireToggle == b.Edges.AutofireToggle && a.Edges.Cruise == b.Edges.Cruise;
        static string Float(float value) => math.asuint(value) == 0x80000000u ? "-0" : value.ToString("R", CultureInfo.InvariantCulture);
        public string ToText()
        {
            var text = new StringBuilder();
            text.Append("cmdrec 1\nstart ").Append(StartTick.ToString(CultureInfo.InvariantCulture));
            text.Append("\nend ").Append(EndTick.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (seed.HasValue) text.Append("seed ").Append(seed.Value.ToString("X16", CultureInfo.InvariantCulture)).Append('\n');
            if (content.HasValue) text.Append("content ").Append(content.Value.ToString("X16", CultureInfo.InvariantCulture)).Append('\n');
            foreach (var change in changes)
            {
                var c = change.Command; var e = c.Edges;
                text.Append(change.Tick.ToString(CultureInfo.InvariantCulture));
                foreach (float f in new[] { c.Thrust.x, c.Thrust.y, c.Turn, c.AimWorld.x, c.AimWorld.y, c.AimWorld.z }) text.Append(' ').Append(Float(f));
                foreach (uint n in new uint[] { c.AimValid, (byte)c.Flags, c.FireGroups, e.Interact, e.Ability1, e.Ability2, e.Ability3, e.Ability4,
                    e.TargetNext, e.TargetUnderCursor, e.TargetClear, e.AutofireToggle, e.Cruise }) text.Append(' ').Append(n.ToString(CultureInfo.InvariantCulture));
                text.Append('\n');
            }
            return text.ToString();
        }
        public static PlayerCommandScript Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var script = new PlayerCommandScript(); bool version = false, start = false, end = false, records = false;
            uint declaredEnd = 0, previous = 0; int lineNumber = 0;
            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++; int comment = line.IndexOf('#'); if (comment >= 0) line = line.Substring(0, comment);
                var tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries); if (tokens.Length == 0) continue;
                try
                {
                    if (!version)
                    {
                        if (tokens.Length != 2 || tokens[0] != "cmdrec" || tokens[1] != "1") throw new FormatException("Expected cmdrec 1.");
                        version = true; continue;
                    }
                    if (tokens[0] == "start" || tokens[0] == "end" || tokens[0] == "seed" || tokens[0] == "content")
                    {
                        if (records || tokens.Length != 2) throw new FormatException("Header must precede records and contain one value.");
                        switch (tokens[0])
                        {
                            case "start": if (start) throw new FormatException("Duplicate start."); script.Begin(UInt(tokens[1])); start = true; break;
                            case "end": if (end) throw new FormatException("Duplicate end."); declaredEnd = UInt(tokens[1]); end = true; break;
                            case "seed": if (script.seed.HasValue) throw new FormatException("Duplicate seed."); script.seed = ulong.Parse(tokens[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture); break;
                            case "content": if (script.content.HasValue) throw new FormatException("Duplicate content."); script.content = ulong.Parse(tokens[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture); break;
                        }
                        continue;
                    }
                    if (!start || !end || tokens.Length != 20) throw new FormatException("Expected start/end headers and 20 record fields.");
                    uint tick = UInt(tokens[0]);
                    if (tick < script.StartTick || tick >= declaredEnd || (records && tick <= previous)) throw new FormatException("Record ticks must increase within [start, end).");
                    var c = new PlayerCommand
                    {
                        Thrust = new float2(ParseFloat(tokens[1]), ParseFloat(tokens[2])), Turn = ParseFloat(tokens[3]),
                        AimWorld = new float3(ParseFloat(tokens[4]), ParseFloat(tokens[5]), ParseFloat(tokens[6])), AimValid = Byte(tokens[7]),
                        Flags = (ShipCommandFlags)Byte(tokens[8]), FireGroups = UInt(tokens[9]),
                        Edges = new PlayerEdgeCounters { Interact = Byte(tokens[10]), Ability1 = Byte(tokens[11]), Ability2 = Byte(tokens[12]), Ability3 = Byte(tokens[13]),
                            Ability4 = Byte(tokens[14]), TargetNext = Byte(tokens[15]), TargetUnderCursor = Byte(tokens[16]), TargetClear = Byte(tokens[17]),
                            AutofireToggle = Byte(tokens[18]), Cruise = Byte(tokens[19]) }
                    };
                    script.Set(tick, c); previous = tick; records = true;
                }
                catch (Exception e) when (e is FormatException || e is OverflowException || e is ArgumentException)
                { throw new FormatException("Line " + lineNumber + ": " + e.Message, e); }
            }
            if (!version || !start || !end || declaredEnd < script.StartTick) throw new FormatException("Line " + Math.Max(1, lineNumber) + ": Missing or invalid header bounds.");
            script.EndTick = declaredEnd; return script;
        }
        static uint UInt(string text) => uint.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
        static byte Byte(string text) => byte.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
        static float ParseFloat(string text)
        {
            float value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            // Unity's Mono parser may normalize negative zero; retain its sign for the promised bit-exact text round trip.
            return value == 0 && text.StartsWith("-", StringComparison.Ordinal) ? math.asfloat(0x80000000u) : value;
        }
        public static PlayerCommandScript Load(string path) => Parse(File.ReadAllText(path));
        public void Save(string path) => File.WriteAllText(path, ToText(), new UTF8Encoding(false));
    }
}
#endif
