#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.App.DevRuntime.Scenarios
{
    /// <summary>Typed expression tree, evaluated without rebuilding nodes or argument arrays (SPEC-70 §6.2).</summary>
    public abstract class ScenarioExpression
    {
        public abstract ScenarioValue Evaluate(DevContext context, IReadOnlyDictionary<string, ScenarioValue> variables = null);
        public virtual string ComparisonValues(DevContext context, IReadOnlyDictionary<string, ScenarioValue> variables) => "";
        public static ScenarioValue Number(double value) => new ScenarioValue(ScenarioValueKind.Number, value, false, null);
        public static ScenarioValue Boolean(bool value) => new ScenarioValue(ScenarioValueKind.Bool, 0, value, null);
        public static ScenarioValue Text(string value) => new ScenarioValue(ScenarioValueKind.String, 0, false, value ?? "");
        public static double RequireNumber(ScenarioValue value) => value.Kind == ScenarioValueKind.Number ? value.Number : throw new InvalidOperationException("expected number, got " + value.Kind);
        public static bool RequireBool(ScenarioValue value) => value.Kind == ScenarioValueKind.Bool ? value.Bool : throw new InvalidOperationException("expected bool, got " + value.Kind);
        public static string RequireString(ScenarioValue value) => value.Kind == ScenarioValueKind.String ? value.String : throw new InvalidOperationException("expected string, got " + value.Kind);
        public static string Display(ScenarioValue value) => value.Kind == ScenarioValueKind.Number ? value.Number.ToString("R", CultureInfo.InvariantCulture) : value.Kind == ScenarioValueKind.Bool ? (value.Bool ? "true" : "false") : DevJsonWriter.Quote(value.String);
        internal sealed class Literal : ScenarioExpression
        {
            readonly ScenarioValue value;
            internal Literal(ScenarioValue value) { this.value = value; }
            public override ScenarioValue Evaluate(DevContext context, IReadOnlyDictionary<string, ScenarioValue> variables = null) => value;
        }
        internal sealed class Variable : ScenarioExpression
        {
            readonly string name;
            internal Variable(string name) { this.name = name; }
            public override ScenarioValue Evaluate(DevContext context, IReadOnlyDictionary<string, ScenarioValue> variables = null)
                => variables != null && variables.TryGetValue(name, out var value) ? value : throw new InvalidOperationException("undefined variable $" + name);
        }
        internal sealed class Unary : ScenarioExpression
        {
            readonly string op; readonly ScenarioExpression value;
            internal Unary(string op, ScenarioExpression value) { this.op = op; this.value = value; }
            public override ScenarioValue Evaluate(DevContext context, IReadOnlyDictionary<string, ScenarioValue> variables = null)
            {
                var v = value.Evaluate(context, variables);
                return op == "!" ? Boolean(!RequireBool(v)) : Number(-RequireNumber(v));
            }
        }
        internal sealed class Binary : ScenarioExpression
        {
            readonly string op; readonly ScenarioExpression left, right;
            internal Binary(string op, ScenarioExpression left, ScenarioExpression right) { this.op = op; this.left = left; this.right = right; }
            bool IsComparison => op == "==" || op == "!=" || op == "<" || op == "<=" || op == ">" || op == ">=";
            public override ScenarioValue Evaluate(DevContext context, IReadOnlyDictionary<string, ScenarioValue> variables = null)
            {
                var a = left.Evaluate(context, variables);
                if (op == "&&") return Boolean(RequireBool(a) && RequireBool(right.Evaluate(context, variables)));
                if (op == "||") return Boolean(RequireBool(a) || RequireBool(right.Evaluate(context, variables)));
                var b = right.Evaluate(context, variables);
                if (IsComparison)
                {
                    if (a.Kind != b.Kind) throw new InvalidOperationException("cannot compare " + a.Kind + " with " + b.Kind);
                    int cmp = a.Kind == ScenarioValueKind.Number ? a.Number.CompareTo(b.Number) : a.Kind == ScenarioValueKind.String ? string.CompareOrdinal(a.String, b.String) : a.Bool.CompareTo(b.Bool);
                    if (a.Kind == ScenarioValueKind.Bool && op != "==" && op != "!=") throw new InvalidOperationException("bool supports only == and !=");
                    return Boolean(op switch { "==" => cmp == 0, "!=" => cmp != 0, "<" => cmp < 0, "<=" => cmp <= 0, ">" => cmp > 0, _ => cmp >= 0 });
                }
                double x = RequireNumber(a), y = RequireNumber(b);
                if (op == "/" && y == 0) throw new InvalidOperationException("division by zero");
                double n = op switch { "+" => x + y, "-" => x - y, "*" => x * y, _ => x / y };
                if (double.IsInfinity(n) || double.IsNaN(n)) throw new InvalidOperationException("non-finite arithmetic result");
                return Number(n);
            }
            public override string ComparisonValues(DevContext context, IReadOnlyDictionary<string, ScenarioValue> variables)
                => IsComparison ? Display(left.Evaluate(context, variables)) + " " + op + " " + Display(right.Evaluate(context, variables)) : "";
        }
        internal sealed class Probe : ScenarioExpression
        {
            readonly IScenarioProbe probe; readonly ScenarioExpression[] arguments; readonly ScenarioValue[] values;
            internal Probe(IScenarioProbe probe, ScenarioExpression[] arguments) { this.probe = probe; this.arguments = arguments; values = new ScenarioValue[arguments.Length]; }
            public override ScenarioValue Evaluate(DevContext context, IReadOnlyDictionary<string, ScenarioValue> variables = null)
            {
                for (int i = 0; i < arguments.Length; i++) values[i] = arguments[i].Evaluate(context, variables);
                return probe.Read(context, values);
            }
        }
    }
}
#endif
