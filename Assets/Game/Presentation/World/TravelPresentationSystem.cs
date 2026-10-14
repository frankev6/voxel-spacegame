using System.Collections.Generic;
using Game.Core.Systems;
using Game.Galaxy.Bodies;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.UI.Data;
using Game.UI.Services;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Rendering;
using UnityEngine.UIElements;

namespace Game.Presentation.World
{
    /// <summary>SPEC-32 §5.8: owns only its cover element and post-creation gate emission.</summary>
    [UpdateInGroup(typeof(PresentationBridgeGroup)), UpdateAfter(typeof(CelestialVisualSystem))]
    public partial class TravelPresentationSystem : SystemBase
    {
        static readonly ProfilerMarker Gates = new(ProfilerCategory.Scripts, "TravelPresentationSystem.Gates");
        readonly List<TimeValue> instant = new() { new TimeValue(0) };
        readonly List<StylePropertyName> noTransition = new();
        UIRoot ui;
        VisualElement layer, cover;
        bool covered, forced, hasGateState;
        uint revision;
        int generation, bodyOrder;
        StarSystemId loaded;
        EntityQuery bodies, current;
        internal VisualElement Cover => cover;
        protected override void OnCreate()
        {
            bodies = GetEntityQuery(ComponentType.ReadOnly<StarSystemBody>());
            current = GetEntityQuery(ComponentType.ReadOnly<CurrentStarSystemInfo>());
        }
        protected override void OnUpdate()
        {
            Game.Core.Services.TryGet<TravelService>(out var travel);
            UpdateCover(travel);
            if (travel == null || !Game.Core.Services.TryGet<GalaxyService>(out var galaxy) || galaxy.Galaxy == null || current.IsEmptyIgnoreFilter)
            { hasGateState = false; return; }
            var info = current.GetSingleton<CurrentStarSystemInfo>();
            if (info.Loaded == 0 || !galaxy.Galaxy.TryGetIndex(info.Id, out int index)) { hasGateState = false; return; }
            uint nextRevision = travel.Revision; int nextOrder = bodies.GetCombinedComponentOrderVersion(false);
            if (hasGateState && revision == nextRevision && loaded.Equals(info.Id) && generation == galaxy.GenerationCount && bodyOrder == nextOrder) return;
            using (Gates.Auto())
            {
                using var gates = new NativeList<StarSystemBody>(16, Allocator.TempJob);
                new CollectGates { System = info.Id, Gates = gates }.Run(bodies);
                var definition = galaxy.Galaxy; var system = definition.StarSystems[index];
                EntityManager.CompleteDependencyBeforeRW<URPMaterialPropertyEmissionColor>();
                for (int i = 0; i < gates.Length; i++)
                {
                    var body = gates[i];
                    if (body.Id.Slot >= system.LaneCount || body.Visual == Entity.Null || !EntityManager.Exists(body.Visual) ||
                        !EntityManager.HasComponent<URPMaterialPropertyEmissionColor>(body.Visual)) continue;
                    int lane = definition.Adjacency[system.FirstAdjacency + body.Id.Slot];
                    var visual = galaxy.GetBodyTemplate(body.TemplateIndex).Visual;
                    if (visual == null) continue;
                    var color = visual.EmissionColor;
                    var emission = travel.IsLaneOpen(lane) ? new float4(color.r, color.g, color.b, color.a) : new float4(0, 0, 0, 1);
                    EntityManager.SetComponentData(body.Visual, new URPMaterialPropertyEmissionColor { Value = emission });
                }
            }
            revision = nextRevision; loaded = info.Id; generation = galaxy.GenerationCount; bodyOrder = nextOrder; hasGateState = true;
        }
        void UpdateCover(TravelService travel)
        {
            Game.Core.Services.TryGet<UIRoot>(out var nextUi);
            if (!ReferenceEquals(ui, nextUi))
            {
                DetachCover(); ui = nextUi;
                if (ui != null)
                {
                    layer = ui.GetLayer(UILayer.Hud);
                    cover = new VisualElement { name = "travel-cover", pickingMode = PickingMode.Ignore };
                    cover.AddToClassList("travel-cover"); layer.Add(cover);
                }
            }
            if (cover == null) return;
            // Navigation may append another HUD root later; keep the cover above it without per-frame mutations.
            if (layer.childCount > 0 && !ReferenceEquals(layer[layer.childCount - 1], cover)) cover.BringToFront();
            bool nextCovered = travel != null && travel.CoverRequested;
            bool nextForced = nextCovered && travel.Phase == TravelPhase.Transit;
            if (nextForced != forced)
            {
                if (nextForced)
                { cover.style.transitionProperty = noTransition; cover.style.transitionDuration = instant; cover.style.opacity = 1; }
                else
                { cover.style.transitionProperty = StyleKeyword.Null; cover.style.transitionDuration = StyleKeyword.Null; cover.style.opacity = StyleKeyword.Null; }
                forced = nextForced;
            }
            if (nextCovered == covered) return;
            covered = nextCovered; cover.EnableInClassList("travel-cover--covered", covered);
        }
        void DetachCover()
        { cover?.RemoveFromHierarchy(); cover = layer = null; ui = null; covered = forced = false; }
        protected override void OnDestroy() => DetachCover();
        [BurstCompile]
        partial struct CollectGates : IJobEntity
        {
            public StarSystemId System;
            public NativeList<StarSystemBody> Gates;
            void Execute(in StarSystemBody body)
            { if (body.Id.Role == Game.Data.Definitions.BodyRole.LaneGate && body.Id.System.Equals(System)) Gates.Add(body); }
        }
    }
}
