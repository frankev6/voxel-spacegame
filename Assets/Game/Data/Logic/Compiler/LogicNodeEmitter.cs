using Game.Data.Logic.Nodes;

namespace Game.Data.Logic.Compiler
{
    /// <summary>Emits a serialized graph only after successful type checking.</summary>
    internal sealed class LogicNodeEmitter
    {
        readonly LogicTypeChecker types;
        readonly LogicFunctionRegistry functions;
        public LogicNodeEmitter(LogicTypeChecker types, LogicFunctionRegistry functions)
        { this.types = types; this.functions = functions; }

        public object Emit(LogicAst node)
        {
            if (node is LogicAstLiteral literal)
                return literal.Value is bool b ? (object)new BoolLiteral(b) : new IntLiteral((int)literal.Value);
            if (node is LogicAstVariable variable)
                return types.TypeOf(node) == LogicType.Bool ? (object)new FactBoolRead(variable.Name) : new FactIntRead(variable.Name);
            if (node is LogicAstUnary unary)
                return unary.Operator == LogicTokenKind.Not ? (object)new NotCondition((ICondition)Emit(unary.Operand)) :
                    new NegateExpr((IIntExpr)Emit(unary.Operand));
            if (node is LogicAstCall call) return Call(call.Name, call.Arguments);
            if (node is LogicAstCommand command) return Call(command.Name, command.Arguments);
            var binary = (LogicAstBinary)node;
            object left = Emit(binary.Left), right = Emit(binary.Right);
            switch (binary.Operator)
            {
                case LogicTokenKind.And: return new AndCondition((ICondition)left, (ICondition)right);
                case LogicTokenKind.Or: return new OrCondition((ICondition)left, (ICondition)right);
                case LogicTokenKind.Plus: return new AddExpr((IIntExpr)left, (IIntExpr)right);
                case LogicTokenKind.Minus: return new SubtractExpr((IIntExpr)left, (IIntExpr)right);
            }
            CompareOp op;
            switch (binary.Operator)
            {
                case LogicTokenKind.Equal: op = CompareOp.Equal; break;
                case LogicTokenKind.NotEqual: op = CompareOp.NotEqual; break;
                case LogicTokenKind.Less: op = CompareOp.Less; break;
                case LogicTokenKind.LessEqual: op = CompareOp.LessEqual; break;
                case LogicTokenKind.Greater: op = CompareOp.Greater; break;
                default: op = CompareOp.GreaterEqual; break;
            }
            return types.TypeOf(binary.Left) == LogicType.Bool
                ? (object)new CompareBoolCondition(op, (ICondition)left, (ICondition)right)
                : new CompareIntCondition(op, (IIntExpr)left, (IIntExpr)right);
        }

        object Call(string name, System.Collections.Generic.IReadOnlyList<LogicAst> arguments)
        {
            functions.TryGet(name, out var function);
            var values = new object[function.Params.Length];
            for (int i = 0; i < values.Length; i++)
            {
                var parameter = function.Params[i];
                if (i >= arguments.Count)
                {
                    values[i] = parameter.ArgKind == LogicArgKind.Tag ? (object)"here" : new IntLiteral(parameter.DefaultInt);
                    continue;
                }
                var argument = arguments[i];
                if (parameter.ArgKind == LogicArgKind.Int || parameter.ArgKind == LogicArgKind.Bool)
                    values[i] = Emit(argument);
                else values[i] = argument is LogicAstVariable variable ? variable.Name : ((LogicAstLiteral)argument).Value;
            }
            return function.CreateNode(values);
        }
    }
}
