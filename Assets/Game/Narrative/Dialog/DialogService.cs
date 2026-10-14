using System;
using Game.Core;
using Game.Data.Logic;
using Game.Data.Logic.Nodes;

namespace Game.Narrative.Dialog
{
    /// <summary>SPEC-41 §5.3. Runner callbacks only queue work; Pump drives progression.</summary>
    public sealed class DialogService : IDialogRunnerHost
    {
        public const int MaxLinesPerPump = 16;
        readonly IDialogRunner runner;
        readonly DialogCommandTable commands;
        readonly ILogicContext context;
        readonly ILogicEffectHost effectHost;
        sealed class RollFrame
        {
            public string Node;
            public int Visit, Count;
            public RollFrame Parent;
            public readonly (int sides, int value)[] Values = new (int, int)[8];
        }
        RollFrame rollFrame;
        DialogRequest request;
        string lastPresentedNode;
        DialogOutcome outcome;
        IEffect terminal;
        Action previousContinuation;
        bool driving, pumping, awaitingContinue, awaitingOption, blocked, resumePending;
        bool finished, pageUnacknowledged, stopPending, optionPending, acknowledgeNotified;
        int selectedOption, nodeVisitIndex, generation, blockingGeneration;

        public DialogService(IDialogRunner runner, DialogCommandTable commands, ILogicContext context, DialogMemory memory)
        {
            this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
            effectHost = context as ILogicEffectHost;
        }
        public bool IsActive { get; private set; }
        public int CurrentBeatIndex => IsActive ? request.BeatIndex : 0;
        // DLG-012 read-only harness diagnostics; the service remains the only writer of runner state.
        internal string CurrentNode => IsActive ? rollFrame?.Node ?? lastPresentedNode ?? request.StartNode ?? "" : "";
        internal bool CanSelectOption => IsActive && awaitingOption && !finished && !blocked && !optionPending;
        internal bool CanAcknowledgeEnd => IsActive && finished && pageUnacknowledged && !stopPending;
        public DialogMemory Memory { get; }
        public event Action<DialogRequest> Started;
        public event Action<DialogLine> LinePresented;
        public event Action<DialogOption[], int> OptionsPresented;
        public event Action PageCleared;
        public event Action WaitingForEndAcknowledge;
        public event Action<DialogEnded> Ended;

        public bool TryStart(in DialogRequest next)
        {
            if (IsActive || driving || pumping) { GameLog.Warn("Dialog", "Dialog already active"); return false; }
            if (!runner.HasNode(next.StartNode)) { GameLog.Error("Dialog", "unknown node: " + next.StartNode); return false; }
            context.PushScope(new LogicScope(LogicUsage.DialogCommand, next.Scope.QuestIndex,
                next.Scope.QuestInstance, next.BeatIndex, next.Scope.HasBody, next.Scope.Body));
            request = next; outcome = DialogOutcome.Completed; terminal = null;
            lastPresentedNode = null;
            awaitingContinue = awaitingOption = blocked = resumePending = finished = false;
            pageUnacknowledged = stopPending = optionPending = acknowledgeNotified = false;
            nodeVisitIndex = 0; rollFrame = null; IsActive = true;
            int token = ++generation;
            if (effectHost != null)
            {
                previousContinuation = effectHost.BlockingCommandCompleted;
                effectHost.BlockingCommandCompleted = () =>
                {
                    if (!IsActive || generation != token || !blocked) return;
                    blocked = false; resumePending = true;
                };
            }
            driving = true;
            try { Started?.Invoke(request); if (!stopPending) runner.Start(request.StartNode, this); }
            catch { outcome = DialogOutcome.Interrupted; terminal = null; finished = true; pageUnacknowledged = false; throw; }
            finally { driving = false; Settle(); }
            return true;
        }

