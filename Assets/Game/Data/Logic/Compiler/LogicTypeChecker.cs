using System;
using System.Collections.Generic;
using Game.Data.Validation;

namespace Game.Data.Logic.Compiler
{
    /// <summary>Authoring-only post-order type and vocabulary validation, capped at ten errors per field.</summary>
    internal sealed class LogicTypeChecker
    {
        readonly FactCatalog facts;
        readonly RefResolverRegistry refs;
        readonly LogicFunctionRegistry functions;
        readonly LogicCompileOptions options;
        readonly ValidationReport report;
        readonly Dictionary<LogicAst, LogicType> types = new Dictionary<LogicAst, LogicType>();
        int errors;
        public int ErrorCount => errors;
        public LogicTypeChecker(FactCatalog facts, RefResolverRegistry refs, LogicFunctionRegistry functions,
            in LogicCompileOptions options, ValidationReport report)
        { this.facts = facts; this.refs = refs; this.functions = functions; this.options = options; this.report = report; }
        public LogicType TypeOf(LogicAst node) => types[node];
        public bool Check(LogicAst node, LogicType expected)
        {
            var actual = Visit(node);
            if (actual.HasValue && actual.Value != expected)
                Error(node, "NAR-E009", "Expression must be " + expected + ", got " + actual + ".");
            return errors == 0;
        }
        public bool Check(LogicAstCommand command)
        { Call(command, command.Name, command.Arguments, true); return errors == 0; }

        LogicType? Visit(LogicAst node)
        {
            if (errors >= 10) return null;
            LogicType? result = null;
            if (node is LogicAstLiteral literal)
            {
                if (literal.Value is int) result = LogicType.Int;
                else if (literal.Value is bool) result = LogicType.Bool;
                else Error(node, "NAR-E009", "Expression must be Bool or Int, got text.");
            }
            else if (node is LogicAstVariable variable)
            {
                int slot = Fact(node, variable.Name);
                if (slot >= 0) result = facts.KindAt(slot) == FactKind.Bool ? LogicType.Bool : LogicType.Int;
            }
            else if (node is LogicAstCall call) result = Call(node, call.Name, call.Arguments, false);
            else if (node is LogicAstUnary unary)
            {
                var operand = Visit(unary.Operand);
                LogicType wanted = unary.Operator == LogicTokenKind.Not ? LogicType.Bool : LogicType.Int;
                if (operand.HasValue && operand.Value != wanted)
                    Error(node, "NAR-E010", "Operator '" + unary.Operator + "' cannot use " + operand + ".");
                else if (operand.HasValue) result = wanted;
            }
            else if (node is LogicAstBinary binary)
            {
                var left = Visit(binary.Left); var right = Visit(binary.Right);
                if (left.HasValue && right.HasValue)
                {
                    bool boolean = binary.Operator == LogicTokenKind.And || binary.Operator == LogicTokenKind.Or;
                    bool equality = binary.Operator == LogicTokenKind.Equal || binary.Operator == LogicTokenKind.NotEqual;
                    bool arithmetic = binary.Operator == LogicTokenKind.Plus || binary.Operator == LogicTokenKind.Minus;
                    if (left != right || boolean && left != LogicType.Bool || !boolean && !equality && left != LogicType.Int)
                        Error(node, "NAR-E010", "Operator '" + binary.Operator + "' cannot combine " + left + " and " + right + ".");
                    else result = arithmetic ? LogicType.Int : LogicType.Bool;
                }
            }
            if (result.HasValue) types[node] = result.Value;
            return result;
        }

