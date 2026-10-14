using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.App.Presenters;
using Game.App.Services;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Narrative;
using Game.Narrative.Beats;
using Game.Narrative.Clues;
using Game.Narrative.Dialog;
using Game.Narrative.Quests;
using Game.Narrative.ShipLog;
using Game.Presentation.Presenters;
using Game.Tests.Common;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using NUnit.Framework;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>Three HUD-014 cases. The map case becomes live automatically when QST-008/HUD-016 register real owners.</summary>
    public sealed class ShipLogViewTests
    {
        MapFlow mapFlow;
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (mapFlow != null) { yield return mapFlow.GameFlowTearDown(); mapFlow = null; }
            Services.Reset(); EventHub.Reset();
        }
        static void Field(object target, string name, object value)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (field != null) { field.SetValue(target, value); return; } }
            Assert.Fail("Fixture field missing: " + name);
        }
        sealed class Source : IDisposable
        {
            readonly List<Object> owned = new();
            internal readonly ContentCatalog Catalog;
            internal readonly QuestDefinition First, Second;
            internal readonly ClueDefinition ClueA, ClueB;
            internal readonly FactStore Facts;
            internal readonly QuestService Quests;
            internal readonly ClueService Clues;
            internal readonly BeatService Beats;
            internal readonly NarrativeService Host;
            internal readonly ShipLogQuery Query;
            internal Source()
            {
                var definitions = new List<ContentDefinition>();
                foreach (string path in new[] { "Blocks/block_missing", "Items/item_missing" })
                { var asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ContentDefinition>("Assets/Game/Content/" + path + ".asset")); owned.Add(asset); definitions.Add(asset); }
                First = Create<QuestDefinition>("quest_first"); Second = Create<QuestDefinition>("quest_second");
                Field(First, "title", "First quest"); Field(First, "description", "Investigate the recovered signal."); Field(Second, "title", "Second quest");
                var firstStage = new QuestStage { id = "investigate", title = "Investigate", objectives = new List<QuestObjective> {
                    new QuestObjective { id = "sample", text = "Gather sample", doneWhen = Condition("$story_count >= 1"), progress = Integer("$story_count"), target = 1 },
                    new QuestObjective { id = "return", text = "Return sample", doneWhen = Condition("$story_finish") } } };
                Field(First, "stages", new List<QuestStage> { firstStage });
                Field(First, "logEntries", new List<QuestLogEntry> { new QuestLogEntry { id = "signal", text = "Recovered a signal." } });
                Field(Second, "stages", new List<QuestStage> { new QuestStage { id = "wait", objectives = new List<QuestObjective> {
                    new QuestObjective { id = "wait", text = "Wait for results", doneWhen = Condition("$story_finish") } } } });
                ClueA = Create<ClueDefinition>("clue_first"); ClueB = Create<ClueDefinition>("clue_second");
                Field(ClueA, "title", "First clue"); Field(ClueA, "body", "First clue body"); Field(ClueB, "title", "Second clue"); Field(ClueB, "body", "Second clue body");
                var mystery = Create<MysteryDefinition>("mystery_signal"); Field(mystery, "title", "The signal"); Field(mystery, "solvedText", "Mystery solved.");
                Field(mystery, "requires", new List<ClueDefinition> { ClueA, ClueB }); Field(ClueA, "mystery", mystery); Field(ClueB, "mystery", mystery);
                var factAsset = Create<FactCatalogDefinition>("facts_ship_log"); Field(factAsset, "facts", new[] {
                    new FactDeclaration { Name = "story_count", Kind = FactKind.Int }, new FactDeclaration { Name = "story_finish", Kind = FactKind.Bool },
                    new FactDeclaration { Name = "journal_signal", Kind = FactKind.Int, Flags = FactFlags.Journal, JournalText = "Signals recovered" } });
                foreach (var asset in owned) if (asset is ContentDefinition definition && !definitions.Contains(definition)) definitions.Add(definition);
                var report = new ValidationReport("ship log UI fixture"); Catalog = ContentCatalog.Build(definitions, report);
                var refs = RefResolverRegistry.CreateDefault(Catalog); var factCatalog = FactCatalog.Build(Catalog, refs, report);
                var compiler = new LogicCompiler(factCatalog, refs, new LogicFunctionRegistry()); var binder = new LogicBinder(factCatalog, refs, report);
                foreach (var definition in definitions) if (definition is ILogicSourceOwner owner) { owner.CompileLogic(compiler, report.For(definition.IdString)); owner.BindLogic(binder); }
                Assert.IsFalse(report.HasErrors, report.ToText()); Facts = new FactStore(factCatalog);
                var external = new ExternalBoundary(); var adapter = new NarrativeLogicAdapter();
                var context = new LogicContext(Facts, external, adapter, external, () => 7); Host = new NarrativeService(Facts, context);
                Quests = new QuestService(Catalog, Facts, context, Host); Clues = new ClueService(Catalog, Facts, context);
                var memory = new DialogMemory(); var program = Create<DialogProgramDefinition>("dialog_ui_fixture"); var table = Create<DialogCommandTableDefinition>("dialog_table_ui_fixture");
                var dialog = new DialogService(new GraphDialogRunner(program, context, memory), new DialogCommandTable(table), context, memory);
                adapter.BindDialog(dialog); Beats = new BeatService(Catalog, Facts, context, dialog, _ => 0, _ => "", 7); adapter.BindServices(Quests, Clues, Beats);
                Query = new ShipLogQuery(Catalog, Facts, Quests, Clues, Beats);
                Host.Register(Quests); Host.Register(Clues); Host.Register(Beats); Host.BeginNewGame(); Host.SetWorldReady(true);
                Quests.Start(Catalog.IndexOf(First)); Quests.Start(Catalog.IndexOf(Second)); Quests.AddLog(Catalog.IndexOf(First), "signal");
                Clues.Reveal(Catalog.IndexOf<ClueDefinition>(ClueA)); Clues.Reveal(Catalog.IndexOf<ClueDefinition>(ClueB)); Set("journal_signal", 3); Frame();
            }
            T Create<T>(string id) where T : ContentDefinition
            {
                var asset = ScriptableObject.CreateInstance<T>(); owned.Add(asset);
                using var edit = new SerializedObject(asset); edit.FindProperty("id").stringValue = id; edit.ApplyModifiedPropertiesWithoutUndo(); return asset;
            }
            static CompiledCondition Condition(string text) { var value = new CompiledCondition(); Field(value, "source", text); return value; }
            static CompiledIntExpr Integer(string text) { var value = new CompiledIntExpr(); Field(value, "source", text); return value; }
            internal void Set(string name, int value) => Facts.SetBySlot(Facts.Catalog.SlotOf(name), value, FactWriteSource.Debugger);
            internal void Frame() { EventHub.DrainAll(); Host.Tick(); }
            public void Dispose() { Query.Dispose(); Beats.Dispose(); foreach (var asset in owned) Object.DestroyImmediate(asset); }
        }
        // Unused world/economy boundaries are intentionally inert; the fact/context/query/quest/clue/beat/dialog owners above are real.
        sealed class ExternalBoundary : ILogicWorld, ILogicEconomy
        {
            public int CurrentStarSystemIndex => 0; public int CurrentStarSystemOwnerFactionIndex => 0; public double UniversalTime => 0;
            public int GetItemCount(int index) => 0; public long GetCredits() => 0;
            public event Action<int> ItemCountChanged { add { } remove { } } public event Action CreditsChanged { add { } remove { } }
            public int GetRelation(int index) => throw new NotSupportedException(); public void AdjustRelation(int index, int delta) => throw new NotSupportedException();
            public void GiveShip(int index) => throw new NotSupportedException(); public void SpawnEncounter(int index, string tag, int count, in LogicScope scope) => throw new NotSupportedException();
            public void StartCombat(int index, in LogicScope scope) => throw new NotSupportedException(); public void RemoveBody(in LogicScope scope) => throw new NotSupportedException();
            public void ReplaceBody(in LogicScope scope, int index) => throw new NotSupportedException(); public void RequestLaneJump(int index) => throw new NotSupportedException();
            public void OpenMerchant(int index, in LogicScope scope, Action close) => throw new NotSupportedException(); public void OpenShipEditor() => throw new NotSupportedException();
            public void RepairPlayerShip(int percent) => throw new NotSupportedException(); public void DamagePlayerShip(int amount, in LogicScope scope) => throw new NotSupportedException();
            public void PlaySfx(int index) => throw new NotSupportedException(); public void Notify(string text) => throw new NotSupportedException();
            public void GiveItems(int index, int count) => throw new NotSupportedException(); public bool TryTakeItems(int index, int count) => throw new NotSupportedException();
            public void GiveCredits(long amount) => throw new NotSupportedException(); public bool TryTakeCredits(long amount) => throw new NotSupportedException();
            public void GiveLoot(int index, int picks, ulong salt) => throw new NotSupportedException();
        }
        sealed class Window : IDisposable
        {
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly ShipLogView View;
            readonly UIConfig config;
            internal Window(ShipLogViewModel model, IShipLogCommands commands, int width = 1280, int height = 720)
            {
                Panel = new UITestPanel(width, height); config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); config.PanelSettings = Panel.Settings;
                UI = new UIRoot(Panel.Document, config, new DialogTestContext.Pause(), new DialogTestContext.Context(), new NullUISoundSink(), new UnityFrameClock());
                View = new ShipLogView(model, commands); UI.Register(View); UI.Navigator.Push(View);
            }
            public void Dispose() { UI.Dispose(); Object.DestroyImmediate(config); Panel.Dispose(); }
        }
        [UnityTest] public IEnumerator SelectQuest_ShowsObjectives()
        {
            using var source = new Source(); var model = new ShipLogViewModel(); using var presenter = new ShipLogPresenter(model, source.Catalog); presenter.Bind(source.Query);
            using var window = new Window(model, presenter); yield return null;
            var list = window.View.Root.Q<ListView>("ship-log-quest-list"); list.SetSelection(1);
            Assert.AreEqual("Second quest", window.View.Root.Q<Label>("ship-log-quest-title").text); Assert.AreEqual("Wait for results", model.Objectives[0].Text);
            list.SetSelection(0); Assert.AreEqual("First quest", model.QuestTitle); Assert.AreEqual("Recovered a signal.", model.QuestLog[0]);
            source.Set("story_count", 1); source.Frame(); Assert.IsTrue(model.Objectives[0].IsDone);
            Assert.IsTrue(window.View.Root.Q("ship-log-objective-0").ClassListContains("is-done")); Assert.AreEqual("Gather sample (1/1)", model.Objectives[0].Text);
            window.View.Root.Q<TabBar>("ship-log-tabs").SelectedIndex = 1; yield return null;
            window.View.Root.Q<ListView>("ship-log-clue-list").SetSelection(1);
            Assert.IsTrue(source.Clues.IsSeen(source.Catalog.IndexOf<ClueDefinition>(source.ClueB))); Assert.IsFalse(model.Clues[1].IsNew);
            Assert.AreEqual("Second clue body", window.View.Root.Q<Label>("ship-log-clue-body").text);
            Assert.AreEqual(2, model.Mysteries[0].Known); Assert.IsTrue(model.Mysteries[0].Solved); Assert.AreEqual("Mystery solved.", model.Mysteries[0].SolvedText);
            Assert.AreEqual("Signals recovered ×3", model.Journal[0].Text); Assert.AreEqual(3, model.Journal[0].Value);
            window.UI.Navigator.Pop(window.View); source.Set("story_count", 0); source.Frame(); window.UI.Navigator.Push(window.View); yield return null;
            Assert.IsTrue(model.Objectives[0].IsDone); Assert.IsTrue(source.Quests.IsObjectiveDone(source.Catalog.IndexOf(source.First), "sample"), "Latched completion remains owner-driven after reopen.");
        }
        sealed class MapFlow : GameFlowFixture
        {
            internal IEnumerator Begin()
            {
                var request = Service<IGameFlow>().NewGameAsync(new NewGameArgs("Ship log map", 42, false, ContentId.None)).GetAwaiter();
                yield return WaitUntil(() => request.IsCompleted, 30, "Ship log map session"); Assert.AreEqual(FlowResult.Ok, request.GetResult());
                if (Service<DialogService>().IsActive) Service<DialogService>().Interrupt();
                yield return WaitUntil(() => PlayerRoot != Entity.Null && Em.HasComponent<LocalTransform>(PlayerRoot), 30, "Ship log map player");
            }
            internal PauseToken Hold() => HoldSimulation("ship log map acceptance");
        }
        [UnityTest] public IEnumerator RumorShowOnMap_OpensGalaxyMapSelected()
        {
            mapFlow = new MapFlow(); yield return mapFlow.GameFlowSetUp();
            if (!Services.TryGet<ShipLogQuery>(out _) || !Services.TryGet<ClueService>(out _))
                Assert.Ignore("Pending QST-008 canonical runtime registration; this case automatically runs when that owner is installed.");
            var ui = Services.Get<UIRoot>(); GalaxyMapView map = null;
            try { map = ui.Navigator.Get<GalaxyMapView>(); }
            catch (InvalidOperationException) { }
            if (map == null) Assert.Ignore("Pending HUD-016 real registered galaxy map; no substitute map is used.");
            yield return mapFlow.Begin(); using var hold = mapFlow.Hold();
            var dialog = Services.Get<DialogService>(); if (dialog.IsActive) dialog.Interrupt();
            var view = ui.Navigator.Get<ShipLogView>(); ui.Navigator.Push(view);
            var content = Services.Get<ContentService>().Catalog; var galaxy = Services.Get<GalaxyService>(); RumorDefinition candidate = null; StarSystemId target = default;
            foreach (var clue in content.All<ClueDefinition>())
                if (clue is RumorDefinition rumor && rumor.Target != null && galaxy.TryResolveStarSystem(rumor.Target.Id, out var id) && !id.Equals(galaxy.State.CurrentStarSystem))
                { candidate = rumor; target = id; break; }
            Assert.IsNotNull(candidate, "Real campaign content must supply a rumor with an authored target.");
            var clues = Services.Get<ClueService>(); clues.Reveal(content.IndexOf<ClueDefinition>(candidate));
            var screens = Services.Get<UiComposition>(); int row = -1;
            for (int frame = 0; frame < 3 && row < 0; frame++)
            { yield return null; row = screens.ShipLogModel.Rumors.FindIndex(item => item.RumorId == candidate.IdString); }
            Assert.GreaterOrEqual(row, 0); Assert.IsTrue(screens.ShipLogModel.Rumors[row].HasMapTarget);
            view.Root.Q<TabBar>("ship-log-tabs").SelectedIndex = 2; view.Root.Q<ListView>("ship-log-rumor-list").ScrollToItem(row); yield return null;
            var button = view.Root.Q<Button>("ship-log-rumor-map-" + row); Assert.IsNotNull(button); DialogTestContext.Activate(button); yield return null;
            Assert.AreSame(map, ui.Navigator.Top); var model = screens.GalaxyMapModel; Assert.GreaterOrEqual(model.SelectedIndex, 0);
            Assert.AreEqual(galaxy.GetStarSystemName(target), model.SelectedName);
            Assert.AreEqual(galaxy.Galaxy.Get(target).PositionLy, model.Data.Points[model.SelectedIndex].Pos);
        }
        sealed class LayoutCommands : IShipLogCommands
        {
            readonly ShipLogViewModel model; internal LayoutCommands(ShipLogViewModel model) => this.model = model;
            public void SelectTab(int tab) => model.SelectedTab = tab; public void SetShowCompleted(bool on) => model.ShowCompleted = on;
            public void SelectQuest(int index) => model.SelectedQuest = index; public void ToggleTrack(int index) => model.Quests[index].IsTracked = !model.Quests[index].IsTracked;
            public void SelectClue(int index) => model.SelectedClue = index; public void ShowRumorOnMap(int index) => throw new NotSupportedException("Layout fixture has no map owner.");
        }
        [UnityTest] public IEnumerator Layout_ManyItems_AllLogicalSizes_NoIssues()
        {
            var model = new ShipLogViewModel { QuestTitle = new string('W', 40), QuestStageText = "A long active stage title", QuestDescription = "A long narrative description that wraps inside its own scroll area.", ClueBody = "A recovered clue with a long body. The investigation continues across several star systems, and the complete record remains readable in this scrolling panel." };
            for (int i = 0; i < 64; i++) { model.Quests.Add(new QuestRowViewModel { QuestId = "quest_" + i, Title = new string('W', 40), StageText = "A long stage", IsTracked = i == 0 }); model.Rumors.Add(new RumorRowViewModel { RumorId = "rumor_" + i, Title = new string('W', 40), LocationText = new string('W', 40), HasMapTarget = true }); }
            for (int i = 0; i < 256; i++) model.Clues.Add(new ClueRowViewModel { ClueId = "clue_" + i, Title = new string('W', 40), MysteryTitle = "Mystery " + i / 16, IsNew = true });
            for (int i = 0; i < 16; i++) { model.Objectives.Add(new ObjectiveRowViewModel { Text = "An objective with progress (9,999/9,999)", IsDone = i % 2 == 0 }); model.Mysteries.Add(new MysteryRowViewModel { Title = "A mystery with a long title", Known = 9999, Required = 9999, Solved = true, SolvedText = "The solved mystery text wraps across multiple lines in its virtualized row." }); }
            for (int i = 0; i < 64; i++) { model.Journal.Add(new JournalRowViewModel { Text = "A journal discovery with a long narrative record ×9999", Value = 9999 }); model.QuestLog.Add("A recorded event in the quest log that wraps without moving the header or tabs."); }
            var issues = new List<LayoutIssue>(); var commands = new LayoutCommands(model);
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var window = new Window(model, commands, size.x, size.y);
                if (size.x == 1280) window.UI.ApplyAccessibility(new AccessibilityOptions { UiScale = 1, TextScale = TextScale.Percent130 });
                for (int tab = 0; tab < 3; tab++)
                {
                    commands.SelectTab(tab); yield return null; UILayoutAudit.Run(window.View.Root, window.Panel.Viewport, issues); Assert.IsEmpty(issues, size + " tab " + tab);
                    if (tab > 1) continue;
                    var list = window.View.Root.Q<ListView>(tab == 0 ? "ship-log-quest-list" : "ship-log-clue-list");
                    Assert.Less(list.Query<VisualElement>(className: "ship-log-row").ToList().Count, tab == 0 ? 64 : 256, "Rows must be virtualized.");
                    list.ScrollToItem(tab == 0 ? 63 : 255); yield return null; UILayoutAudit.Run(window.View.Root, window.Panel.Viewport, issues); Assert.IsEmpty(issues, size + " recycled tab " + tab);
                }
            }
        }
    }
}
