using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Galaxy.Components;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Streaming;
using Game.Presentation.Components;
using Game.Presentation.World;
using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using EcsWorld = Unity.Entities.World;
using Collider = Unity.Physics.Collider;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>SYS-011's three direct cases. Actual registration needs graphics; transform/hide jobs also run without it.</summary>
    public sealed class CelestialVisualTests
    {
        sealed class FailingFactory : ICelestialVisualFactory
        {
            readonly ICelestialVisualFactory factory; int calls;
            internal FailingFactory(ICelestialVisualFactory factory) => this.factory = factory;
            public Entity Create(Entity anchor, BodyVisualDefinition definition, float radius)
            { if (++calls == 2) throw new InvalidOperationException("Injected second-child failure"); return factory.Create(anchor, definition, radius); }
        }
        sealed class Fixture : System.IDisposable
        {
            internal readonly EcsWorld World = new("Celestial visual fixture");
            internal readonly CelestialVisualSystem System;
            internal readonly BodyVisualDefinition Definition;
            internal EntityManager EM => World.EntityManager;
            internal Fixture(bool graphics = false)
            {
                try
                {
                    Definition = AssetDatabase.LoadAssetAtPath<BodyVisualDefinition>("Assets/Game/Content/Bodies/bodyvis_nocturne_celestial.asset");
                    Assert.IsNotNull(Definition); Assert.IsNotNull(Definition.Mesh); Assert.IsNotNull(Definition.Material);
                    if (graphics) { World.GetOrCreateSystemManaged<PresentationSystemGroup>(); World.GetOrCreateSystemManaged<EntitiesGraphicsSystem>(); }
                    System = World.GetOrCreateSystemManaged<CelestialVisualSystem>();
                    EM.CreateSingleton(new StreamingConfig { MaxRenderDistanceM = 25000 });
                    EM.CreateSingleton(new MainCameraState { Position = float3.zero });
                }
                catch { World.Dispose(); throw; }
            }
            internal Entity Anchor(float3 position, float radius = 1000)
            {
                var root = EM.CreateEntity(typeof(StarSystemBody), typeof(LocalTransform), typeof(LocalToWorld), typeof(UniversePosition));
                EM.SetComponentData(root, new StarSystemBody { Id = new BodyId(new StarSystemId(1), BodyRole.Planet, 0, 0), RadiusM = radius });
                EM.SetComponentData(root, LocalTransform.FromPosition(position));
                EM.SetComponentData(root, new LocalToWorld { Value = float4x4.Translate(position) });
                EM.SetComponentData(root, new UniversePosition { Value = position }); return root;
            }
            internal void Link(Entity root, Entity child)
            { var body = EM.GetComponentData<StarSystemBody>(root); body.Visual = child; EM.SetComponentData(root, body); }
            internal Entity TransformChild(Entity root, float radius = 1000)
            {
                // CPU fixture arranges only render data; it does not pretend IDs were registered or replace the factory.
                var child = EM.CreateEntity(typeof(CelestialVisual), typeof(LocalTransform), typeof(LocalToWorld), typeof(Parent), typeof(MaterialMeshInfo));
                EM.SetComponentData(child, new CelestialVisual { Anchor = root, BaseScale = radius / Definition.MeshRadius,
                    HideWithGrid = (byte)(Definition.HideWhenGridPresent ? 1 : 0) });
                EM.SetComponentData(child, new Parent { Value = root }); Link(root, child); return child;
            }
            internal void Update() { System.Update(); EM.CompleteAllTrackedJobs(); }
            public void Dispose() => World.Dispose();
        }
        [Test] public void RegisteredOnce()
        {
            if (!EntitiesGraphicsSystem.EntitiesGraphicsEnabled) Assert.Ignore("Actual mesh/material registration requires the lead's graphics-enabled run.");
            using var f = new Fixture(true);
            var a = f.Anchor(new float3(100, 0, 0)); var b = f.Anchor(new float3(200, 0, 0));
            var first = f.System.Create(a, f.Definition, 1000); var second = f.System.Create(b, f.Definition, 1000);
            Assert.AreNotEqual(Entity.Null, first); Assert.AreNotEqual(Entity.Null, second); f.Link(a, first); f.Link(b, second);
            Assert.AreEqual(1, f.System.RegisteredDefinitionCount);
            var one = f.EM.GetComponentData<MaterialMeshInfo>(first); var two = f.EM.GetComponentData<MaterialMeshInfo>(second);
            Assert.AreEqual(one.MeshID, two.MeshID); Assert.AreEqual(one.MaterialID, two.MaterialID);
            var graphics = f.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            Assert.AreSame(f.Definition.Mesh, graphics.GetMesh(one.MeshID)); Assert.AreSame(f.Definition.Material, graphics.GetMaterial(one.MaterialID));
            Assert.AreEqual(a, f.EM.GetComponentData<Parent>(first).Value);
            Assert.AreEqual(1000 / f.Definition.MeshRadius, f.EM.GetComponentData<LocalTransform>(first).Scale);
            // A future gate writer's emission must survive all compression/visibility updates.
            var emission = new float4(.2f, .3f, .4f, 1); f.EM.SetComponentData(first, new URPMaterialPropertyEmissionColor { Value = emission });
            f.Update(); Assert.AreEqual(emission, f.EM.GetComponentData<URPMaterialPropertyEmissionColor>(first).Value);
            f.EM.DestroyEntity(first); first = f.System.Create(a, f.Definition, 1000); f.Link(a, first);
            Assert.AreEqual(1, f.System.RegisteredDefinitionCount); Assert.AreEqual(one.MeshID, f.EM.GetComponentData<MaterialMeshInfo>(first).MeshID);
            // Exercise the actual T1 bridge and its linked-child cleanup within this required case.
            f.EM.DestroyEntity(first); f.EM.DestroyEntity(second); f.EM.DestroyEntity(a); f.EM.DestroyEntity(b);
            var template = AssetDatabase.LoadAssetAtPath<BodyTemplateDefinition>("Assets/Game/Content/Bodies/body_planet_terrestrial.asset");
            Assert.AreSame(f.Definition, template.Visual);
            var id = new StarSystemId(1);
            using var galaxy = new GalaxyDef(1, 1, Array.Empty<StarSystemTemplateDefinition>(), Array.Empty<AuthoredStarSystemDefinition>(), new[] { template }, id);
            using var content = new StarSystemContent(id); using var delta = new StarSystemDelta(id);
            for (ushort slot = 0; slot < 2; slot++) content.Bodies.Add(new BodySpec { Id = new BodyId(id, BodyRole.Planet, slot, 0),
                TemplateIndex = 0, RadiusM = 1000, Orbit = new OrbitElements { Mode = OrbitMode.Fixed } });
            var loaded = new Dictionary<ulong, LoadedBody>(); var colliders = new Dictionary<float, BlobAssetReference<Collider>>();
            try
            {
                AnchorBuilder.Create(f.World, galaxy, content, delta, 0, double3.zero, loaded, colliders, f.System);
                Assert.AreEqual(2, loaded.Count); Assert.AreEqual(1, f.System.RegisteredDefinitionCount);
                foreach (var body in loaded.Values)
                {
                    var visual = f.EM.GetComponentData<StarSystemBody>(body.Anchor).Visual;
                    Assert.IsTrue(f.EM.Exists(visual)); Assert.AreEqual(id, f.EM.GetSharedComponent<StarSystemMember>(visual).Id);
                    f.EM.DestroyEntity(visual); f.EM.DestroyEntity(body.Anchor);
                }
                loaded.Clear();
                Assert.Throws<InvalidOperationException>(() => AnchorBuilder.Create(f.World, galaxy, content, delta, 0, double3.zero, loaded, colliders, new FailingFactory(f.System)));
                using var roots = f.EM.CreateEntityQuery(typeof(StarSystemBody)); using var children = f.EM.CreateEntityQuery(typeof(CelestialVisual));
                Assert.IsTrue(roots.IsEmptyIgnoreFilter); Assert.IsTrue(children.IsEmptyIgnoreFilter); Assert.AreEqual(0, loaded.Count);
            }
            finally
            {
                foreach (var body in loaded.Values)
                {
                    if (!f.EM.Exists(body.Anchor)) continue;
                    var visual = f.EM.GetComponentData<StarSystemBody>(body.Anchor).Visual;
                    if (f.EM.Exists(visual)) f.EM.DestroyEntity(visual);
                    if (f.EM.Exists(body.Anchor)) f.EM.DestroyEntity(body.Anchor);
                }
                foreach (var blob in colliders.Values) if (blob.IsCreated) blob.Dispose();
            }
        }
        [Test] public void FarBody_SameAngularSize()
        {
            using var f = new Fixture(); var anchor = f.Anchor(new float3(200000, 0, 0)); var child = f.TransformChild(anchor);
            var physical = f.EM.GetComponentData<LocalTransform>(anchor); var universe = f.EM.GetComponentData<UniversePosition>(anchor);
            f.Update(); var local = f.EM.GetComponentData<LocalTransform>(child); var matrix = f.EM.GetComponentData<LocalToWorld>(child).Value;
            Assert.That(math.distance(float3.zero, matrix.c3.xyz), Is.EqualTo(25000).Within(.01f));
            Assert.That(local.Scale, Is.EqualTo((1000 / f.Definition.MeshRadius) * .125f).Within(.001f));
            Assert.That((f.Definition.MeshRadius * local.Scale) / 25000, Is.EqualTo(1000f / 200000).Within(1e-7f));
            Assert.AreEqual(physical, f.EM.GetComponentData<LocalTransform>(anchor)); Assert.AreEqual(universe.Value, f.EM.GetComponentData<UniversePosition>(anchor).Value);
            Assert.That(math.length(local.Position - new float3(-175000, 0, 0)), Is.LessThan(.01f));
            // Returning inside the limit restores identity offset; no compressed position feeds simulation.
            f.EM.SetComponentData(anchor, LocalTransform.FromPosition(new float3(10000, 0, 0))); f.Update();
            local = f.EM.GetComponentData<LocalTransform>(child); Assert.AreEqual(float3.zero, local.Position);
            Assert.AreEqual(1000 / f.Definition.MeshRadius, local.Scale);
        }
        [Test] public void Impostor_HiddenWithGrid()
        {
            using var f = new Fixture(); Assert.IsTrue(f.Definition.HideWhenGridPresent);
            var anchor = f.Anchor(float3.zero); var child = f.TransformChild(anchor); f.Update();
            Assert.IsTrue(f.EM.IsComponentEnabled<MaterialMeshInfo>(child));
            var body = f.EM.GetComponentData<StarSystemBody>(anchor); body.Flags |= BodyFlags.HasGrid; f.EM.SetComponentData(anchor, body); f.Update();
            Assert.IsFalse(f.EM.IsComponentEnabled<MaterialMeshInfo>(child));
            body.Flags &= ~BodyFlags.HasGrid; f.EM.SetComponentData(anchor, body); f.Update();
            Assert.IsTrue(f.EM.IsComponentEnabled<MaterialMeshInfo>(child), "Disabled impostors must still be queried so they can reappear.");
        }
    }
}
