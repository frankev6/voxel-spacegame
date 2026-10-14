using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>HUD-007: five required view-boundary cases; no fabricated production gameplay source.</summary>
    public sealed class FlightHudViewTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); }
        sealed class RadarData : IHudRadarSource
        {
            internal readonly HudRadarBlipView[] Data = new HudRadarBlipView[64];
            internal int Count;
            public ReadOnlySpan<HudRadarBlipView> Blips => Data.AsSpan(0, Count);
        }
        sealed class Window : IDisposable
        {
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly HudViewModel Model;
            internal readonly RadarData Radar = new();
            internal readonly FlightHudView View;
            internal readonly Sprite[] Icons = new Sprite[2];
            readonly Texture2D iconTexture;
            readonly UIConfig config;
            internal Window(HudViewModel model, int width = 1280, int height = 720)
            {
                Model = model; Panel = new UITestPanel(width, height); config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); config.PanelSettings = Panel.Settings;
                iconTexture = new Texture2D(2, 2);
                for (int i = 0; i < Icons.Length; i++) Icons[i] = Sprite.Create(iconTexture, new Rect(0, 0, 2, 2), Vector2.zero);
                UI = new UIRoot(Panel.Document, config, new DialogTestContext.Pause(), new DialogTestContext.Context(), new NullUISoundSink(), new UnityFrameClock());
                View = new FlightHudView(Model, Radar, path => path == "Flight/FirePrimary" ? "LMB" : path == "Flight/FireSecondary" ? "RMB" : path == "Flight/FireTertiary" ? "Space" : "F");
                UI.Register(View); UI.Navigator.SetRoot(View);
            }
            public void Dispose() { UI.Dispose(); foreach (var icon in Icons) Object.DestroyImmediate(icon); Object.DestroyImmediate(iconTexture); Object.DestroyImmediate(config); Panel.Dispose(); }
        }
        static HudViewModel Maximum()
        {
            string name = new string('W', 40);
            var model = new HudViewModel { HasShip = true, HasTarget = true, HasInteraction = true, HasObjective = true, HasRoute = true,
                HullPercent = 9999, HullFraction = 1, ShieldValue = 9999, ShieldMax = 9999, ShieldFraction = 1, ShieldStatus = 1,
                EnergyValue = 9999, EnergyMax = 9999, EnergyFraction = 1, SpeedMps = 9999, Boosting = true,
                TargetName = name, TargetRelation = 2, TargetHullFraction = 1, TargetShieldFraction = 1, TargetDistanceM = 9999,
                InteractionVerbText = "Inspect", InteractionTargetName = name, InteractionKeyText = "F", InteractionInRange = true,
                InteractionDetailText = name, InteractionBlocked = true, StarSystemName = name, ZoneName = name,
                ObjectiveTitle = name, ObjectiveText = "Find the beacon and inspect the station.", RouteText = name, RouteBlocked = true, RadarRangeM = 9999 };
            for (int i = 0; i < model.WeaponGroups.Count; i++)
            { var group = model.WeaponGroups[i]; group.GroupNumber = i + 1; group.WeaponCount = 9999; group.ReadyFraction = 1; group.Firing = true; }
            for (int i = 0; i < model.Abilities.Count; i++)
            { var ability = model.Abilities[i]; ability.HasAbility = true; ability.Name = name; ability.Charges = 9999; ability.Ready = true; ability.Active = true; ability.KeyText = "4"; }
            return model;
        }
        [UnityTest] public IEnumerator Layout_MaxContent_AllLogicalSizes_NoOverlap()
        {
            var issues = new List<LayoutIssue>();
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var w = new Window(Maximum(), size.x, size.y); yield return null;
                var regions = w.View.Root.Query(className: UIStyleClasses.HudRegion).ToList(); Assert.AreEqual(7, regions.Count);
                for (int i = 0; i < regions.Count; i++) for (int j = i + 1; j < regions.Count; j++)
                    Assert.IsFalse(regions[i].worldBound.Overlaps(regions[j].worldBound), size + " " + regions[i].name + "/" + regions[j].name);
                UILayoutAudit.Run(w.View.Root, w.Panel.Viewport, issues); Assert.IsEmpty(issues, size + ": " + string.Join(", ", issues.ConvertAll(issue => issue.Kind + " " + issue.ElementPath)));
                Assert.AreEqual(size.x < 1700, w.View.Root.ClassListContains("hud-narrow"));
                Assert.AreEqual(new string('W', 40), w.View.Root.Q<Label>("hud-star-system").text, "Rare text uses the real binding pipeline.");
                Assert.AreEqual("9999", w.View.Root.Q<Label>("hud-speed").text);
            }
        }
        [UnityTest] public IEnumerator Contrast_AllHudText_Passes()
        {
            using var w = new Window(Maximum()); yield return null; var issues = new List<ContrastIssue>();
            ContrastAudit.Run(w.View.Root, issues); Assert.IsEmpty(issues, string.Join(", ", issues.ConvertAll(issue => issue.ElementPath + " " + issue.Ratio)));
        }
        static void Change(HudViewModel model, int i, Sprite first, Sprite second)
        {
            bool even = (i & 1) == 0; int number = even ? 100 : 200; float fraction = even ? .5f : .75f;
            model.HasShip = even; model.HasTarget = even; model.HasInteraction = even; model.HasObjective = even; model.HasRoute = even;
            model.IsHulk = !even; model.HullPercent = number; model.HullFraction = fraction; model.ShieldValue = number; model.ShieldMax = number + 1; model.ShieldFraction = fraction; model.ShieldStatus = even ? 1 : 2;
            model.EnergyValue = number; model.EnergyMax = number + 1; model.EnergyFraction = fraction; model.SpeedMps = number; model.Boosting = even;
            model.TargetName = even ? "A" : "B"; model.TargetRelation = even ? 1 : 2; model.TargetHullFraction = fraction; model.TargetShieldFraction = fraction; model.TargetDistanceM = number;
            model.InteractionVerbText = even ? "Dock" : "Inspect"; model.InteractionTargetName = even ? "A" : "B"; model.InteractionKeyText = even ? "F" : "E";
            model.InteractionInRange = even; model.InteractionDetailText = even ? "A" : "B"; model.InteractionBlocked = even;
            model.StarSystemName = even ? "A" : "B"; model.ZoneName = even ? "A" : "B"; model.ObjectiveTitle = even ? "A" : "B"; model.ObjectiveText = even ? "A" : "B";
            model.RouteText = even ? "A" : "B"; model.RouteBlocked = even; model.RadarRangeM = number; model.RadarYawDeg = number; model.RadarVersion = i;
            for (int j = 0; j < model.WeaponGroups.Count; j++)
            { var group = model.WeaponGroups[j]; group.GroupNumber = j + (even ? 1 : 2); group.WeaponCount = number; group.ReadyFraction = fraction; group.Firing = even; group.KeyText = even ? "1" : "2"; }
            for (int j = 0; j < model.Abilities.Count; j++)
            { var ability = model.Abilities[j]; ability.HasAbility = even; ability.Icon = even ? first : second; ability.Name = even ? "A" : "B"; ability.Charges = number; ability.CooldownFraction = fraction; ability.Ready = even; ability.Active = even; ability.KeyText = even ? "1" : "2"; }
            model.NotifyWeaponGroups(); model.NotifyAbilities();
        }
        [Test] public void DirectUpdate_600Changes_ZeroAlloc()
        {
            using var w = new Window(Maximum()); w.Radar.Count = 64;
            // Warm both class/value paths and the immutable number cache; no frames or gameplay are advanced.
            Change(w.Model, 0, w.Icons[0], w.Icons[1]); Change(w.Model, 1, w.Icons[0], w.Icons[1]); long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 2; i < 602; i++) Change(w.Model, i, w.Icons[0], w.Icons[1]);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual("200", w.View.Root.Q<Label>("hud-speed").text);
            if (allocated != 0) GameLog.Warn("HUD", "Advisory direct-update allocation: " + allocated + " B for 600 changes (ADR-0042).");
        }
        [Test] public void NoShip_ShipZonesHidden()
        {
            using var w = new Window(Maximum()); w.Model.HasShip = false;
            foreach (string zone in new[] { "status", "weapons", "radar", "target", "prompt" }) Assert.IsTrue(w.View.Root.Q("hud-region-" + zone).ClassListContains(UIStyleClasses.IsHidden));
            Assert.IsFalse(w.View.Root.Q("hud-region-location").ClassListContains(UIStyleClasses.IsHidden)); Assert.IsFalse(w.View.Root.Q("hud-region-objective").ClassListContains(UIStyleClasses.IsHidden));
            w.Model.HasShip = true; Assert.IsFalse(w.View.Root.Q("hud-region-status").ClassListContains(UIStyleClasses.IsHidden));
            float saved = w.View.Root.Q<Meter>("hud-hull-meter").Fraction; w.Model.IsHulk = true; w.Model.HullFraction = .1f;
            Assert.AreEqual(saved, w.View.Root.Q<Meter>("hud-hull-meter").Fraction, "Hulk status meters keep the last values.");
        }
        static HashSet<VisualElement> Elements(VisualElement root)
        {
            var values = new HashSet<VisualElement>(); Visit(root); return values;
            void Visit(VisualElement node) { values.Add(node); for (int i = 0; i < node.hierarchy.childCount; i++) Visit(node.hierarchy[i]); }
        }
        [Test] public void Radar_64Blips_PooledNoNewElements()
        {
            using var w = new Window(Maximum()); var original = Elements(w.View.Root); w.Radar.Count = 64;
            for (int i = 0; i < 64; i++) w.Radar.Data[i] = new HudRadarBlipView { OffsetXZ = new Vector2(0, 100), Kind = (byte)(i % 4), Relation = (byte)(i % 3), Flags = i == 63 ? (byte)6 : (byte)0 };
            w.Model.RadarRangeM = 100; w.Model.RadarVersion++;
            Assert.AreEqual(64, w.View.Root.Query(className: "hud-blip").ToList().Count);
            var blip = w.View.Root.Q("hud-radar-blip-0"); Assert.AreEqual(-120, blip.style.translate.value.y.value, .001f);
            w.Model.RadarYawDeg = 90; Assert.AreEqual(120, blip.style.translate.value.x.value, .001f);
            Assert.IsTrue(w.View.Root.Q("hud-radar-blip-63").ClassListContains("hud-blip--beyond"));
            Assert.IsTrue(w.View.Root.Q("hud-radar-blip-63").ClassListContains("hud-blip--waypoint"));
            w.Radar.Data[0].OffsetXZ = new Vector2(1000, 0); w.Model.RadarVersion++; Assert.IsTrue(blip.ClassListContains("hud-blip--beyond"));
            w.Radar.Count = 1; w.Model.RadarVersion++; Assert.IsTrue(w.View.Root.Q("hud-radar-blip-1").ClassListContains(UIStyleClasses.IsHidden));
            for (int i = 1; i <= 5; i++) Assert.IsNotNull(w.View.Root.Q<Slot>("hud-weapon-group-" + i));
            for (int i = 1; i <= 4; i++) Assert.IsNotNull(w.View.Root.Q<Slot>("hud-ability-" + i));
            Assert.IsTrue(original.SetEquals(Elements(w.View.Root))); w.UI.Dispose(); w.Model.SpeedMps = 77;
            Assert.AreNotEqual("77", w.View.Root.Q<Label>("hud-speed").text, "Root detach releases direct handlers.");
        }
    }
}
