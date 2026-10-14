using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Narrative;
using Game.Presentation.Presenters;
using Game.UI.Data;
using Game.UI.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>HUD-010: three required mappings/coalescing cases through real deferred buses and notification owner.</summary>
    public sealed class HudNotificationPresenterTests
    {
        sealed class Clock : IFrameClock { public int FrameCount { get; set; } public float UnscaledTime { get; set; } }
        readonly List<Object> assets = new();
        Clock clock;
        NotificationService service;
        HudNotificationPresenter presenter;
        VisualElement layer;
        ItemDefinition item;
        ContentCatalog catalog;
        [SetUp] public void Setup()
        {
            EventHub.Reset(); var defs = new List<ContentDefinition>();
            var block = ScriptableObject.CreateInstance<BlockDefinition>(); assets.Add(block); Id(block, "block_missing"); defs.Add(block);
            var missing = ScriptableObject.CreateInstance<ItemDefinition>(); assets.Add(missing); Id(missing, "item_missing"); defs.Add(missing);
            item = ScriptableObject.CreateInstance<ItemDefinition>(); assets.Add(item); Id(item, "item_hud_fixture");
            using (var edit = new SerializedObject(item)) { edit.FindProperty("displayName").stringValue = "Hull"; edit.ApplyModifiedPropertiesWithoutUndo(); } defs.Add(item);
            var report = new ValidationReport("HUD notification fixture"); catalog = ContentCatalog.Build(defs, report); Assert.AreEqual(0, report.ErrorCount);
            var config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); assets.Add(config);
            clock = new Clock(); layer = new VisualElement(); service = new NotificationService(layer, config, clock); presenter = new HudNotificationPresenter(service, catalog); presenter.Bind();
        }
        static void Id(ContentDefinition definition, string id)
        { using var edit = new SerializedObject(definition); edit.FindProperty("id").stringValue = id; edit.ApplyModifiedPropertiesWithoutUndo(); }
        [TearDown] public void Cleanup()
        { presenter?.Dispose(); service?.Dispose(); foreach (var asset in assets) Object.DestroyImmediate(asset); assets.Clear(); EventHub.Reset(); }
        VisualElement Visible()
        {
            var column = layer.Q("toast-column"); foreach (var toast in column.Children()) if (!toast.ClassListContains(UIStyleClasses.IsHidden)) return toast;
            Assert.Fail("Expected a visible real notification."); return null;
        }
        void Expect(string text, string style)
        { Assert.AreEqual(1, service.VisibleCount); var toast = Visible(); Assert.AreEqual(text, toast.Q<Label>(toast.name + "-text").text); Assert.IsTrue(toast.ClassListContains(style)); }
        [Test] public void TwelveItems_OneToast()
        {
            for (int i = 0; i < 12; i++)
            {
                clock.UnscaledTime = i * .2f; EventBus<ItemAcquired>.Publish(new ItemAcquired(item.Id.Hash, catalog.IndexOf(item), 1, AcquireSource.Loot)); EventBus<ItemAcquired>.Drain();
            }
            Expect("+12 Hull", UIStyleClasses.ToastPositive); Assert.AreEqual(0, service.QueuedCount);
            presenter.Unbind(); EventBus<ItemAcquired>.Publish(new ItemAcquired(item.Id.Hash, catalog.IndexOf(item), 1, AcquireSource.Loot)); EventBus<ItemAcquired>.Drain(); Expect("+12 Hull", UIStyleClasses.ToastPositive);
        }
        [Test] public void TravelAndEconomyNotices_Mapped()
        {
            foreach (TravelNoticeKind kind in Enum.GetValues(typeof(TravelNoticeKind)))
            {
                service.ClearAll(); EventBus<TravelNotice>.Publish(new TravelNotice(kind, 1, "Travel message")); EventBus<TravelNotice>.Drain();
                Expect("Travel message", kind == TravelNoticeKind.DestinationReached || kind == TravelNoticeKind.RouteRestored ? UIStyleClasses.ToastInfo : UIStyleClasses.ToastWarning);
            }
            foreach (EconomyNoticeKind kind in Enum.GetValues(typeof(EconomyNoticeKind)))
            {
                service.ClearAll(); EventBus<EconomyNotice>.Publish(new EconomyNotice(kind, "Economy message")); EventBus<EconomyNotice>.Drain();
                Expect("Economy message", kind == EconomyNoticeKind.SalvageDone || kind == EconomyNoticeKind.SalvagePartial ? UIStyleClasses.ToastPositive : UIStyleClasses.ToastWarning);
            }
            service.ClearAll(); EventBus<GameSaved>.Publish(new GameSaved("p001", "manual_001", SaveReason.Manual)); EventBus<GameSaved>.Drain(); Expect("Game saved", UIStyleClasses.ToastInfo);
        }
        [Test] public void InteractionUnhandled_Info()
        {
            EventBus<InteractionUnhandled>.Publish(new InteractionUnhandled(default)); EventBus<InteractionUnhandled>.Drain(); Expect("Nothing of interest", UIStyleClasses.ToastInfo);
            presenter.Unbind(); service.ClearAll(); EventBus<InteractionUnhandled>.Publish(new InteractionUnhandled(default)); EventBus<InteractionUnhandled>.Drain(); Assert.AreEqual(0, service.VisibleCount);
        }
    }
}
