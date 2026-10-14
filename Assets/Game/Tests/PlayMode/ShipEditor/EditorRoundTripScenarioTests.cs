using System.Collections;
using System.Collections.Generic;
using Game.App.EditorHost;
using Game.App.Session;
using Game.App.DevRuntime;
using Game.Core.Components;
using Game.Data.Services;
using Game.Galaxy.Services;
using Game.Galaxy.Components;
using Game.Galaxy.Bodies;
using Game.Narrative;
using Game.Narrative.Dialog;
using Game.ShipEditor.Services;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Game.Core;

using Game.Core.Flow;
using Game.Input;
using Game.Persistence.Services;
using Game.ShipEditor.Data;
using Game.Tests.Common;
using Game.Tests.PlayMode.UI;
using Game.UI.Services;
using Game.UI.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.ShipEditor
{
    /// <summary>EDT-023 production navigation and persisted blueprint acceptance.</summary>
    [Category("Scenario")]
    public sealed class EditorRoundTripScenarioTests : GameFlowFixture
    {
        [UnityTest]
        public IEnumerator Lab_FromMainMenu_SaveBlueprint()
        {
            var session = Service<GameSession>();
            var saves = Service<SaveService>();
            var ui = Service<UIRoot>();
            Assert.IsFalse(session.HasActiveSession);
            DialogTestContext.Activate(ui.Navigator.Get<MainMenuView>().Root.Q<Button>("main-menu-lab"));
            yield return WaitUntil(() => session.CurrentScene == GameScene.BlueprintLab && !session.IsTransitioning, 10, "Main menu opens actual Lab");
            var editor = Service<EditorHostServices>();
            var host = Object.FindAnyObjectByType<ShipEditorHost>();
            Assert.IsNotNull(host); Assert.AreSame(editor.View, ui.Navigator.Top);
            Assert.AreEqual(InputContext.Editor, Service<InputService>().CurrentContext);
            int cells = host.Session.Draft.SolidCellCount;
            Assert.Greater(cells, 0);
            editor.SaveBlueprint();
            yield return null;
            var prompt = ui.Navigator.Get<TextPromptView>();
            prompt.Root.Q<TextField>("text-prompt-field").value = "Lab scenario";
            DialogTestContext.Activate(prompt.Root.Q<Button>("text-prompt-ok"));
            var blueprints = new List<BlueprintInfo>();
            editor.Blueprints.List(blueprints);
            var saved = blueprints.FindAll(item => !item.ReadOnly);
            Assert.AreEqual(1, saved.Count);
            Assert.AreEqual("lab_scenario", saved[0].DisplayName); // SPEC-51 Q47: saved names use sanitized IDs.
            Assert.IsTrue(editor.Blueprints.TryLoad(saved[0].Id, out var restored, out var error), error);
            using (restored) Assert.AreEqual(cells, restored.SolidCellCount);
            Assert.IsEmpty(saves.ListSlots(saves.ActiveProfile), "Creative blueprint storage must not create a campaign save.");
            editor.RequestLeave();
            yield return WaitUntil(() => session.CurrentScene == GameScene.MainMenu && !session.IsTransitioning, 10, "Lab returns to MainMenu");
            Assert.IsFalse(session.HasActiveSession);
            Assert.IsInstanceOf<MainMenuView>(ui.Navigator.Top);
            Assert.AreEqual(1, ui.Navigator.Count);
            Assert.AreEqual(0, Service<IPauseService>().ActiveTokenCount);
            Assert.AreEqual(InputContext.Menu, Service<InputService>().CurrentContext);
            editor.Blueprints.List(blueprints);
            Assert.AreEqual(1, blueprints.FindAll(item => !item.ReadOnly).Count, "Stored blueprint survives session teardown.");
        }
        IEnumerator DockedWorld()
        {
            Service<EncounterDirectorService>().Enabled = false;
            var operation = Service<IGameFlow>().NewGameAsync(new NewGameArgs("Editor roundtrip", 42, false, ContentId.None));
            var awaiter = operation.GetAwaiter();
            yield return WaitUntil(() => awaiter.IsCompleted, 10, "Campaign world loads");
            Assert.AreEqual(FlowResult.Ok, awaiter.GetResult());
            // Opening-story sequencing is QST-010. Reset its state and suspend automatic stories
            // only in this editor fixture; station interaction and compiled dialog remain real.
            Service<NarrativeService>().BeginNewGame(false);
            Service<DialogService>().Interrupt();
            yield return WaitUntil(() => PlayerReady, 10, "Actual player ready for station interaction");
            yield return InteractStation();
            Assert.IsTrue(Service<DockingService>().IsDocked);
        }
        IEnumerator InteractStation()
        {
            Service<DialogService>().Interrupt();
            Service<DockingService>().Undock();
            Service<UIRoot>().Navigator.CloseAllAboveRoot();
            yield return WaitUntil(() => Service<InputService>().CurrentContext == InputContext.Flight, 2, "Navigation releases flight input");
            var host = Service<DevRuntimeHost>();
            var result = host.Commands.Execute("interact body=ark_station", host.Context);
            Assert.AreEqual(DevResultKind.Pending, result.Kind, result.Text);
            IDevOperation op = null;
            Assert.IsTrue(host.TryGetOperation(result.OperationId, out op));
            yield return WaitUntil(() => op.Status != DevOperationStatus.Running, 10, "Real station interaction completes");
            Assert.AreEqual(DevOperationStatus.Succeeded, op.Status, op.Message);
        }
        IEnumerator OpenFromStationDialog()
        {
            var dialog = Service<DialogService>();
            if (dialog.IsActive) dialog.Interrupt();
            int hangar = -1;
            void Options(DialogOption[] values, int count)
            { for (int i = 0; i < count; i++) if (values[i].Text == "Visit Hangar Bay") hangar = values[i].Id; }
            dialog.OptionsPresented += Options;
            try
            {
                yield return InteractStation();
                yield return WaitUntil(() => hangar >= 0, 5, "Compiled station dialog offers hangar");
                dialog.SelectOption(hangar);
                dialog.Pump();
                if (dialog.CanAcknowledgeEnd) dialog.AcknowledgeEnd();
                yield return WaitUntil(() => Service<GameSession>().CurrentScene == GameScene.BlueprintLab && !Service<GameSession>().IsTransitioning, 5, "Station dialog opens fleet editor");
                Assert.IsFalse(dialog.IsActive, "Terminal edit effect must run after DialogEnded");
                Assert.IsNotNull(Object.FindAnyObjectByType<ShipEditorHost>());
                Assert.AreEqual(InputContext.Editor, Service<InputService>().CurrentContext);
            }
            finally { dialog.OptionsPresented -= Options; }
        }
        IEnumerator ReturnToFlight()
        {
            Service<EditorHostServices>().RequestLeave();
            yield return WaitUntil(() => Service<GameSession>().CurrentScene == GameScene.World && !Service<GameSession>().IsTransitioning && PlayerReady, 10, "Editor returns to actual player");
            var ui = Service<UIRoot>();
            Assert.AreEqual(1, ui.Navigator.Count);
            Assert.IsInstanceOf<FlightHudView>(ui.Navigator.Top);
            Assert.AreEqual(0, Service<IPauseService>().ActiveTokenCount);
            Assert.AreEqual(InputContext.Flight, Service<InputService>().CurrentContext);
        }
        double3 Position()
        {
            using var frame = Em.CreateEntityQuery(typeof(FrameOrigin));
            return frame.GetSingleton<FrameOrigin>().OriginInStarSystem + (double3)Em.GetComponentData<LocalTransform>(PlayerRoot).Position;
        }
        void AssertPose(ulong id, double3 position, quaternion rotation)
        {
            Assert.AreEqual(id, Em.GetComponentData<PersistentId>(PlayerRoot).Value);
            Assert.LessOrEqual(math.distance(position, Position()), .01, "Universe position survives scene/rebase round trip");
            var actual = Em.GetComponentData<LocalTransform>(PlayerRoot).Rotation;
            float degrees = math.degrees(2 * math.acos(math.min(1, math.abs(math.dot(rotation.value, actual.value)))));
            Assert.LessOrEqual(degrees, .1f);
        }
        int SystemEntities()
        { using var q = Em.CreateEntityQuery(typeof(StarSystemBody)); return q.CalculateEntityCount(); }
        [UnityTest] public IEnumerator RoundTrip_PoseAndIdentityKept_R16()
        {
            yield return DockedWorld();
            ulong id = Em.GetComponentData<PersistentId>(PlayerRoot).Value;
            var position = Position(); var rotation = Em.GetComponentData<LocalTransform>(PlayerRoot).Rotation;
            yield return OpenFromStationDialog();
            var session = Object.FindAnyObjectByType<ShipEditorHost>().Session;
            var services = Service<EditorHostServices>();
            using var context = new EditorContext(services.Config, services.Content.Catalog, services.Content.BlockDefs, services.Content.Orientations, 16);
            using var records = new NativeList<BlockRecord>(Allocator.Persistent);
            new DraftReader(session.Draft, context).Enumerate(records);
            int originalCount = records.Length; bool committed = false;
            // Select an existing removable non-functional hull block. Keep real validation and
            // refund/cargo rules; undo any candidate whose removal makes the ship invalid.
            for (int i = records.Length - 1; i >= 0; i--)
            {
                if (context.Definitions.Value.Blocks[records[i].Block].RequiresInstance) continue;
                using var batch = new EditBatch();
                var record = records[i]; record.DamageOffset = -1;
                batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = record });
                if (!session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Remove, "Remove one hull block", new ApplyReport())) continue;
                if (session.Commit().Status == CommitStatus.Committed) { committed = true; break; }
                Assert.IsTrue(session.Undo());
            }
            Assert.IsTrue(committed, "Actual imported ship has a removable hull block");
            records.Clear(); new DraftReader(session.Draft, context).Enumerate(records);
            Assert.AreEqual(originalCount - 1, records.Length);
            yield return ReturnToFlight();
            AssertPose(id, position, rotation);
            records.Clear(); new DraftReader(Service<GameSession>().Fleet.Flagship.Structure, context).Enumerate(records);
            Assert.AreEqual(originalCount - 1, records.Length, "Committed fleet source retains removal after respawn");
        }
        [UnityTest] public IEnumerator RoundTrip_InteractionWorks_R19()
        {
            yield return DockedWorld(); yield return OpenFromStationDialog(); yield return ReturnToFlight();
            yield return InteractStation(); Assert.IsTrue(Service<DockingService>().IsDocked);
        }
        [UnityTest] public IEnumerator ThreeRoundTrips_NoDrift()
        {
            yield return DockedWorld();
            ulong id = Em.GetComponentData<PersistentId>(PlayerRoot).Value;
            var position = Position(); var rotation = Em.GetComponentData<LocalTransform>(PlayerRoot).Rotation;
            int entities = SystemEntities();
            for (int i = 0; i < 3; i++)
            {
                yield return OpenFromStationDialog(); yield return ReturnToFlight();
                AssertPose(id, position, rotation); Assert.AreEqual(entities, SystemEntities());
            }
        }
        [UnityTest] public IEnumerator FromDialog_DialogEndedFirst()
        {
            yield return DockedWorld();
            bool endedInWorld = false;
            var dialog = Service<DialogService>();
            void Ended(DialogEnded _) => endedInWorld = Service<GameSession>().CurrentScene == GameScene.World;
            dialog.Ended += Ended;
            try { yield return OpenFromStationDialog(); Assert.IsTrue(endedInWorld); yield return ReturnToFlight(); }
            finally { dialog.Ended -= Ended; }
        }
    }
}
