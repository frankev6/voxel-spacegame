using System;

namespace Game.Data.Logic.Nodes
{
    /// <summary>Main-thread bound expression dispatch. Arguments and generated slots are cached once at Bind, never looked up at Evaluate.</summary>
    internal sealed class FunctionEvaluation
    {
        readonly LogicFunctionInfo info;
        readonly ExpressionFunction function;
        readonly IIntExpr[] integers;
        readonly ICondition[] booleans;
        readonly LogicValue[] values;
        readonly int generatedSlot;
        readonly bool valid;

        internal FunctionEvaluation(LogicFunctionInfo info, IIntExpr[] integers, ICondition[] booleans,
            string[] text, int[] indices, int[] slots)
        {
            this.info = info; this.integers = integers; this.booleans = booleans;
            function = info.BindExpression(); values = new LogicValue[info.Params.Length]; valid = true;
            generatedSlot = slots.Length > 0 ? slots[0] : -1;
            for (int i = 0; i < values.Length; i++)
            {
                var parameter = info.Params[i];
                if (!ArgumentMatches(parameter.ArgKind, integers[i], booleans[i], text[i])) valid = false;
                int value = parameter.ArgKind == LogicArgKind.Fact ? slots[i] : indices[i];
                values[i] = new LogicValue(parameter.ArgKind == LogicArgKind.Bool ? LogicType.Bool : LogicType.Int, value, text[i]);
                if (parameter.ArgKind == LogicArgKind.Fact && slots[i] < 0 || parameter.ArgKind == LogicArgKind.Ref && value == 0)
                    valid = false;
                if ((function == ExpressionFunction.ClueKnown || function == ExpressionFunction.MysterySolved || function == ExpressionFunction.BeatCount) && generatedSlot < 0)
                    valid = false;
            }
        }
        internal static bool ValidateShape(LogicFunctionInfo info, IIntExpr[] integers, ICondition[] booleans, string[] text,
            Game.Data.Validation.ValidationReport report)
        {
            if (integers == null || booleans == null || text == null || integers.Length != info.Params.Length ||
                booleans.Length != info.Params.Length || text.Length != info.Params.Length)
            { report.Error("NAR-E004", "", "", "Compiled function argument shape is invalid."); return false; }
            bool valid = true;
            for (int i = 0; i < info.Params.Length; i++)
            {
                var kind = info.Params[i].ArgKind;
                if (ArgumentMatches(kind, integers[i], booleans[i], text[i])) continue;
                report.Error("NAR-E005", "", info.Params[i].Name, "Compiled function argument is missing or has the wrong type."); valid = false;
            }
            return valid;
        }
        static bool ArgumentMatches(LogicArgKind kind, IIntExpr integer, ICondition boolean, string text)
            => kind == LogicArgKind.Int ? integer != null && boolean == null && string.IsNullOrEmpty(text)
                : kind == LogicArgKind.Bool ? boolean != null && integer == null && string.IsNullOrEmpty(text)
                : text != null && integer == null && boolean == null;
        internal LogicValue Evaluate(ILogicContext context)
        {
            if (!valid) return new LogicValue(info.ReturnType, 0);
            for (int i = 0; i < values.Length; i++)
            {
                if (integers[i] != null) values[i] = new LogicValue(LogicType.Int, integers[i].Evaluate(context));
                else if (booleans[i] != null) values[i] = new LogicValue(LogicType.Bool, booleans[i].Evaluate(context) ? 1 : 0);
            }
            return info.InvokeBound(context, values, function, generatedSlot);
        }
    }
}
