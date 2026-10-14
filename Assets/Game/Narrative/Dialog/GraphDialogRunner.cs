using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;

namespace Game.Narrative.Dialog
{
    /// <summary>SPEC-41 compiled graph interpreter. No source parsing or effect compilation at runtime.</summary>
    public sealed class GraphDialogRunner : IDialogRunner
    {
        enum Wait { None, Line, Options, Command }
        struct Frame { public int Node, Pc; }
        readonly DialogProgramDefinition program;
        readonly ILogicContext context;
        readonly DialogMemory memory;
        readonly Dictionary<string, int> nodes = new(StringComparer.Ordinal);
        readonly Frame[] stack = new Frame[16];
        readonly List<int> options = new(16);
        DialogOption[] presented = new DialogOption[16];
        int depth;
        Wait waiting;
        bool driving;
        IDialogRunnerHost host;
        public GraphDialogRunner(DialogProgramDefinition program, ILogicContext context, DialogMemory memory)
        {
            this.program = program ?? throw new ArgumentNullException(nameof(program));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            for (int i = 0; i < program.Nodes.Count; i++)
            {
                var node = program.Nodes[i];
                if (string.IsNullOrEmpty(node.Title) || node.Start < 0 || node.Length <= 0 || (long)node.Start + node.Length > program.Code.Count || !nodes.TryAdd(node.Title, i))
                    throw new ArgumentException("Invalid compiled dialog node.", nameof(program));
            }
            foreach (var condition in program.Conditions)
                if (condition == null || condition.IsStale || condition.HasErrors) throw new ArgumentException("Invalid compiled dialog condition.", nameof(program));
            foreach (var expression in program.IntExprs)
                if (expression == null || expression.IsStale || expression.HasErrors) throw new ArgumentException("Invalid compiled dialog expression.", nameof(program));
        }
        public bool HasNode(string node) => node != null && nodes.ContainsKey(node);
        public bool IsRunning => depth != 0;
        public void Start(string node, IDialogRunnerHost receiver)
        {
            if (driving || IsRunning) throw new InvalidOperationException("Dialog runner is already active.");
            if (!nodes.TryGetValue(node, out int index)) throw new ArgumentException("Unknown dialog node: " + node);
            host = receiver ?? throw new ArgumentNullException(nameof(receiver)); options.Clear(); waiting = Wait.None;
            stack[depth++] = new Frame { Node = index, Pc = program.Nodes[index].Start };
            driving = true;
            try { host.OnNodeStarted(node); Run(); } finally { driving = false; }
        }
        public void Continue() => Advance(Wait.Line);
        public void Resume() => Advance(Wait.Command);
        void Advance(Wait expected)
        {
            if (driving) throw new InvalidOperationException("Dialog runner cannot be reentered.");
            if (!IsRunning || waiting != expected) return;
            waiting = Wait.None; driving = true; try { Run(); } finally { driving = false; }
        }
        public void SelectOption(int optionId)
        {
            if (driving) throw new InvalidOperationException("Dialog runner cannot be reentered.");
            if (!IsRunning || waiting != Wait.Options) return;
            for (int i = 0; i < options.Count; i++)
                if (presented[i].Id == optionId && presented[i].Available)
                {
                    stack[depth - 1].Pc = program.Code[program.Code[options[i]].C + 1].A;
                    options.Clear(); waiting = Wait.None; driving = true;
                    try { Run(); } finally { driving = false; } return;
                }
        }
        public void Stop()
        {
            if (driving) throw new InvalidOperationException("Dialog runner cannot be reentered.");
            driving = true; try { EndAll(); } finally { driving = false; }
        }
        string Text(int index) => program.Strings[index];
        void CompleteTop()
        {
            string title = program.Nodes[stack[depth - 1].Node].Title;
            memory.IncrementVisit(title); host.OnNodeCompleted(title);
        }
        void EndAll()
        {
            if (!IsRunning) return;
            CompleteTop(); depth = 0; waiting = Wait.None; options.Clear(); host.OnDialogComplete();
        }
        void Return()
        {
            CompleteTop(); depth--;
            if (depth == 0) { waiting = Wait.None; options.Clear(); host.OnDialogComplete(); }
        }
        bool Condition(int index)
        {
            var scope = context.Scope;
            context.PushScope(new LogicScope(LogicUsage.DialogCondition, scope.QuestIndex, scope.QuestInstance, scope.BeatIndex, scope.HasBody, scope.Body));
            try { return program.Conditions[index].Evaluate(context); } finally { context.PopScope(); }
        }
        int Integer(int index)
        {
            var scope = context.Scope;
            context.PushScope(new LogicScope(LogicUsage.DialogCondition, scope.QuestIndex, scope.QuestInstance, scope.BeatIndex, scope.HasBody, scope.Body));
            try { return program.IntExprs[index].Evaluate(context); } finally { context.PopScope(); }
        }
        void Run()
        {
            int steps = 0;
            while (IsRunning && waiting == Wait.None)
            {
                if (++steps > 10000) { GameLog.Error("Dialog", "Dialog instruction limit exceeded (possible loop)."); EndAll(); return; }
                ref var frame = ref stack[depth - 1]; var node = program.Nodes[frame.Node];
                if (frame.Pc < node.Start || frame.Pc >= node.Start + node.Length) throw new InvalidOperationException("Dialog program counter escaped its node.");
                var instruction = program.Code[frame.Pc++];
                switch (instruction.Op)
                {
                    case DialogOp.Line:
                        waiting = Wait.Line; int tag = instruction.C;
                        host.OnLine(new DialogLine(Text(instruction.B), Text(instruction.A), Text(tag), Text(tag+1), Text(tag+2), Text(tag+3), node.Title, Text(tag+4) == "d")); break;
                    case DialogOp.Option: options.Add(frame.Pc - 1); break;
                    case DialogOp.ShowOptions:
                        if (presented.Length < options.Count) Array.Resize(ref presented, options.Count);
                        for (int i = 0; i < options.Count; i++)
                        {
                            int optionPc = options[i], record = program.Code[optionPc].C, flags = program.Code[record+2].A;
                            var option = program.Code[optionPc]; int condition = program.Code[record].A;
                            presented[i] = new DialogOption { Id = record, Text = Text(option.A), LineId = Text(option.B),
                                Available = condition < 0 || Condition(condition), HideWhenUnavailable = (flags & 1) != 0,
                                IsQuest = (flags & 2) != 0, IsLeave = (flags & 4) != 0 };
                        }
                        waiting = Wait.Options; host.OnOptions(presented, options.Count); break;
                    case DialogOp.BranchIfFalse: if (!Condition(instruction.A)) frame.Pc = instruction.B; break;
                    case DialogOp.Goto: frame.Pc = instruction.A; break;
                    case DialogOp.Jump:
                    case DialogOp.Detour:
                        if (!nodes.TryGetValue(Text(instruction.A), out int target)) throw new InvalidOperationException("Unknown compiled jump target.");
                        if (instruction.Op == DialogOp.Jump) { CompleteTop(); frame.Node = target; frame.Pc = program.Nodes[target].Start; }
                        else
                        {
                            if (depth == stack.Length) { GameLog.Error("Dialog", "Dialog detour depth exceeds 16."); EndAll(); return; }
                            stack[depth++] = new Frame { Node = target, Pc = program.Nodes[target].Start };
                        }
                        host.OnNodeStarted(program.Nodes[target].Title); break;
                    case DialogOp.Return:
                    case DialogOp.EndNode: Return(); break;
                    case DialogOp.Stop: EndAll(); break;
                    case DialogOp.SetBool:
                    case DialogOp.SetInt:
                        int slot = context.Facts.Catalog.SlotOf(Text(instruction.A));
                        if (slot < 0 || !context.Facts.Catalog.IsWritableByContent(slot)) throw new InvalidOperationException("Invalid dialog fact write.");
                        context.Facts.SetBySlot(slot, instruction.Op == DialogOp.SetBool ? (Condition(instruction.B) ? 1 : 0) : Integer(instruction.B), FactWriteSource.Dialog); break;
                    case DialogOp.Command:
                        switch (host.OnCommand(Text(instruction.A)))
                        { case DialogCommandResult.Block: waiting = Wait.Command; break; case DialogCommandResult.Stop: EndAll(); break; }
                        break;
                    default: throw new InvalidOperationException("Unknown dialog opcode.");
                }
            }
        }
    }
}
