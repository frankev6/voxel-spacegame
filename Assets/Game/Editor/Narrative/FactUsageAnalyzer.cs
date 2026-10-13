using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Logic.Nodes;
using Game.Data.Validation;
using static Game.Editor.Narrative.NarrativeValidationContext;

namespace Game.Editor.Narrative
{
    /// <summary>SPEC-40 §5.7: editor-only graph traversal; never calls Evaluate, CanApply or Apply.</summary>
    public sealed class FactUsageAnalyzer
    {
        public sealed class FactUsage
        {
            public int Reads { get; internal set; }
            public int Writes { get; internal set; }
            public bool NeedsNonDefault { get; internal set; }
            public string ReadAssetPath { get; internal set; }
            public string ReadField { get; internal set; }
        }
        public readonly struct MemberUse
        {
            public readonly string Member, AssetPath, Field;
            internal MemberUse(string member, CompiledGraph graph)
            { Member = member; AssetPath = graph.AssetPath; Field = graph.Field; }
        }
        readonly FactCatalog facts;
        readonly RefResolverRegistry refs;
        readonly FactUsage[] slots;
        readonly List<MemberUse> worldUses = new();
        public IReadOnlyList<FactUsage> Slots => slots;
        public IReadOnlyList<MemberUse> WorldUses => worldUses;
        internal FactUsageAnalyzer(FactCatalog facts, RefResolverRegistry refs)
        {
            this.facts = facts; this.refs = refs; slots = new FactUsage[facts.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                var flags = facts.FlagsAt(i);
                slots[i] = new FactUsage {
                    Reads = (flags & (FactFlags.ReadByCode | FactFlags.Journal)) != 0 ? 1 : 0,
                    Writes = (flags & FactFlags.WrittenByCode) != 0 || facts.FamilyAt(i) != FactFamily.Authored && facts.FamilyAt(i) != FactFamily.Clue ? 1 : 0
                };
            }
        }

        internal void Analyze(CompiledGraph graph)
        {
            foreach (object node in Nodes(graph.Root))
            {
                if (node is FactBoolRead || node is FactIntRead) Read(Value(node, "name") as string);
                if (node is BoolFunctionCondition || node is IntFunctionExpr)
                {
                    string function = Value(node, "function") as string;
                    var text = Value(node, "textArgs") as string[];
                    if (new LogicFunctionRegistry().TryGet(function, out var info))
                        for (int i = 0; i < info.Params.Length && i < (text?.Length ?? 0); i++)
                            if (info.Params[i].ArgKind == LogicArgKind.Fact) Read(text[i]);
                            else if (info.Params[i].ArgKind == LogicArgKind.Ref &&
                                (info.Params[i].RefKind == RefKind.Clue || info.Params[i].RefKind == RefKind.Beat || info.Params[i].RefKind == RefKind.Mystery))
                            { int slot = RefSlot(info.Params[i].RefKind, text[i]); if (slot >= 0) slots[slot].Reads++; }
                    World(function, text, graph);
                }
                if (node is IEffect)
                {
                    string function = EffectName(node.GetType().Name);
                    var text = Value(Value(node, "arguments"), "Text") as string[];
                    if (function == "set_fact" || function == "add_fact") Write(text?.FirstOrDefault());
                    if (function == "reveal_clue") { int slot = RefSlot(RefKind.Clue, text?.FirstOrDefault()); if (slot >= 0) slots[slot].Writes++; }
                    World(function, text, graph);
                }
            }
            if (graph.Wrapper is CompiledCondition) Require(graph.Root, true, graph);
            // A value/progress read does not require a change from its declared default (V001).

            void Read(string name) { int slot = facts.SlotOf(name); if (slot >= 0) slots[slot].Reads++; }
        }
        internal void AnalyzeAssignments(DialogProgramDefinition program)
        {
            foreach (var instruction in program.Code)
                if ((instruction.Op == DialogOp.SetBool || instruction.Op == DialogOp.SetInt) && instruction.A >= 0 && instruction.A < program.Strings.Count)
                    Write(program.Strings[instruction.A]);
        }
        void Write(string name) { int slot = facts.SlotOf(name); if (slot >= 0) slots[slot].Writes++; }
        int RefSlot(RefKind kind, string name)
        {
            int slot = facts.SlotOf(name);
            if (slot >= 0) return slot;
            if (!refs.TryGet(kind, out var resolver) || !resolver.TryResolve(name, out int index)) return -1;
            foreach (string canonical in resolver.AllIds)
                if (resolver.TryResolve(canonical, out int candidate) && candidate == index) return facts.SlotOf(canonical);
            return -1;
        }

