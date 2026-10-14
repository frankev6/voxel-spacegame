using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Validation;
using static Game.Editor.Narrative.Dialog.DialogCommandTableGenerator;

namespace Game.Editor.Narrative.Dialog
{
    internal static class DialogSyntaxTraversal
    {
        internal static IEnumerable<DialogStatementAst> Walk(IReadOnlyList<DialogStatementAst> block)
        {
            foreach (var statement in block)
            {
                yield return statement;
                if (statement is OptionGroupStmt group)
                    foreach (var option in group.Options)
                    { yield return option; foreach (var child in Walk(option.Body)) yield return child; }
                else if (statement is IfStmt conditional)
                {
                    foreach (var branch in conditional.Branches) foreach (var child in Walk(branch.Body)) yield return child;
                    foreach (var child in Walk(conditional.Else)) yield return child;
                }
            }
        }
    }

    /// <summary>SPEC-41 §5.2: source semantics and exact generated-artifact checks; never executes logic.</summary>
    public sealed class DialogValidatorRules : INarrativeRuleProvider
    {
        public int Order => 10;
        public void Run(NarrativeValidationContext ctx, ValidationReport report)
        {
            var sources = ReadSources(Root); var scripts = Parse(sources);
            Validate(ctx, scripts, report);
            var plan = Prepare(ctx, sources, Root);
            CheckGenerated(ctx, plan, Root, report);
        }
        internal static string NormalizeCommand(string text)
        {
            var result = new StringBuilder(); bool quoted = false, escaped = false, space = false;
            foreach (char c in text)
            {
                if (quoted)
                {
                    result.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                }
                else if (char.IsWhiteSpace(c)) space = result.Length > 0;
                else { if (space) result.Append(' '); space = false; result.Append(c); if (c == '"') quoted = true; }
            }
            return result.ToString();
        }
        internal static void CheckGenerated(NarrativeValidationContext ctx, Plan plan, string root, ValidationReport report)
        {
            var tables = ctx.Content.All<DialogCommandTableDefinition>();
            if (tables.Count != 1 || tables[0].IdString != "dialog_command_table" || tables[0].SourcesHash != plan.Hash ||
                !CommandsCurrent(tables[0], plan) || !BytesCurrent(root + "/_Generated/facts.yarn", plan.Facts) ||
                !BytesCurrent(root + "/_Generated/logic.ysls.json", plan.Ysls))
                report.Error("DLG-E013", root + "/_Generated", "", "Dialog command table or generated files are stale; rebuild dialog tables.");
        }
        internal void Validate(NarrativeValidationContext ctx, IReadOnlyList<DialogScriptAst> scripts, ValidationReport report)
        {
            var nodes = new Dictionary<string, (string Path, DialogNodeAst Node)>(StringComparer.Ordinal);
            var ids = new Dictionary<string, (string Path, int Line)>(StringComparer.Ordinal);
            var edges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var script in scripts)
            {
                report.Merge(script.Diagnostics); int draftCount = 0;
                foreach (var node in script.Nodes)
                {
                    if (nodes.TryGetValue(node.Title ?? "", out var previous))
                        Error("DLG-E003", script.Path, node.Line, "Duplicate node '" + node.Title + "'; first at " + previous.Path + ":" + previous.Node.Line + ".");
                    else nodes.Add(node.Title ?? "", (script.Path, node));
                    if (!edges.ContainsKey(node.Title ?? "")) edges.Add(node.Title ?? "", new HashSet<string>(StringComparer.Ordinal));
                    foreach (var statement in DialogSyntaxTraversal.Walk(node.Body))
                    {
                        if (statement is JumpStmt jump) edges[node.Title ?? ""].Add(jump.Node);
                        else if (statement is DetourStmt detour) edges[node.Title ?? ""].Add(detour.Node);
                        if (statement is LineStmt line) Tags(script.Path, line.Line, line.LineId, line.Tags, false, "", line.SpeakerName);
                        else if (statement is OptionStmt option)
                        {
                            Tags(script.Path, option.Line, option.LineId, option.Tags, true, option.Condition, "");
                            if (!string.IsNullOrEmpty(option.Condition)) Condition(script.Path, option.Line, option.Condition);
                        }
                        else if (statement is IfStmt conditional)
                            foreach (var branch in conditional.Branches) Condition(script.Path, conditional.Line, branch.Condition);
                        else if (statement is SetStmt set) Assignment(script.Path, set);
                    }
                    Block(script.Path, node.Body);
                    Menus(script.Path, node.Body, false);
                }
                if (draftCount > 0) report.Info("DLG-I030", script.Path, "", draftCount + " draft lines/options.");

                void Tags(string path, int line, string id, string[] tags, bool option, string condition, string speaker)
                {
                    if (tags.Contains("draft")) draftCount++;
                    if (!Regex.IsMatch(id ?? "", "^[a-z0-9_]{1,64}$") || tags.Count(tag => tag.StartsWith("line:", StringComparison.Ordinal)) != 1)
                        Error("DLG-E004", path, line, "Line/option requires exactly one valid #line ID.");
                    foreach (string tag in tags)
                    {
                        if (tag.StartsWith("line:", StringComparison.Ordinal))
                        {
                            string value = tag.Substring(5);
                            if (ids.TryGetValue(value, out var first))
                                Error("DLG-E005", path, line, "Duplicate line ID '" + value + "'; first at " + first.Path + ":" + first.Line + ".");
                            else ids.Add(value, (path, line));
                        }
                        else if (tag == "draft") { }
                        else if (tag.StartsWith("portrait:", StringComparison.Ordinal) || tag.StartsWith("speaker:", StringComparison.Ordinal))
                        {
                            string value = tag.Substring(tag.IndexOf(':') + 1);
                            if (option || !ctx.Content.All<SpeakerDefinition>().Any(asset => asset.IdString == value || asset.FormerIds.Contains(value)))
                                Error("DLG-E010", path, line, "Unknown or misplaced speaker tag: " + tag + ".");
                        }
                        else if (tag.StartsWith("illustration:", StringComparison.Ordinal))
                        {
                            string value = tag.Substring(13);
                            if (option || !ctx.Content.All<IllustrationDefinition>().Any(asset => asset.IdString == value || asset.FormerIds.Contains(value)))
                                Error("DLG-E010", path, line, "Unknown or misplaced illustration tag: " + tag + ".");
                        }
                        else if (tag == "quest" || tag == "leave" || tag == "hide")
                        {
                            if (!option || tag == "hide" && string.IsNullOrEmpty(condition)) Error("DLG-E006", path, line, "Tag is not valid here: " + tag + ".");
                        }
                        else Error("DLG-E006", path, line, "Unsupported tag: " + tag + ".");
                    }
                    if (!string.IsNullOrEmpty(speaker) && !ctx.Content.All<SpeakerDefinition>().Any(asset => asset.DisplayName == speaker))
                        Error("DLG-E010", path, line, "Unknown speaker display name: " + speaker + ".");
                }
            }
            foreach (var script in scripts)
                foreach (var node in script.Nodes)
                    foreach (var statement in DialogSyntaxTraversal.Walk(node.Body))
                    {
                        string target = statement is JumpStmt jump ? jump.Node : statement is DetourStmt detour ? detour.Node : null;
                        if (target != null && !nodes.ContainsKey(target)) Error("DLG-E002", script.Path, statement.Line, "Unknown target node: " + target + ".");
                    }
            var reachable = new HashSet<string>(StringComparer.Ordinal); var queue = new Queue<string>();
            foreach (var beat in ctx.Content.All<StoryBeatDefinition>())
                if (!string.IsNullOrEmpty(beat.YarnNode) && nodes.ContainsKey(beat.YarnNode) && reachable.Add(beat.YarnNode)) queue.Enqueue(beat.YarnNode);
            while (queue.Count != 0)
                foreach (string target in edges[queue.Dequeue()]) if (nodes.ContainsKey(target) && reachable.Add(target)) queue.Enqueue(target);
            foreach (var pair in nodes)
                if (!reachable.Contains(pair.Key)) report.Warning("DLG-W022", pair.Value.Path, "line:" + pair.Value.Node.Line, "Node is not reachable from any beat: " + pair.Key + ".");

