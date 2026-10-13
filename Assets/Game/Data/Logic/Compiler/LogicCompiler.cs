using System;
using System.Collections.Generic;
using Game.Data.Validation;

namespace Game.Data.Logic.Compiler
{
    /// <summary>Authoring-time parse, validation and graph emission. Runtime code only binds compiled graphs.</summary>
    public sealed class LogicCompiler
    {
        public const int Version = 1;
        readonly FactCatalog facts;
        readonly RefResolverRegistry refs;
        readonly LogicFunctionRegistry functions;
        public LogicCompiler(FactCatalog facts, RefResolverRegistry refs, LogicFunctionRegistry functions)
        {
            this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
            this.refs = refs ?? throw new ArgumentNullException(nameof(refs));
            this.functions = functions ?? throw new ArgumentNullException(nameof(functions));
        }

        public bool Compile(CompiledCondition target, in LogicCompileOptions options, ValidationReport report)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            bool success = Expression(target.Source, LogicType.Bool, options, report, out object root);
            target.SetCompiled(root as ICondition, !success);
            return success;
        }
        public bool Compile(CompiledIntExpr target, in LogicCompileOptions options, ValidationReport report)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            bool success = Expression(target.Source, LogicType.Int, options, report, out object root);
            target.SetCompiled(root as IIntExpr, !success);
            return success;
        }
        public bool Compile(CompiledEffectList target, in LogicCompileOptions options, ValidationReport report)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var effects = new List<IEffect>(); bool success = true;
            for (int i = 0; i < target.Source.Count; i++)
            {
                // Empty lines within an authored list are errors; an empty list is valid.
                var lineOptions = new LogicCompileOptions(options.Usage, options.AssetPath, options.Field + "[" + i + "]");
                if (TryCompileCommand(target.Source[i], lineOptions, report, out var effect)) effects.Add(effect);
                else success = false;
            }
            target.SetCompiled(effects.ToArray(), !success);
            return success;
        }
        public bool TryCompileCommand(string commandText, in LogicCompileOptions options, ValidationReport report, out IEffect effect)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            effect = null;
            try
            {
                var ast = LogicParser.ParseCommand(commandText);
                var checker = new LogicTypeChecker(facts, refs, functions, options, report);
                if (!checker.Check(ast)) return false;
                effect = (IEffect)new LogicNodeEmitter(checker, functions).Emit(ast);
                return true;
            }
            catch (LogicDiagnostics diagnostic) { Report(diagnostic, options, report); return false; }
        }
        public bool TryCompileExpression(string expressionText, in LogicCompileOptions options, ValidationReport report, out ICondition condition)
        {
            bool success = Expression(expressionText, LogicType.Bool, options, report, out object root);
            condition = root as ICondition;
            return success;
        }

        bool Expression(string source, LogicType expected, in LogicCompileOptions options, ValidationReport report, out object root)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            root = null;
            if (string.IsNullOrWhiteSpace(source)) return true;
            try
            {
                var ast = LogicParser.ParseExpression(source);
                var checker = new LogicTypeChecker(facts, refs, functions, options, report);
                if (!checker.Check(ast, expected)) return false;
                root = new LogicNodeEmitter(checker, functions).Emit(ast);
                return true;
            }
            catch (LogicDiagnostics diagnostic) { Report(diagnostic, options, report); return false; }
        }

        static void Report(LogicDiagnostics diagnostic, in LogicCompileOptions options, ValidationReport report)
        {
            string at = diagnostic.Line + ":" + diagnostic.Column;
            string message = diagnostic.Code == "NAR-E002" ? "Unterminated string at " + at + "." :
                diagnostic.Code == "NAR-E013" ? "Empty command line." : "Syntax error at " + at + ": " + diagnostic.Message;
            report.Error(diagnostic.Code, options.AssetPath, options.Field + " (" + at + ")", message);
        }
    }
}