        void Require(object node, bool truth, CompiledGraph graph)
        {
            if (node == null || graph.Debug) return;
            if (node is NotCondition) { Require(Value(node, "operand"), !truth, graph); return; }
            if (node is AndCondition || node is OrCondition)
            { Require(Value(node, "left"), truth, graph); Require(Value(node, "right"), truth, graph); return; }
            if (node is CompareBoolCondition)
            {
                object left = Value(node, "left"), right = Value(node, "right");
                var op = ((CompareBoolCondition)node).Op;
                if (left is BoolLiteral || right is BoolLiteral)
                {
                    bool literal = (bool)Value(left is BoolLiteral ? left : right, "value");
                    bool required = op == CompareOp.Equal ? truth == literal : truth != literal;
                    Require(left is BoolLiteral ? right : left, required, graph); return;
                }
            }
            RequireNonDefaultReads(node, truth, graph);
        }
        void RequireNonDefaultReads(object predicate, bool truth, CompiledGraph graph)
        {
            if (graph.Debug) return;
            foreach (object node in Nodes(predicate))
            {
                string name = node is FactBoolRead || node is FactIntRead ? Value(node, "name") as string : null;
                if (node is IntFunctionExpr && (Value(node, "function") as string == "since_accept" || Value(node, "function") as string == "since_stage"))
                    name = (Value(node, "textArgs") as string[])?.FirstOrDefault();
                int slot = facts.SlotOf(name);
                if (slot < 0) continue;
                // Hold only this fact at its default. Another fact or a service can change independently,
                // so an unknown result is not evidence that this particular fact needs a writer.
                int? result = DefaultValue(predicate, slot);
                if (!result.HasValue || (result.Value != 0) == truth) continue;
                slots[slot].NeedsNonDefault = true;
                slots[slot].ReadAssetPath ??= graph.AssetPath; slots[slot].ReadField ??= graph.Field;
            }
        }
        int? DefaultValue(object node, int factSlot)
        {
            if (node == null) return null;
            if (node is IntLiteral) return (int)Value(node, "value");
            if (node is BoolLiteral) return (bool)Value(node, "value") ? 1 : 0;
            if (node is FactIntRead || node is FactBoolRead)
            { int slot = facts.SlotOf(Value(node, "name") as string); return slot == factSlot ? facts.DefaultAt(slot) : (int?)null; }
            if (node is IntFunctionExpr)
            {
                string name = Value(node, "function") as string;
                if ((name == "since_accept" || name == "since_stage") && facts.SlotOf((Value(node, "textArgs") as string[])?.FirstOrDefault()) == factSlot) return 0;
                return null;
            }
            if (node is NotCondition)
            { int? inner = DefaultValue(Value(node, "operand"), factSlot); return inner.HasValue ? inner == 0 ? 1 : 0 : (int?)null; }
            if (node is NegateExpr)
            { int? inner = DefaultValue(Value(node, "operand"), factSlot); return inner.HasValue ? Saturate(-(long)inner.Value) : (int?)null; }
            int? a = DefaultValue(Value(node, "left"), factSlot), b = DefaultValue(Value(node, "right"), factSlot);
            if (!a.HasValue || !b.HasValue) return null;
            if (node is AddExpr) return Saturate((long)a.Value + b.Value);
            if (node is SubtractExpr) return Saturate((long)a.Value - b.Value);
            if (node is AndCondition) return a != 0 && b != 0 ? 1 : 0;
            if (node is OrCondition) return a != 0 || b != 0 ? 1 : 0;
            if (node is CompareIntCondition integer) return Compare(integer.Op, a.Value, b.Value) ? 1 : 0;
            if (node is CompareBoolCondition boolean) return Compare(boolean.Op, a.Value, b.Value) ? 1 : 0;
            return null;
        }
        static int Saturate(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);
        static bool Compare(CompareOp op, int a, int b) => op switch {
            CompareOp.Equal => a == b, CompareOp.NotEqual => a != b, CompareOp.Less => a < b,
            CompareOp.LessEqual => a <= b, CompareOp.Greater => a > b, CompareOp.GreaterEqual => a >= b, _ => false
        };

