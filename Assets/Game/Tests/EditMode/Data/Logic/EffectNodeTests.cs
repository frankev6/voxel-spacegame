using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Tests.Common;
using NUnit.Framework;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data.Logic
{
    [Category("Narrative")]
    public sealed class EffectNodeTests : GameTestBase
    {
        [Test] public void List_IsAtomic_SummedCosts()
        {
            using var f = new Fixture(); f.Context.Items[f.Index(RefKind.Item, "resource_fuel")] = 3;
            var list = f.List("set_fact story_count 9", "take_item resource_fuel 2", "take_item resource_fuel 2");
            Assert.IsFalse(list.ApplyAll(f.Context, new EffectRequirements()));
            Assert.AreEqual(3, f.Context.GetItemCount(f.Index(RefKind.Item, "resource_fuel")));
            Assert.AreEqual(0, f.Context.Values[f.Facts.SlotOf("story_count")]); Assert.IsEmpty(f.Context.Calls);
            Assert.AreEqual(1, f.Context.Rejections);
        }
        [Test] public void List_RejectsWhenAnyCannotApply()
        {
            using var f = new Fixture(); f.Context.Credits = 40;
            var list = f.List("give_item resource_fuel 1", "take_credits 50");
            Assert.IsFalse(list.ApplyAll(f.Context, new EffectRequirements()));
            Assert.AreEqual(40, f.Context.Credits); Assert.AreEqual(0, f.Context.GetItemCount(f.Index(RefKind.Item, "resource_fuel")));
            Assert.IsEmpty(f.Context.Calls); Assert.AreEqual(1, f.Context.Rejections);
        }
        [Test] public void EachEffect_RoutesToContext()
        {
            using var f = new Fixture();
            foreach (var row in f.Rows())
            {
                f.Context.Calls.Clear(); f.Context.Credits = 100; f.Context.Items[f.Index(RefKind.Item, "resource_fuel")] = 10;
                var effect = f.Effect(row.command); Assert.IsTrue(effect.CanApply(f.Context), row.command); effect.Apply(f.Context);
                CollectionAssert.AreEqual(new[] { row.call }, f.Context.Calls, row.command);
                if (row.command.StartsWith("open_merchant", StringComparison.Ordinal))
                { Assert.AreEqual(0, f.Context.Resumed); f.Context.LastContinuation(); Assert.AreEqual(1, f.Context.Resumed); }
                if (row.command.StartsWith("spawn_encounter", StringComparison.Ordinal) || row.command.StartsWith("start_combat", StringComparison.Ordinal) ||
                    row.command.StartsWith("remove_body", StringComparison.Ordinal) || row.command.StartsWith("replace_body", StringComparison.Ordinal) ||
                    row.command.StartsWith("damage_ship", StringComparison.Ordinal) || row.command.StartsWith("open_merchant", StringComparison.Ordinal))
                    Assert.AreEqual(f.Context.Scope, f.Context.LastScope, row.command);
            }
        }
        [Test] public void FlowFlags_MatchTable()
        {
            using var f = new Fixture(); int count = 0;
            foreach (var row in f.Rows())
            {
                string name = row.command.Split(' ')[0];
                var expected = name == "open_merchant" ? EffectFlow.Blocking :
                    name == "start_combat" || name == "request_lane_jump" || name == "open_ship_editor" ? EffectFlow.Terminal : EffectFlow.Immediate;
                Assert.AreEqual(expected, f.Effect(row.command).Flow, name); count++;
            }
            Assert.AreEqual(29, count);
        }
        [Test] public void GiveLoot_SaltIsDeterministic()
        {
            using var f = new Fixture(); int slot = f.Facts.SlotOf("sys_beats_completed"); f.Context.Values[slot] = 7;
            var effect = f.Effect("give_loot loot_sample 2"); effect.Apply(f.Context);
            ulong expected = StableHash.Combine(((ulong)(uint)f.Context.Scope.BeatIndex << 32) | (uint)f.Context.Scope.QuestIndex, 7);
            Assert.AreEqual(expected, f.Context.LootSalt); effect.Apply(f.Context); Assert.AreEqual(expected, f.Context.LootSalt);
        }
        sealed class Fixture : IDisposable
        {
            internal readonly FactCatalog Facts;
            readonly RefResolverRegistry refs;
            readonly LogicCompiler compiler;
            internal readonly RecordingContext Context;
            internal Fixture()
            {
                var defs = FactCatalogTests.BaseContent(); defs.Add(Item("resource_fuel"));
                defs.Add(Create<QuestDefinition>("quest_sample")); defs.Add(Create<ClueDefinition>("clue_sample"));
                defs.Add(Create<StoryBeatDefinition>("beat_sample")); defs.Add(Create<ShipBlueprintDefinition>("ship_sample"));
                defs.Add(Create<LootTableDefinition>("loot_sample")); defs.Add(Create<EncounterDefinition>("encounter_sample"));
                defs.Add(Create<BodyTemplateDefinition>("body_sample")); defs.Add(Create<AuthoredStarSystemDefinition>("starsys_sample"));
                defs.Add(Create<MerchantDefinition>("merchant_sample")); defs.Add(Create<AudioCueDefinition>("cue_sample"));
                defs.Add(Create<FactionDefinition>("faction_sample"));
                defs.Add(FactCatalogTests.Facts("facts_effects", new FactDeclaration { Name = "story_count", Kind = FactKind.Int }));
                var content = FactCatalogTests.Content(defs); refs = RefResolverRegistry.CreateDefault(content);
                var report = new ValidationReport("effect fixture"); Facts = FactCatalog.Build(content, refs, report);
                Assert.IsFalse(report.HasErrors, report.ToText()); compiler = new LogicCompiler(Facts, refs, new LogicFunctionRegistry());
                Context = new RecordingContext(Facts) { CurrentStarSystemOwnerFactionIndex = Index(RefKind.Faction, "faction_sample"),
                    DefaultScope = new LogicScope(LogicUsage.DialogEffect, 3, 0, 5, true, new LogicBodyHandle(42, 1, 2, 0, default)) };
            }
            internal int Index(RefKind kind, string id)
            { Assert.IsTrue(refs.TryGet(kind, out var resolver)); Assert.IsTrue(resolver.TryResolve(id, out int value), id); return value; }
            internal IEffect Effect(string source)
            {
                var report = new ValidationReport(source);
                Assert.IsTrue(compiler.TryCompileCommand(source, new LogicCompileOptions(LogicUsage.DialogEffect, "test:effects", "command"), report, out var effect), report.ToText());
                effect.Validate(report); effect.Bind(new LogicBinder(Facts, refs, report)); Assert.IsFalse(report.HasErrors, report.ToText()); return effect;
            }
            internal CompiledEffectList List(params string[] source)
            {
                var list = LogicTestEnvironment.Source(new CompiledEffectList(), source); var report = new ValidationReport("list");
                Assert.IsTrue(compiler.Compile(list, new LogicCompileOptions(LogicUsage.QuestEffect, "test:effects", "list"), report), report.ToText());
                list.Bind(new LogicBinder(Facts, refs, report)); Assert.IsFalse(report.HasErrors, report.ToText()); return list;
            }
            internal IEnumerable<(string command, string call)> Rows()
            {
                int item = Index(RefKind.Item, "resource_fuel"), quest = Index(RefKind.Quest, "quest_sample"), beat = Index(RefKind.Beat, "beat_sample");
                yield return ("set_fact story_count 2", $"set:{Facts.SlotOf("story_count")}:2");
                yield return ("add_fact story_count 3", $"add:{Facts.SlotOf("story_count")}:3");
                yield return ("give_item resource_fuel 2", $"give_item:{item}:2");
                yield return ("take_item resource_fuel 2", $"take_item:{item}:2");
                yield return ("give_credits 2", "give_credits:2"); yield return ("take_credits 2", "take_credits:2");
                yield return ("give_loot loot_sample 2", $"give_loot:{Index(RefKind.LootTable, "loot_sample")}:2");
                yield return ("give_ship ship_sample", $"give_ship:{Index(RefKind.Blueprint, "ship_sample")}");
                foreach (string action in new[] { "start_quest", "fail_quest", "complete_quest", "track_quest" }) yield return (action + " quest_sample", action + ":" + quest);
                yield return ("quest_log quest_sample entry", $"quest_log:{quest}:entry");
                yield return ("reveal_clue clue_sample", $"reveal_clue:{Index(RefKind.Clue, "clue_sample")}");
                yield return ("queue_beat beat_sample", $"queue_beat:{beat}"); yield return ("complete_beat beat_sample", $"complete_beat:{beat}");
                yield return ("abort_beat", "abort_beat");
                yield return ("spawn_encounter encounter_sample player 2", $"spawn_encounter:{Index(RefKind.Encounter, "encounter_sample")}:player:2");
                yield return ("start_combat encounter_sample", $"start_combat:{Index(RefKind.Encounter, "encounter_sample")}");
                yield return ("remove_body", "remove_body"); yield return ("replace_body body_sample", $"replace_body:{Index(RefKind.Entity, "body_sample")}");
                yield return ("request_lane_jump starsys_sample", $"request_lane_jump:{Index(RefKind.StarSystem, "starsys_sample")}");
                yield return ("open_merchant merchant_sample", $"open_merchant:{Index(RefKind.Merchant, "merchant_sample")}");
                yield return ("open_ship_editor", "open_ship_editor"); yield return ("repair_ship 50", "repair_ship:50"); yield return ("damage_ship 10", "damage_ship:10");
                yield return ("adjust_relation owner 3", $"adjust_relation:{Context.CurrentStarSystemOwnerFactionIndex}:3");
                yield return ("play_sfx cue_sample", $"play_sfx:{Index(RefKind.AudioCue, "cue_sample")}"); yield return ("notify \"hello\"", "notify:hello");
            }
            public void Dispose() => DestroyAll();
        }
    sealed class RecordingContext : ILogicContext, ILogicFacts, ILogicEconomy, ILogicNarrative, ILogicWorld, ILogicEffectHost
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
        public RecordingContext(FactCatalog catalog)
        {
            this.catalog = catalog; Values = new int[catalog.Count];
            for (int i = 0; i < Values.Length; i++) Values[i] = catalog.DefaultAt(i);
        }
        public void PushScope(in LogicScope scope) { if (depth == scopes.Length) throw new InvalidOperationException("Scope depth exceeds eight."); scopes[depth++] = scope; }
        public void PopScope() { if (depth == 0) throw new InvalidOperationException("No scope to pop."); depth--; }
        public FactCatalog Catalog { get { CatalogReads++; return catalog; } }
        public int GetBySlot(int slot) { FactReads++; return Values[slot]; }
        public int Get(in FactId id) { int slot = catalog.SlotOf(id); return slot < 0 ? 0 : GetBySlot(slot); }
        public void SetBySlot(int slot, int value, FactWriteSource source) { Values[slot] = value; Calls.Add($"set:{slot}:{value}"); }
        public void AddBySlot(int slot, int delta, FactWriteSource source)
        { Values[slot] = (int)Math.Clamp((long)Values[slot] + delta, int.MinValue, int.MaxValue); Calls.Add($"add:{slot}:{delta}"); }
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
        public readonly List<string> Calls = new();
        public int Rejections, Resumed;
        public ulong LootSalt;
        public LogicScope LastScope;
        public Action LastContinuation;
        public Action BlockingCommandCompleted => Resume;
        void Resume() => Resumed++;
        public void EffectRejected() => Rejections++;
        public void GiveItems(int itemIndex, int count) { Items[itemIndex] = GetItemCount(itemIndex) + count; Calls.Add($"give_item:{itemIndex}:{count}"); }
        public bool TryTakeItems(int itemIndex, int count) { if (GetItemCount(itemIndex) < count) return false; Items[itemIndex] -= count; Calls.Add($"take_item:{itemIndex}:{count}"); return true; }
        public void GiveCredits(long amount) { Credits += amount; Calls.Add($"give_credits:{amount}"); }
        public bool TryTakeCredits(long amount) { if (Credits < amount) return false; Credits -= amount; Calls.Add($"take_credits:{amount}"); return true; }
        public void GiveLoot(int lootTableIndex, int picks, ulong salt) { LootSalt = salt; Calls.Add($"give_loot:{lootTableIndex}:{picks}"); }
        public bool StartQuest(int questIndex) { Calls.Add($"start_quest:{questIndex}"); return true; }
        public bool FailQuest(int questIndex) { Calls.Add($"fail_quest:{questIndex}"); return true; }
        public bool CompleteQuest(int questIndex) { Calls.Add($"complete_quest:{questIndex}"); return true; }
        public void TrackQuest(int questIndex) { Calls.Add($"track_quest:{questIndex}"); }
        public void AddQuestLog(int questIndex, string entryId) { Calls.Add($"quest_log:{questIndex}:{entryId}"); }
        public bool RevealClue(int clueIndex) { Calls.Add($"reveal_clue:{clueIndex}"); return true; }
        public void QueueBeat(int beatIndex) { Calls.Add($"queue_beat:{beatIndex}"); }
        public void CompleteBeat(int beatIndex) { Calls.Add($"complete_beat:{beatIndex}"); }
        public void AbortCurrentBeat() { Calls.Add("abort_beat"); }
        public void AdjustRelation(int factionIndex, int delta) { Calls.Add($"adjust_relation:{factionIndex}:{delta}"); }
        public void GiveShip(int blueprintIndex) { Calls.Add($"give_ship:{blueprintIndex}"); }
        public void SpawnEncounter(int encounterIndex, string anchorTag, int count, in LogicScope scope) { LastScope = scope; Calls.Add($"spawn_encounter:{encounterIndex}:{anchorTag}:{count}"); }
        public void StartCombat(int encounterIndex, in LogicScope scope) { LastScope = scope; Calls.Add($"start_combat:{encounterIndex}"); }
        public void RemoveBody(in LogicScope scope) { LastScope = scope; Calls.Add("remove_body"); }
        public void ReplaceBody(in LogicScope scope, int bodyTemplateIndex) { LastScope = scope; Calls.Add($"replace_body:{bodyTemplateIndex}"); }
        public void RequestLaneJump(int starSystemIndex) { Calls.Add($"request_lane_jump:{starSystemIndex}"); }
        public void OpenMerchant(int merchantIndex, in LogicScope scope, Action onClosed) { LastScope = scope; LastContinuation = onClosed; Calls.Add($"open_merchant:{merchantIndex}"); }
        public void OpenShipEditor() { Calls.Add("open_ship_editor"); }
        public void RepairPlayerShip(int percent) { Calls.Add($"repair_ship:{percent}"); }
        public void DamagePlayerShip(int amount, in LogicScope scope) { LastScope = scope; Calls.Add($"damage_ship:{amount}"); }
        public void PlaySfx(int audioCueIndex) { Calls.Add($"play_sfx:{audioCueIndex}"); }
        public void Notify(string text) { Calls.Add($"notify:{text}"); }
    }
    }
}