        public void Pump()
        {
            if (!IsActive || driving || pumping) return;
            pumping = true;
            try
            {
                Settle();
                if (!IsActive || finished || blocked) return;
                if (optionPending)
                {
                    optionPending = false;
                    Drive(() => { PageCleared?.Invoke(); if (!stopPending) runner.SelectOption(selectedOption); });
                }
                if (IsActive && !finished && resumePending)
                { resumePending = false; Drive(runner.Resume); }
                for (int i = 0; i < MaxLinesPerPump && IsActive && !finished && !blocked && awaitingContinue; i++)
                { awaitingContinue = false; Drive(runner.Continue); }
            }
            finally { pumping = false; }
        }
        void Drive(Action action)
        {
            driving = true;
            try { action(); }
            finally { driving = false; Settle(); }
        }
        public void SelectOption(int optionId)
        {
            if (!IsActive || !awaitingOption || finished) return;
            awaitingOption = false; selectedOption = optionId; optionPending = true;
        }
        public void AcknowledgeEnd()
        {
            if (!IsActive || !finished) return;
            pageUnacknowledged = false; Settle();
        }
        public void AbortByPlayer() => RequestStop(DialogOutcome.Aborted);
        public void Interrupt() => RequestStop(DialogOutcome.Interrupted);
        void RequestStop(DialogOutcome reason)
        {
            if (!IsActive) return;
            outcome = reason; terminal = null; pageUnacknowledged = false; stopPending = true;
            Settle();
        }
        void Settle()
        {
            if (driving || !IsActive) return;
            if (stopPending)
            {
                stopPending = false; driving = true;
                try { runner.Stop(); }
                finally { driving = false; }
                finished = true;
            }
            if (!finished) return;
            if (pageUnacknowledged && outcome != DialogOutcome.Interrupted)
            {
                if (!acknowledgeNotified)
                { acknowledgeNotified = true; WaitingForEndAcknowledge?.Invoke(); }
                return;
            }
            var result = new DialogEnded(request.BeatIndex, outcome, terminal, request.Scope);
            context.PopScope();
            if (effectHost != null) effectHost.BlockingCommandCompleted = previousContinuation;
            previousContinuation = null; IsActive = false;
            Ended?.Invoke(result);
        }
        public void OnLine(in DialogLine line)
        {
            lastPresentedNode = line.Node;
            pageUnacknowledged = true; awaitingContinue = true;
            LinePresented?.Invoke(line);
        }
        public void OnOptions(DialogOption[] options, int count)
        {
            awaitingContinue = false; awaitingOption = true; pageUnacknowledged = false;
            OptionsPresented?.Invoke(options, count);
        }
        public DialogCommandResult OnCommand(string text)
        {
            if (stopPending) return DialogCommandResult.Stop;
            if (!commands.TryGet(text, out var effect))
            { GameLog.Error("Dialog", "command not in table: " + text); outcome = DialogOutcome.Interrupted; terminal = null; return DialogCommandResult.Stop; }
            if (effect.Flow == EffectFlow.Terminal) { terminal = effect; return DialogCommandResult.Stop; }
            if (effect is AbortBeatEffect) { outcome = DialogOutcome.Aborted; return DialogCommandResult.Continue; }
            if (!effect.CanApply(context))
            {
                effectHost?.EffectRejected();
                GameLog.WarnOnce("Dialog.Rejected:" + text, "Dialog", "Rejected command: " + text);
                return DialogCommandResult.Continue;
            }
            if (effect.Flow == EffectFlow.Blocking)
            {
                blocked = true;
                int dialogToken = generation, blockToken = ++blockingGeneration;
                if (effectHost != null) effectHost.BlockingCommandCompleted = () =>
                {
                    if (!IsActive || generation != dialogToken || blockingGeneration != blockToken || !blocked) return;
                    blocked = false; resumePending = true;
                };
                effect.Apply(context); LogicEffectTrace.Applied(context); return DialogCommandResult.Block;
            }
            effect.Apply(context); LogicEffectTrace.Applied(context); return stopPending ? DialogCommandResult.Stop : DialogCommandResult.Continue;
        }
        public void OnNodeStarted(string node)
        { rollFrame = new RollFrame { Node = node, Visit = ++nodeVisitIndex, Parent = rollFrame }; }
        public void OnNodeCompleted(string node)
        { if (rollFrame != null && rollFrame.Node == node) rollFrame = rollFrame.Parent; }
        public void OnDialogComplete() { finished = true; Settle(); }

        public int Roll(int sides)
        {
            if (!IsActive || rollFrame == null) throw new InvalidOperationException("roll requires an active dialog node");
            if (sides < 1 || sides == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(sides));
            for (int i = 0; i < rollFrame.Count; i++) if (rollFrame.Values[i].sides == sides) return rollFrame.Values[i].value;
            // Beat indices and generated beat facts both use ordinal content IDs, one-based.
            int beatCount = 0, index = 0;
            var catalog = context.Facts.Catalog;
            for (int slot = 0; request.BeatIndex > 0 && slot < catalog.Count; slot++)
                if (catalog.FamilyAt(slot) == FactFamily.Beat && ++index == request.BeatIndex)
                { beatCount = context.Facts.GetBySlot(slot); break; }
            ulong salt = StableHash.Combine(StableHash.Combine((ulong)(uint)request.BeatIndex, (ulong)(uint)beatCount),
                StableHash.Combine(StableHash.Hash64(rollFrame.Node), (ulong)(uint)rollFrame.Visit));
            var rng = RngStreams.For(context.CampaignSeed, RngPurpose.Narrative, salt);
            int value = rng.NextInt(1, sides + 1);
            if (rollFrame.Count < rollFrame.Values.Length) rollFrame.Values[rollFrame.Count++] = (sides, value);
            return value;
        }
    }
}