        // Inspect both stored graphs and source: a failed fresh compile has no root, but must still emit V008.
        internal void CheckBoolComparisons(CompiledGraph graph, ValidationReport report)
        {
            bool invalid = Nodes(graph.Root).OfType<CompareIntCondition>().Any(node =>
                Invalid(Value(node, "left"), Value(node, "right")) || Invalid(Value(node, "right"), Value(node, "left")));
            if (graph.Wrapper is CompiledCondition condition && !string.IsNullOrWhiteSpace(condition.Source))
                try { invalid |= AstInvalid(LogicParser.ParseExpression(condition.Source)); } catch (LogicDiagnostics) { }
            if (invalid) report.Error("NAR-V008", graph.AssetPath, graph.Field, "A Bool fact is compared with an integer other than 0 or 1.");

            bool Invalid(object a, object b)
            {
                if (!(a is FactBoolRead || a is FactIntRead) || !(b is IntLiteral)) return false;
                int slot = facts.SlotOf(Value(a, "name") as string), value = (int)Value(b, "value");
                return slot >= 0 && facts.KindAt(slot) == FactKind.Bool && value != 0 && value != 1;
            }
            bool AstInvalid(LogicAst ast)
            {
                if (ast is LogicAstBinary binary)
                {
                    bool comparison = binary.Operator == LogicTokenKind.Equal || binary.Operator == LogicTokenKind.NotEqual ||
                        binary.Operator == LogicTokenKind.Less || binary.Operator == LogicTokenKind.LessEqual ||
                        binary.Operator == LogicTokenKind.Greater || binary.Operator == LogicTokenKind.GreaterEqual;
                    return comparison && (AstPair(binary.Left, binary.Right) || AstPair(binary.Right, binary.Left)) || AstInvalid(binary.Left) || AstInvalid(binary.Right);
                }
                if (ast is LogicAstUnary unary) return AstInvalid(unary.Operand);
                if (ast is LogicAstCall call) return call.Arguments.Any(AstInvalid);
                return false;
            }
            bool AstPair(LogicAst a, LogicAst b)
            {
                if (!(a is LogicAstVariable variable)) return false;
                int? number = Constant(b);
                int slot = facts.SlotOf(variable.Name);
                return slot >= 0 && facts.KindAt(slot) == FactKind.Bool && number.HasValue && number != 0 && number != 1;
            }
            int? Constant(LogicAst ast)
            {
                if (ast is LogicAstLiteral literal && literal.Kind == LogicTokenKind.Int) return (int)literal.Value;
                if (ast is LogicAstUnary unary && unary.Operator == LogicTokenKind.Minus)
                { int? operand = Constant(unary.Operand); return operand.HasValue ? Saturate(-(long)operand.Value) : (int?)null; }
                if (ast is LogicAstBinary binary && (binary.Operator == LogicTokenKind.Plus || binary.Operator == LogicTokenKind.Minus))
                {
                    int? a = Constant(binary.Left), b = Constant(binary.Right);
                    if (a.HasValue && b.HasValue) return Saturate((long)a.Value + (binary.Operator == LogicTokenKind.Plus ? b.Value : -(long)b.Value));
                }
                return null;
            }
        }

        static IEnumerable<object> Nodes(object value)
        {
            var result = new List<object>();
            var pending = new Stack<object>(); var visited = new HashSet<object>(new NodeIdentity());
            pending.Push(value);
            while (pending.Count != 0)
            {
                object node = pending.Pop();
                if (node == null || node is string || node.GetType().IsPrimitive || node.GetType().IsEnum || node is UnityEngine.Object || !visited.Add(node)) continue;
                result.Add(node);
                if (node is IEnumerable array) foreach (object child in array) pending.Push(child);
                else foreach (var field in SerializedFields(node.GetType())) pending.Push(field.GetValue(node));
            }
            return result;
        }
        sealed class NodeIdentity : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value); // editor graph identity only
        }
        void World(string function, string[] text, CompiledGraph graph)
        {
            string member = function switch {
                "in_star_system" => "CurrentStarSystemIndex", "star_system_owned" => "CurrentStarSystemOwnerFactionIndex",
                "game_hours" => "UniversalTime", "relation" => "GetRelation", "adjust_relation" => "AdjustRelation",
                "give_ship" => "GiveShip", "spawn_encounter" => "SpawnEncounter", "start_combat" => "StartCombat",
                "remove_body" => "RemoveBody", "replace_body" => "ReplaceBody", "request_lane_jump" => "RequestLaneJump",
                "open_merchant" => "OpenMerchant", "open_ship_editor" => "OpenShipEditor", "repair_ship" => "RepairPlayerShip",
                "damage_ship" => "DamagePlayerShip", "play_sfx" => "PlaySfx", "notify" => "Notify", _ => null
            };
            if (member != null) worldUses.Add(new MemberUse(member, graph));
            if ((function == "relation" || function == "adjust_relation") && text?.FirstOrDefault() == "owner")
                worldUses.Add(new MemberUse("CurrentStarSystemOwnerFactionIndex", graph));
        }
        static string EffectName(string type) => type switch {
            "SetFactEffect" => "set_fact", "AddFactEffect" => "add_fact", "RevealClueEffect" => "reveal_clue",
            "AdjustRelationEffect" => "adjust_relation", "GiveShipEffect" => "give_ship", "SpawnEncounterEffect" => "spawn_encounter",
            "StartCombatEffect" => "start_combat", "RemoveBodyEffect" => "remove_body", "ReplaceBodyEffect" => "replace_body",
            "RequestLaneJumpEffect" => "request_lane_jump", "OpenMerchantEffect" => "open_merchant", "OpenShipEditorEffect" => "open_ship_editor",
            "RepairShipEffect" => "repair_ship", "DamageShipEffect" => "damage_ship", "PlaySfxEffect" => "play_sfx", "NotifyEffect" => "notify", _ => null
        };
    }
}
