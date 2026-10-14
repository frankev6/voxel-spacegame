using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Compiler;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Narrative;
using Game.Presentation.Components;
using Game.Presentation.World;
using Game.Simulation.Commands;
using Game.Tests.PlayMode.UI;
using Game.UI.Data;
using Game.UI.Services;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using EcsWorld = Unity.Entities.World;

namespace Game.Tests.PlayMode.Galaxy
{
    /// <summary>Two authored systems, one real fact-gated lane and real travel/UI owners; direct clocks, no game loop waits.</summary>
    internal sealed class TravelPresentationFixture : IDisposable
    {
        internal sealed class Loader : ITravelLoader
        {
            public bool IsReady { get; set; } = true;
            public StarSystemId Loaded { get; set; }
            public bool IsArrivalReady { get; set; }
            public bool IsAreaReady(double3 position) => IsArrivalReady;
            public bool BeginStarSystemChange(StarSystemId target, double3 position, float yaw, StarSystemEntryReason reason)
            { IsReady = false; return true; }
        }
        sealed class Audio : IAudioService
        {
            public void PlayCue(ContentId id) { }
            public void PlayCueAt(ContentId id, float3 position, float volume) { }
            public void PlayUi(UiSound sound) { }
            public void PlayStinger(ContentId id) { }
            public void SetMusicOverride(ContentId id, float seconds) { }
            public void ClearMusicOverride(float seconds) { }
        }
        sealed class Context : ILogicContext, ILogicEconomy
        {
            public ILogicFacts Facts { get; }
            internal Context(FactStore store) => Facts = store;
            public ILogicEconomy Economy => this;
            public ILogicNarrative Narrative => null;
            public ILogicWorld World => null;
            public LogicScope Scope => default;
            public ulong CampaignSeed => 42;
            public void PushScope(in LogicScope scope) => throw new NotSupportedException();
            public void PopScope() => throw new NotSupportedException();
            public int GetItemCount(int item) => 20;
            public bool TryTakeItems(int item, int count) => true;
            public long GetCredits() => 0;
            public void GiveItems(int item, int count) { }
            public void GiveCredits(long amount) { }
            public bool TryTakeCredits(long amount) => false;
            public void GiveLoot(int table, int picks, ulong salt) { }
            public event Action<int> ItemCountChanged { add { } remove { } }
            public event Action CreditsChanged { add { } remove { } }
        }
        readonly List<Object> owned = new();
        internal readonly EcsWorld World = new("Travel presentation fixture");
        internal readonly UITestPanel Panel;
        internal readonly UIRoot UI;
        internal readonly Loader TargetLoader = new();
        internal readonly GalaxyState State = new();
        internal readonly GalaxyService Galaxy;
        internal readonly TravelService Travel;
        internal readonly TravelPresentationSystem System;
        internal readonly FactStore Facts;
        internal readonly BodyTemplateDefinition Gate;
        internal readonly BodyVisualDefinition Visual;
        internal readonly StarSystemId From, To;
        readonly Context logic;
        readonly Entity clock;
        int frame;
        internal EntityManager EM => World.EntityManager;
        T Make<T>(string id, Action<SerializedObject> edit = null) where T : ContentDefinition
        {
            var asset = ScriptableObject.CreateInstance<T>(); owned.Add(asset); asset.name = id;
            using var so = new SerializedObject(asset); so.FindProperty("id").stringValue = id;
            edit?.Invoke(so); so.ApplyModifiedPropertiesWithoutUndo(); return asset;
        }
        internal TravelPresentationFixture(int width = 1280, int height = 720)
        {
            try
            {
                var star = AssetDatabase.LoadAssetAtPath<BodyTemplateDefinition>("Assets/Game/Content/Bodies/body_star_blackhole.asset");
                var sourceVisual = AssetDatabase.LoadAssetAtPath<BodyVisualDefinition>("Assets/Game/Content/Bodies/bodyvis_nocturne_celestial.asset");
                Visual = Object.Instantiate(sourceVisual); owned.Add(Visual);
                using (var so = new SerializedObject(Visual))
                { so.FindProperty("id").stringValue = "bodyvis_test_travel"; so.FindProperty("emissionColor").colorValue = new Color(.2f, .6f, .8f, 1); so.ApplyModifiedPropertiesWithoutUndo(); }
                Gate = Make<BodyTemplateDefinition>("body_test_gate", so =>
                { so.FindProperty("role").intValue = (int)BodyRole.LaneGate; so.FindProperty("visual").objectReferenceValue = Visual; });
                var template = Make<StarSystemTemplateDefinition>("systpl_test_travel", so =>
                {
                    foreach (string field in new[] { "minPlanets", "maxPlanets", "minBelts", "maxBelts", "minStations", "maxStations", "minPointsOfInterest", "maxPointsOfInterest" }) so.FindProperty(field).intValue = 0;
                    var pool = so.FindProperty("starPool"); pool.arraySize = 1;
                    pool.GetArrayElementAtIndex(0).FindPropertyRelative("template").objectReferenceValue = star;
                    pool.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1;
                });
                AuthoredStarSystemDefinition MakeSystem(string id, float x) => Make<AuthoredStarSystemDefinition>(id, so =>
                { so.FindProperty("template").objectReferenceValue = template; so.FindProperty("star").objectReferenceValue = star; so.FindProperty("positionLy").vector2Value = new Vector2(x, 0); });
                var a = MakeSystem("starsys_travel_a", 0); var b = MakeSystem("starsys_travel_b", 6);
                var generation = Make<GalaxyGenerationDefinition>("galaxy_test_travel", so =>
                {
                    so.FindProperty("fixedSeed").ulongValue = 42; so.FindProperty("startStarSystem").objectReferenceValue = a;
                    so.FindProperty("defaultHyperlaneGate").objectReferenceValue = Gate; so.FindProperty("defaultAncientGate").objectReferenceValue = Gate;
                    var pool = so.FindProperty("templatePool"); pool.arraySize = 1;
                    pool.GetArrayElementAtIndex(0).FindPropertyRelative("template").objectReferenceValue = template; pool.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1;
                    var systems = so.FindProperty("authoredStarSystems"); systems.arraySize = 2; systems.GetArrayElementAtIndex(0).objectReferenceValue = a; systems.GetArrayElementAtIndex(1).objectReferenceValue = b;
                    var lanes = so.FindProperty("authoredLanes"); lanes.arraySize = 1; var lane = lanes.GetArrayElementAtIndex(0);
                    lane.FindPropertyRelative("a").objectReferenceValue = a; lane.FindPropertyRelative("b").objectReferenceValue = b;
                    lane.FindPropertyRelative("condition.source").stringValue = "$gate_open";
                });
                var factDefinition = Make<FactCatalogDefinition>("facts_test_travel", so =>
                { var facts = so.FindProperty("facts"); facts.arraySize = 1; var fact = facts.GetArrayElementAtIndex(0); fact.FindPropertyRelative("Name").stringValue = "gate_open"; fact.FindPropertyRelative("Kind").intValue = (int)FactKind.Bool; });
                var fuel = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/resource_fuel.asset");
                var config = Make<TravelConfigDefinition>("config_travel", so => so.FindProperty("fuelItem").objectReferenceValue = fuel);
                var definitions = new ContentDefinition[] { AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"),
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset"), fuel, Visual, Gate, star, template, a, b, generation, factDefinition, config };
                var report = new ValidationReport("Travel presentation fixture"); var catalog = ContentCatalog.Build(definitions, report);
                var refs = RefResolverRegistry.CreateDefault(catalog); var factsCatalog = FactCatalog.Build(catalog, refs, report);
                generation.CompileLogic(new LogicCompiler(factsCatalog, refs, new LogicFunctionRegistry()), report.For("test:travel")); generation.BindLogic(new LogicBinder(factsCatalog, refs, report));
                NUnit.Framework.Assert.IsFalse(report.HasErrors, report.ToText());
                Facts = new FactStore(factsCatalog); logic = new Context(Facts); Services.Register<ILogicContext>(logic);
                Galaxy = new GalaxyService(catalog, State, World, new GalaxyFrameTicker()); Galaxy.NewGame(42);
                From = Galaxy.Galaxy.StartStarSystem; To = Galaxy.Galaxy.StarSystems[0].Id.Equals(From) ? Galaxy.Galaxy.StarSystems[1].Id : Galaxy.Galaxy.StarSystems[0].Id;
                TargetLoader.Loaded = From;
                EM.CreateEntity(typeof(WorldSceneActive)); EM.CreateEntity(typeof(PlayerControlLock));
                var player = EM.CreateEntity(typeof(PlayerShip), typeof(LocalTransform)); EM.SetComponentData(player, LocalTransform.Identity);
                clock = EM.CreateEntity(typeof(SimClock)); EM.CreateSingleton(new CurrentStarSystemInfo { Id = From, Loaded = 1 });
                Travel = new TravelService(Galaxy, TargetLoader, new DockingService(), World, catalog, new DialogTestContext.Pause(), new Audio());
                Services.Register(Galaxy); Services.Register(Travel); SetGate(true);
                Panel = new UITestPanel(width, height);
                var uiConfig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); owned.Add(uiConfig); uiConfig.PanelSettings = Panel.Settings;
                UI = new UIRoot(Panel.Document, uiConfig, new DialogTestContext.Pause(), new DialogTestContext.Context(), new NullUISoundSink(), new UnityFrameClock());
                Services.Register(UI);
                System = World.GetOrCreateSystemManaged<TravelPresentationSystem>(); System.Update();
            }
            catch { Dispose(); throw; }
        }
        internal void SetGate(bool open) { Facts.SetBySlot(Facts.Catalog.SlotOf("gate_open"), open ? 1 : 0, FactWriteSource.Debugger); EventHub.DrainAll(); }
        internal void Start() => NUnit.Framework.Assert.IsTrue(Travel.RequestJump(To, JumpSource.Dev).Accepted);
        internal void Tick(uint tick, float seconds = 0)
        { EM.SetComponentData(clock, new SimClock { Tick = tick }); Travel.Tick(new GalaxyFrameContext(EM, 0, tick, seconds, ++frame)); EventHub.DrainAll(); System.Update(); }
        internal void Reveal()
        { TargetLoader.Loaded = To; TargetLoader.IsReady = TargetLoader.IsArrivalReady = true; Tick(30, .25f); Tick(30, .25f); }
        public void Dispose()
        {
            if (World.IsCreated) { Travel?.Dispose(); Galaxy?.Dispose(); World.Dispose(); }
            UI?.Dispose(); Panel?.Dispose(); State.Dispose();
            if (logic != null) Services.Unregister<ILogicContext>(logic);
            if (Travel != null) Services.Unregister(Travel); if (Galaxy != null) Services.Unregister(Galaxy);
            foreach (var asset in owned) if (asset != null) Object.Destroy(asset); owned.Clear();
        }
    }
}