            void Error(string code, string path, int line, string message) => report.Error(code, path, "line:" + line, message);
            void Forward(string code, string path, int line, ValidationReport local)
            {
                foreach (var diagnostic in local.Entries)
                    report.Add(diagnostic.Severity, diagnostic.Severity == ValidationSeverity.Error ? code : diagnostic.Code,
                        path, "line:" + line, diagnostic.Code + ": " + diagnostic.Message);
            }
            void Condition(string path, int line, string source)
            {
                if (string.IsNullOrWhiteSpace(source)) { Error("DLG-E008", path, line, "Condition expression is empty."); return; }
                var local = new ValidationReport(path);
                ctx.Compiler.TryCompileExpression(source, new LogicCompileOptions(LogicUsage.DialogCondition, path, "line:" + line), local, out _);
                Forward("DLG-E008", path, line, local);
            }
            void Assignment(string path, SetStmt set)
            {
                int slot = ctx.Facts.SlotOf(set.Fact);
                if (slot < 0 || !ctx.Facts.IsWritableByContent(slot)) { Error("DLG-E009", path, set.Line, "Set target must be a known authored fact: " + set.Fact + "."); return; }
                var local = new ValidationReport(path);
                try
                {
                    var checker = new LogicTypeChecker(ctx.Facts, ctx.Refs, new LogicFunctionRegistry(), new LogicCompileOptions(LogicUsage.DialogCondition, path, "line:" + set.Line), local);
                    checker.Check(LogicParser.ParseExpression(set.Expression), ctx.Facts.KindAt(slot) == FactKind.Bool ? LogicType.Bool : LogicType.Int);
                }
                catch (LogicDiagnostics diagnostic) { local.Error(diagnostic.Code, path, "line:" + set.Line, diagnostic.Message); }
                Forward("DLG-E009", path, set.Line, local);
            }
            void Block(string path, IReadOnlyList<DialogStatementAst> block)
            {
                bool terminal = false;
                foreach (var statement in block)
                {
                    if (statement is CommentStmt) continue;
                    if (terminal) Error("DLG-E014", path, statement.Line, "Statement follows a terminal command in the same block.");
                    if (statement is CommandStmt command)
                    {
                        if (NormalizeCommand(command.Text) != command.Text) Error("DLG-E012", path, command.Line, "Command text must have single spaces between tokens and no surrounding whitespace.");
                        var local = new ValidationReport(path);
                        ctx.Compiler.TryCompileCommand(command.Text, new LogicCompileOptions(LogicUsage.DialogCommand, path, "line:" + command.Line), local, out var effect);
                        Forward("DLG-E007", path, command.Line, local); terminal |= effect?.Flow == EffectFlow.Terminal;
                    }
                    else if (statement is OptionGroupStmt group) foreach (var option in group.Options) Block(path, option.Body);
                    else if (statement is IfStmt conditional)
                    { foreach (var branch in conditional.Branches) Block(path, branch.Body); Block(path, conditional.Else); }
                }
            }
            void Menus(string path, IReadOnlyList<DialogStatementAst> block, bool outerCondition)
            {
                int count = 0, line = 0; bool allConditional = true;
                void Flush()
                {
                    if (count > 9) report.Warning("DLG-W020", path, "line:" + line, "Option group has " + count + " options.");
                    if (count > 0 && allConditional) report.Warning("DLG-W021", path, "line:" + line, "Every option has an availability or outer visibility condition.");
                    count = 0; line = 0; allConditional = true;
                }
                void Include(IReadOnlyList<DialogStatementAst> items, bool visibleWhen)
                {
                    foreach (var item in items)
                        if (item is OptionGroupStmt group)
                            foreach (var option in group.Options)
                            { if (count == 0) line = option.Line; count++; allConditional &= visibleWhen || !string.IsNullOrEmpty(option.Condition); }
                        else if (item is IfStmt conditional)
                        { foreach (var branch in conditional.Branches) Include(branch.Body, true); Include(conditional.Else, true); }
                }
                void NestedOptionBodies(IReadOnlyList<DialogStatementAst> items)
                {
                    foreach (var item in items)
                        if (item is OptionGroupStmt group) foreach (var option in group.Options) Menus(path, option.Body, false);
                        else if (item is IfStmt conditional)
                        { foreach (var branch in conditional.Branches) NestedOptionBodies(branch.Body); NestedOptionBodies(conditional.Else); }
                }
                foreach (var statement in block)
                {
                    if (statement is CommentStmt) continue;
                    if (statement is OptionGroupStmt || statement is IfStmt conditionalOptions && OptionOnly(conditionalOptions))
                        Include(new[] { statement }, outerCondition);
                    else Flush();
                    if (statement is OptionGroupStmt group) foreach (var option in group.Options) Menus(path, option.Body, false);
                    else if (statement is IfStmt conditional)
                    {
                        if (OptionOnly(conditional))
                        { foreach (var branch in conditional.Branches) NestedOptionBodies(branch.Body); NestedOptionBodies(conditional.Else); }
                        else { foreach (var branch in conditional.Branches) Menus(path, branch.Body, true); Menus(path, conditional.Else, true); }
                    }
                }
                Flush();
            }
        }
        static bool OptionOnly(IfStmt conditional)
        {
            bool Only(IReadOnlyList<DialogStatementAst> block) => block.All(statement => statement is CommentStmt || statement is OptionGroupStmt || statement is IfStmt nested && OptionOnly(nested));
            return conditional.Branches.All(branch => Only(branch.Body)) && Only(conditional.Else);
        }
    }
}
