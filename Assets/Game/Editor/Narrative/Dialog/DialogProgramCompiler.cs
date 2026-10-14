using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.Narrative.Dialog
{
    /// <summary>DLG-007: lower the shared validated AST to the specified graph format.</summary>
    public sealed class DialogProgramCompiler
    {
        readonly List<DialogInstruction> code = new();
        readonly List<DialogNodeRecord> nodes = new();
        readonly List<string> strings = new(), conditions = new(), integers = new();
        readonly FactCatalog facts;
        DialogProgramCompiler(FactCatalog facts) => this.facts = facts;
        int Emit(DialogOp op, int a = 0, int b = 0, int c = 0)
        { int pc = code.Count; code.Add(new DialogInstruction { Op = op, A = a, B = b, C = c }); return pc; }
        int String(string value) { int index = strings.Count; strings.Add(value ?? ""); return index; }
        int Condition(string source) { int index = conditions.Count; conditions.Add(source); return index; }
        void GotoTarget(int pc, int target) { var value = code[pc]; value.A = target; code[pc] = value; }
        void BranchTarget(int pc, int target) { var value = code[pc]; value.B = target; code[pc] = value; }
        static string Tag(string[] tags, string key) => tags.FirstOrDefault(value => value.StartsWith(key + ":", StringComparison.Ordinal))?.Substring(key.Length + 1) ?? "";
        int Tags(string speaker, string[] tags)
        {
            int start = String(speaker); String(Tag(tags, "portrait")); String(Tag(tags, "speaker")); String(Tag(tags, "illustration"));
            String(tags.Contains("draft") ? "d" : ""); return start;
        }
        static bool ChoiceOnly(DialogStatementAst statement)
        {
            if (statement is OptionGroupStmt) return true;
            if (statement is IfStmt conditional)
                return conditional.Branches.All(branch => branch.Body.Count != 0 && branch.Body.All(item => item is CommentStmt || ChoiceOnly(item))) &&
                    conditional.Else.All(item => item is CommentStmt || ChoiceOnly(item));
            return false;
        }
        void Body(IReadOnlyList<DialogStatementAst> body)
        {
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i] is CommentStmt) continue;
                if (ChoiceOnly(body[i]))
                {
                    var menu = new List<DialogStatementAst>();
                    do { menu.Add(body[i++]); } while (i < body.Count && (body[i] is CommentStmt || ChoiceOnly(body[i])));
                    i--; Menu(menu); continue;
                }
                Statement(body[i]);
            }
        }
        void Statement(DialogStatementAst item)
        {
            switch (item)
            {
                case CommentStmt: break;
                case LineStmt line: Emit(DialogOp.Line, String(line.Text), String(line.LineId), Tags(line.SpeakerName, line.Tags)); break;
                case CommandStmt command: Emit(DialogOp.Command, String(command.Text)); break;
                case JumpStmt jump: Emit(DialogOp.Jump, String(jump.Node)); break;
                case DetourStmt detour: Emit(DialogOp.Detour, String(detour.Node)); break;
                case ReturnStmt: Emit(DialogOp.Return); break;
                case StopStmt: Emit(DialogOp.Stop); break;
                case SetStmt set:
                    int slot = facts.SlotOf(set.Fact);
                    if (facts.KindAt(slot) == FactKind.Bool) Emit(DialogOp.SetBool, String(set.Fact), Condition(set.Expression));
                    else { int index = integers.Count; integers.Add(set.Expression); Emit(DialogOp.SetInt, String(set.Fact), index); }
                    break;
                case IfStmt conditional: Branches(conditional, Body); break;
                default: throw new InvalidOperationException("Unexpected dialog AST statement.");
            }
        }
        void Branches(IfStmt conditional, Action<IReadOnlyList<DialogStatementAst>> emitBody)
        {
            var ends = new List<int>();
            foreach (var branch in conditional.Branches)
            {
                int check = Emit(DialogOp.BranchIfFalse, Condition(branch.Condition)); emitBody(branch.Body);
                ends.Add(Emit(DialogOp.Goto)); BranchTarget(check, code.Count);
            }
            emitBody(conditional.Else);
            foreach (int end in ends) GotoTarget(end, code.Count);
        }
        void Menu(IReadOnlyList<DialogStatementAst> statements)
        {
            var choices = new List<OptionStmt>();
            void Collect(IReadOnlyList<DialogStatementAst> list)
            {
                foreach (var item in list)
                    if (item is OptionGroupStmt group) choices.AddRange(group.Options);
                    else if (item is IfStmt conditional)
                    { foreach (var branch in conditional.Branches) Collect(branch.Body); Collect(conditional.Else); }
            }
            Collect(statements);
            int skip = Emit(DialogOp.Goto);
            var records = new Dictionary<OptionStmt, int>();
            foreach (var choice in choices)
            {
                int record = code.Count; records.Add(choice, record);
                // Four A operands are data words, skipped by the preceding Goto; never executable opcodes.
                Emit(DialogOp.Goto, string.IsNullOrEmpty(choice.Condition) ? -1 : Condition(choice.Condition));
                Emit(DialogOp.Goto);
                Emit(DialogOp.Goto, (choice.Tags.Contains("hide") ? 1 : 0) | (choice.Tags.Contains("quest") ? 2 : 0) | (choice.Tags.Contains("leave") ? 4 : 0));
                Emit(DialogOp.Goto, Tags("", choice.Tags));
            }
            GotoTarget(skip, code.Count); int first = code.Count;
            void Headers(IReadOnlyList<DialogStatementAst> list)
            {
                foreach (var item in list)
                    if (item is OptionGroupStmt group)
                        foreach (var choice in group.Options) Emit(DialogOp.Option, String(choice.Text), String(choice.LineId), records[choice]);
                    else if (item is IfStmt conditional) Branches(conditional, Headers);
            }
            Headers(statements); Emit(DialogOp.ShowOptions, first, choices.Count);
            var ends = new List<int>(); ends.Add(Emit(DialogOp.Goto));
            foreach (var choice in choices)
            { GotoTarget(records[choice] + 1, code.Count); Body(choice.Body); ends.Add(Emit(DialogOp.Goto)); }
            foreach (int end in ends) GotoTarget(end, code.Count);
        }
        public static DialogProgramDefinition Compile(IReadOnlyDictionary<string, string> sources, ContentCatalog content, ValidationReport report)
        {
            var context = DialogCommandTableGenerator.Context(content, report);
            var scripts = DialogCommandTableGenerator.Parse(sources);
            new DialogValidatorRules().Validate(context, scripts, report);
            if (report.HasErrors) return null;
            var compiler = new DialogProgramCompiler(context.Facts);
            foreach (var node in scripts.SelectMany(script => script.Nodes).OrderBy(node => node.Title, StringComparer.Ordinal))
            {
                int start = compiler.code.Count; compiler.Body(node.Body); compiler.Emit(DialogOp.EndNode);
                compiler.nodes.Add(new DialogNodeRecord { Title = node.Title, Start = start, Length = compiler.code.Count - start });
            }
            var result = ScriptableObject.CreateInstance<DialogProgramDefinition>();
            result.name = "dialog_program";
            try
            {
                using var serialized = new SerializedObject(result);
                serialized.FindProperty("id").stringValue = "dialog_program";
                serialized.FindProperty("sourcesHash").ulongValue = DialogCommandTableGenerator.Prepare(context, sources, DialogCommandTableGenerator.Root).Hash;
                var rows = serialized.FindProperty("nodes"); rows.arraySize = compiler.nodes.Count;
                for (int i = 0; i < rows.arraySize; i++)
                { var row = rows.GetArrayElementAtIndex(i); var node = compiler.nodes[i]; row.FindPropertyRelative("Title").stringValue = node.Title; row.FindPropertyRelative("Start").intValue = node.Start; row.FindPropertyRelative("Length").intValue = node.Length; }
                rows = serialized.FindProperty("code"); rows.arraySize = compiler.code.Count;
                for (int i = 0; i < rows.arraySize; i++)
                { var row = rows.GetArrayElementAtIndex(i); var op = compiler.code[i]; row.FindPropertyRelative("Op").intValue = (int)op.Op; row.FindPropertyRelative("A").intValue = op.A; row.FindPropertyRelative("B").intValue = op.B; row.FindPropertyRelative("C").intValue = op.C; }
                rows = serialized.FindProperty("strings"); rows.arraySize = compiler.strings.Count;
                for (int i = 0; i < rows.arraySize; i++) rows.GetArrayElementAtIndex(i).stringValue = compiler.strings[i];
                rows = serialized.FindProperty("conditions"); rows.arraySize = compiler.conditions.Count;
                for (int i = 0; i < rows.arraySize; i++) rows.GetArrayElementAtIndex(i).FindPropertyRelative("source").stringValue = compiler.conditions[i];
                rows = serialized.FindProperty("intExprs"); rows.arraySize = compiler.integers.Count;
                for (int i = 0; i < rows.arraySize; i++) rows.GetArrayElementAtIndex(i).FindPropertyRelative("source").stringValue = compiler.integers[i];
                serialized.ApplyModifiedPropertiesWithoutUndo(); result.CompileLogic(context.Compiler, report.For("dialog_program"));
                if (report.HasErrors) { Object.DestroyImmediate(result); return null; }
                return result;
            }
            catch { Object.DestroyImmediate(result); throw; }
        }
        public static ValidationReport Generate(ContentCatalog content, string root = DialogCommandTableGenerator.Root)
        {
            var report = new ValidationReport("Dialog program generation");
            if (EditorApplication.isPlayingOrWillChangePlaymode) { report.Error("DLG-E013", root, "", "Dialog generation requires Edit mode."); return report; }
            var generated = Compile(DialogCommandTableGenerator.ReadSources(root), content, report);
            if (generated == null) return report;
            string path = root + "/_Generated/dialog_program.asset";
            try
            {
                var existing = AssetDatabase.LoadMainAssetAtPath(path);
                if (existing != null && !(existing is DialogProgramDefinition))
                { report.Error("DLG-E013", path, "", "Dialog program path has another asset type."); return report; }
                if (existing == null)
                { Directory.CreateDirectory(Path.GetDirectoryName(path)); AssetDatabase.Refresh(); AssetDatabase.CreateAsset(generated, path); generated = null; }
                else if (EditorJsonUtility.ToJson(existing) != EditorJsonUtility.ToJson(generated))
                { EditorUtility.CopySerialized(generated, existing); EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); }
            }
            finally { if (generated != null) Object.DestroyImmediate(generated); }
            return report;
        }
    }
}
