using System;
using System.Collections.Generic;
using Game.Data.Logic;

namespace Game.Tests.EditMode.Data.Logic
{
    /// <summary>Reusable in-memory read fixture. All writes used to arrange facts stay in this fake, never in runtime effects.</summary>
    public sealed class FakeLogicContext : ILogicContext, ILogicFacts, ILogicEconomy, ILogicNarrative, ILogicWorld
    {
        readonly FactCatalog catalog;
        readonly LogicScope[] scopes = new LogicScope[8];
        int depth;
        public readonly int[] Values;
        public readonly Dictionary<int, int> Items = new Dictionary<int, int>();
        public readonly Dictionary<int, int> Relations = new Dictionary<int, int>();
        public readonly Dictionary<(int quest, int instance, int slot, BaselineKind kind), int> Baselines = new Dictionary<(int, int, int, BaselineKind), int>();
        public LogicScope DefaultScope = new LogicScope(LogicUsage.BeatCondition, 1, 0, 0, false, default);
        public QuestStatus Status = QuestStatus.NotStarted;
        public int Stage, Visits, RollValue = 1;
        public bool ObjectiveDone;
        public string Objective = "core", VisitNode = "node_sample";
        public long Credits;
        public int FactReads, ItemReads, CatalogReads, RollCalls, LastRollSides, BaselineCalls;
        public LogicScope LastBaselineScope;
        public BaselineKind LastBaselineKind;
        public int LastBaselineSlot;
        public ILogicFacts Facts => this;
        public ILogicEconomy Economy => this;
        public ILogicNarrative Narrative => this;
        public ILogicWorld World => this;
        public LogicScope Scope => depth == 0 ? DefaultScope : scopes[depth - 1];
        public ulong CampaignSeed => 40004;
        public FakeLogicContext(FactCatalog catalog)
        {
            this.catalog = catalog; Values = new int[catalog.Count];
            for (int i = 0; i < Values.Length; i++) Values[i] = catalog.DefaultAt(i);
        }
        public void PushScope(in LogicScope scope) { if (depth == scopes.Length) throw new InvalidOperationException("Scope depth exceeds eight."); scopes[depth++] = scope; }
        public void PopScope() { if (depth == 0) throw new InvalidOperationException("No scope to pop."); depth--; }
        public FactCatalog Catalog { get { CatalogReads++; return catalog; } }
        public int GetBySlot(int slot) { FactReads++; return Values[slot]; }
        public int Get(in FactId id) { int slot = catalog.SlotOf(id); return slot < 0 ? 0 : GetBySlot(slot); }
        public void SetBySlot(int slot, int value, FactWriteSource source) => Values[slot] = value;
        public void AddBySlot(int slot, int delta, FactWriteSource source)
            => Values[slot] = (int)Math.Clamp((long)Values[slot] + delta, int.MinValue, int.MaxValue);
        public int GetItemCount(int itemIndex) { ItemReads++; return Items.TryGetValue(itemIndex, out int count) ? count : 0; }
        public long GetCredits() => Credits;
        public QuestStatus GetQuestStatus(int questIndex) => Status;
        public int GetQuestStage(int questIndex) => Status == QuestStatus.Active ? Stage : -1;
        public bool IsObjectiveDone(int questIndex, string objectiveId) => ObjectiveDone && objectiveId == Objective;
        public int GetBaseline(in LogicScope scope, int factSlot, BaselineKind kind)
        {
            BaselineCalls++; LastBaselineScope = scope; LastBaselineKind = kind; LastBaselineSlot = factSlot;
            return Baselines.TryGetValue((scope.QuestIndex, scope.QuestInstance, factSlot, kind), out int baseline) ? baseline : 0;
        }
        public int GetDialogVisitCount(string node) => node == VisitNode ? Visits : 0;
        public int Roll(int sides) { RollCalls++; LastRollSides = sides; return RollValue; }
        public int CurrentStarSystemIndex { get; set; }
        public double UniversalTime { get; set; }
        public int CurrentStarSystemOwnerFactionIndex { get; set; }
        public int GetRelation(int factionIndex) => Relations.TryGetValue(factionIndex, out int value) ? value : 0;
        public event Action<int> ItemCountChanged { add { } remove { } }
        public event Action CreditsChanged { add { } remove { } }
        static NotSupportedException Effect() => new NotSupportedException("Read-only condition fixture: effects are outside NAR-004.");
        public void GiveItems(int itemIndex, int count) => throw Effect();
        public bool TryTakeItems(int itemIndex, int count) => throw Effect();
        public void GiveCredits(long amount) => throw Effect();
        public bool TryTakeCredits(long amount) => throw Effect();
        public void GiveLoot(int lootTableIndex, int picks, ulong salt) => throw Effect();
        public bool StartQuest(int questIndex) => throw Effect();
        public bool FailQuest(int questIndex) => throw Effect();
        public bool CompleteQuest(int questIndex) => throw Effect();
        public void TrackQuest(int questIndex) => throw Effect();
        public void AddQuestLog(int questIndex, string entryId) => throw Effect();
        public bool RevealClue(int clueIndex) => throw Effect();
        public void QueueBeat(int beatIndex) => throw Effect();
        public void CompleteBeat(int beatIndex) => throw Effect();
        public void AbortCurrentBeat() => throw Effect();
        public void AdjustRelation(int factionIndex, int delta) => throw Effect();
        public void GiveShip(int blueprintIndex) => throw Effect();
        public void SpawnEncounter(int encounterIndex, string anchorTag, int count, in LogicScope scope) => throw Effect();
        public void StartCombat(int encounterIndex, in LogicScope scope) => throw Effect();
        public void RemoveBody(in LogicScope scope) => throw Effect();
        public void ReplaceBody(in LogicScope scope, int bodyTemplateIndex) => throw Effect();
        public void RequestLaneJump(int starSystemIndex) => throw Effect();
        public void OpenMerchant(int merchantIndex, in LogicScope scope, Action onClosed) => throw Effect();
        public void OpenShipEditor() => throw Effect();
        public void RepairPlayerShip(int percent) => throw Effect();
        public void DamagePlayerShip(int amount, in LogicScope scope) => throw Effect();
        public void PlaySfx(int audioCueIndex) => throw Effect();
        public void Notify(string text) => throw Effect();
    }
}
