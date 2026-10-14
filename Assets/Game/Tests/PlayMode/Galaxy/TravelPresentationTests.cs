using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Galaxy.Travel;
using Game.Presentation.Components;
using Game.Presentation.World;
using Game.UI.Data;
using Game.UI.Services;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.Galaxy
{
    public sealed class TravelPresentationTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); GameLog.ResetOnceKeys(); }
        [Test] public void Cover_FollowsPhase()
        {
            using var f = new TravelPresentationFixture(); var cover = f.System.Cover; var hud = f.UI.GetLayer(UILayer.Hud);
            Assert.AreEqual("travel-cover", cover.name); Assert.AreEqual(PickingMode.Ignore, cover.pickingMode);
            Assert.IsTrue(cover.ClassListContains("travel-cover")); Assert.IsFalse(cover.ClassListContains("travel-cover--covered"));
            hud.Add(new VisualElement()); f.System.Update(); Assert.AreSame(cover, hud[hud.childCount - 1]);
            f.Start(); f.System.Update(); Assert.AreEqual(TravelPhase.Spooling, f.Travel.Phase); Assert.IsTrue(cover.ClassListContains("travel-cover--covered"));
            f.Tick(30); Assert.AreEqual(TravelPhase.Transit, f.Travel.Phase); Assert.IsTrue(cover.ClassListContains("travel-cover--covered"));
            f.Reveal(); Assert.AreEqual(TravelPhase.Arriving, f.Travel.Phase); Assert.IsFalse(cover.ClassListContains("travel-cover--covered"));
            f.Tick(60); Assert.AreEqual(TravelPhase.Idle, f.Travel.Phase);
            Services.Unregister(f.UI); f.System.Update(); Assert.IsNull(f.System.Cover); Assert.IsNull(cover.parent);
            Assert.IsNotNull(f.UI.Panel, "Presentation detaches its element without disposing caller-owned UI.");
            Services.Register(f.UI); f.System.Update(); Assert.AreNotSame(cover, f.System.Cover);
            Assert.AreSame(f.System.Cover, hud[hud.childCount - 1]);
        }
        [UnityTest] public IEnumerator Cover_OpaqueAtCommit()
        {
            using var f = new TravelPresentationFixture(); yield return null;
            Assert.That(f.System.Cover.resolvedStyle.opacity, Is.EqualTo(0).Within(.001f));
            f.Start(); f.System.Update(); f.Tick(30); // Commit immediately using the real owner's required fixed tick count.
            yield return null;
            Assert.AreEqual(TravelPhase.Transit, f.Travel.Phase);
            Assert.That(f.System.Cover.resolvedStyle.opacity, Is.EqualTo(1).Within(.001f), "Commit must be opaque even when spool ticks were driven faster than wall time.");
            double start = Time.realtimeSinceStartupAsDouble; f.Reveal();
            while (f.System.Cover.resolvedStyle.opacity > .001f)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble - start, .6, "The specified 0.5 s reveal fade must finish within 0.6 s.");
                yield return null; // Only this required opacity assertion waits for the actual UI transition.
            }
            Assert.That(f.System.Cover.resolvedStyle.opacity, Is.EqualTo(0).Within(.001f));
        }
        [Test] public void GateEmission_FollowsLane()
        {
            if (!EntitiesGraphicsSystem.EntitiesGraphicsEnabled) Assert.Ignore("Actual celestial child registration requires the lead's graphics-enabled run.");
            using var f = new TravelPresentationFixture();
            f.World.GetOrCreateSystemManaged<PresentationSystemGroup>(); f.World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>();
            var visuals = f.World.GetOrCreateSystemManaged<CelestialVisualSystem>();
            f.EM.CreateSingleton(new MainCameraState()); f.EM.CreateSingleton(new StreamingConfig { MaxRenderDistanceM = 25000 });
            var body = new StarSystemBody { Id = new BodyId(f.From, Game.Data.Definitions.BodyRole.LaneGate, 0, 0),
                TemplateIndex = f.Galaxy.Galaxy.BodyTemplateIndexOf(f.Gate), RadiusM = 100 };
            Entity CreateAnchor()
            {
                var root = f.EM.CreateEntity(typeof(StarSystemBody), typeof(LocalTransform), typeof(LocalToWorld));
                f.EM.SetComponentData(root, body); f.EM.SetComponentData(root, LocalTransform.Identity);
                var child = visuals.Create(root, f.Visual, 100); Assert.AreNotEqual(Entity.Null, child);
                body.Visual = child; f.EM.SetComponentData(root, body); return root;
            }
            var anchor = CreateAnchor(); f.System.Update(); var color = f.Visual.EmissionColor;
            var open = new float4(color.r, color.g, color.b, color.a); var black = new float4(0, 0, 0, 1);
            Assert.AreNotEqual(black, open, "The fixture must distinguish open and locked emissions.");
            Assert.AreEqual(open, f.EM.GetComponentData<URPMaterialPropertyEmissionColor>(body.Visual).Value);
            f.SetGate(false); f.System.Update(); Assert.AreEqual(black, f.EM.GetComponentData<URPMaterialPropertyEmissionColor>(body.Visual).Value);
            visuals.Update(); f.EM.CompleteAllTrackedJobs(); Assert.AreEqual(black, f.EM.GetComponentData<URPMaterialPropertyEmissionColor>(body.Visual).Value);
            f.EM.DestroyEntity(body.Visual); f.EM.DestroyEntity(anchor); anchor = CreateAnchor(); f.System.Update();
            Assert.AreEqual(black, f.EM.GetComponentData<URPMaterialPropertyEmissionColor>(body.Visual).Value, "T1 anchor refresh inherits the lock without a lane revision change.");
            f.SetGate(true); f.System.Update(); Assert.AreEqual(open, f.EM.GetComponentData<URPMaterialPropertyEmissionColor>(body.Visual).Value);
        }
        [UnityTest] public IEnumerator Layout_NoIssues()
        {
            var issues = new List<LayoutIssue>();
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            {
                using var f = new TravelPresentationFixture(size.x, size.y); f.Start(); f.Tick(30);
                yield return null; yield return null;
                Assert.Greater(f.System.Cover.worldBound.width, 0);
                UILayoutAudit.Run(f.UI.RootElement, f.Panel.Viewport, issues);
                Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            }
        }
    }
}