        LogicType? Call(LogicAst node, string name, IReadOnlyList<LogicAst> args, bool effect)
        {
            if (!functions.TryGet(name, out var info) || info.IsEffect != effect)
            {
                string suggestion = functions.Suggest(name);
                Error(node, effect ? "NAR-E016" : "NAR-E003", "Unknown " + (effect ? "command" : "function") +
                    " '" + name + "'" + (suggestion == null ? "" : "; did you mean '" + suggestion + "'?") + ".");
                return null;
            }
            if ((info.AllowedUsage & options.Usage) != options.Usage || options.Usage == LogicUsage.None)
                Error(node, "NAR-E008", "'" + name + "' is not allowed in " + options.Usage + ".");
            int minimum = 0;
            foreach (var parameter in info.Params) if (!parameter.Optional) minimum++;
            if (args.Count < minimum || args.Count > info.Params.Length)
                Error(node, "NAR-E004", "'" + name + "' expects " + minimum + ".." + info.Params.Length + " arguments, got " + args.Count + ".");
            int factSlot = -1;
            for (int i = 0; i < Math.Min(args.Count, info.Params.Length) && errors < 10; i++)
            {
                var argument = args[i]; var parameter = info.Params[i];
                string text = argument is LogicAstLiteral textLiteral && textLiteral.Value is string value ? value :
                    effect && argument is LogicAstVariable variable ? variable.Name : null;
                if (parameter.ArgKind == LogicArgKind.Fact)
                {
                    if (text == null) Wrong(argument, i, name, "fact name");
                    else
                    {
                        factSlot = Fact(argument, text);
                        if (factSlot >= 0 && (name == "set_fact" || name == "add_fact"))
                        {
                            if (!facts.IsWritableByContent(factSlot))
                                Error(argument, "NAR-E011", "Fact '" + text + "' is generated by the game and cannot be written.");
                            if (name == "add_fact" && facts.KindAt(factSlot) != FactKind.Int) Wrong(argument, i, name, "Int fact");
                        }
                    }
                }
                else if (parameter.ArgKind == LogicArgKind.Ref)
                {
                    if (text == null || argument is LogicAstVariable) Wrong(argument, i, name, parameter.RefKind + " reference");
                    else if (!refs.TryGet(parameter.RefKind, out var resolver))
                        report.Warning("NAR-W001", options.AssetPath, Field(argument), "No resolver for " + parameter.RefKind + "; '" + text + "' was not checked.");
                    else if (!(parameter.RefKind == RefKind.Faction && text == "owner") && !resolver.TryResolve(text, out _))
                        Error(argument, "NAR-E007", "Unknown " + parameter.RefKind + " '" + text + "'.");
                }
                else if (parameter.ArgKind == LogicArgKind.Text || parameter.ArgKind == LogicArgKind.Tag)
                {
                    bool quoted = argument is LogicAstLiteral l && l.Kind == LogicTokenKind.String;
                    if (text == null || parameter.ArgKind == LogicArgKind.Text && !quoted) Wrong(argument, i, name, parameter.ArgKind.ToString());
                    else if (name == "notify" && (text.Length < 1 || text.Length > 200)) Range(argument, name, text.Length, 1, 200);
                }
                else
                {
                    var actual = Visit(argument);
                    var expected = parameter.ArgKind == LogicArgKind.Bool ? LogicType.Bool : LogicType.Int;
                    if (name == "set_fact" && i == 1 && factSlot >= 0 && facts.KindAt(factSlot) == FactKind.Bool)
                    {
                        if (actual != LogicType.Bool && !(argument is LogicAstLiteral boolNumber && boolNumber.Value is int n && (n == 0 || n == 1)))
                            Error(argument, "NAR-E019", "Bool fact assigned a non-bool value.");
                    }
                    else if (actual.HasValue && actual.Value != expected) Wrong(argument, i, name, expected.ToString());
                    if (argument is LogicAstLiteral number && number.Value is int integer) CheckRange(argument, name, i, integer);
                }
            }
            if (name == "complete_quest")
                report.Warning("NAR-W010", options.AssetPath, Field(node), "complete_quest bypasses objectives; use only for converted legacy content.");
            return info.ReturnType;
        }

        int Fact(LogicAst node, string name)
        {
            int slot = facts.SlotOf(name);
            if (slot < 0) Error(node, "NAR-E006", "Unknown fact '" + name + "'.");
            return slot;
        }
        void CheckRange(LogicAst node, string name, int argument, int value)
        {
            int min = int.MinValue, max = int.MaxValue;
            switch (name)
            {
                case "roll": min = 2; max = 1000; break;
                case "give_item": case "take_item": if (argument == 1) { min = 1; max = 100000; } break;
                case "give_credits": case "take_credits": min = 1; max = 1000000000; break;
                case "repair_ship": min = 1; max = 100; break;
                case "damage_ship": min = 1; max = 1000; break;
                case "adjust_relation": if (argument == 1) { min = -100; max = 100; } break;
                case "spawn_encounter": if (argument == 2) { min = 1; max = 10; } break;
            }
            if (value < min || value > max) Range(node, name, value, min, max);
        }
        void Range(LogicAst node, string name, int value, int min, int max) =>
            Error(node, "NAR-E012", "Value " + value + " is out of range " + min + ".." + max + " for '" + name + "'.");
        void Wrong(LogicAst node, int argument, string name, string expected) =>
            Error(node, "NAR-E005", "Argument " + (argument + 1) + " of '" + name + "' must be " + expected + ".");
        string Field(LogicAst node) => options.Field + " (" + node.Line + ":" + node.Column + ")";
        void Error(LogicAst node, string code, string message)
        {
            if (errors >= 10) return;
            errors++; report.Error(code, options.AssetPath, Field(node), message);
        }
    }
}
