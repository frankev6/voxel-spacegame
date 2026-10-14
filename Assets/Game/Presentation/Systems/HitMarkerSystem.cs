using Game.Core.Systems;
using Game.Data.Definitions;
using Game.Presentation.Services;
using Game.UI.Data;
using Game.Voxel.PresentationStreams;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.Systems
{
    /// <summary>QA-001 hit confirmation: the player's own hull hits pop a small cross at the impact point, fading in
    /// Seconds. Nearby hits refresh an existing marker instead of stacking. Pooled; reads the FX frame like camera shake.</summary>
    [Unity.Entities.UpdateInGroup(typeof(PresentationBridgeGroup))]
    [Unity.Entities.UpdateAfter(typeof(CameraTargetSyncSystem))]
    public partial class HitMarkerSystem : Unity.Entities.SystemBase
    {
        const int Pool = 12; const float Seconds = .22f, MergePx = 28;
        static readonly ProfilerMarker Marker = new("HitMarkerSystem.Update");
        sealed class Mark { internal Label Label; internal Vector3 World; internal float Age = float.MaxValue; }
        readonly Mark[] marks = new Mark[Pool];
        VisualElement root; Camera camera; IFxFrameReader fx;
        public void Bind(VisualElement worldLayer, Camera camera, IFxFrameReader fx)
        {
            Unbind();
            if (worldLayer == null || camera == null || fx == null) return;
            this.camera = camera; this.fx = fx;
            root = new VisualElement { name = "hit-markers", pickingMode = PickingMode.Ignore }; root.AddToClassList("world-overlay");
            for (int i = 0; i < Pool; i++)
            {
                var label = new Label("×") { name = "hit-marker-" + i, pickingMode = PickingMode.Ignore };
                label.AddToClassList("hit-marker"); label.AddToClassList(UIStyleClasses.IsHidden);
                marks[i] = new Mark { Label = label }; root.Add(label);
            }
            worldLayer.Add(root);
        }
        public void Unbind() { root?.RemoveFromHierarchy(); root = null; camera = null; fx = null; for (int i = 0; i < Pool; i++) marks[i] = null; }
        protected override void OnDestroy() => Unbind();
        protected override void OnUpdate()
        {
            if (root == null || root.panel == null || camera == null) return;
            using var scope = Marker.Auto();
            float dt = math.clamp(UnityEngine.Time.unscaledDeltaTime, 0, .1f);
            var events = fx.Current;
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                if ((e.Kind != FxKind.HitKinetic && e.Kind != FxKind.HitEnergy) || (e.Flags & FxEventFlags.PlayerSource) == 0) continue;
                Spawn(e.Pos, e.Kind == FxKind.HitEnergy);
            }
            foreach (var mark in marks)
            {
                if (mark.Age >= Seconds) continue;
                mark.Age += dt;
                if (mark.Age >= Seconds || camera.WorldToViewportPoint(mark.World).z <= 0) { mark.Age = float.MaxValue; mark.Label.AddToClassList(UIStyleClasses.IsHidden); continue; }
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, mark.World, camera);
                float k = mark.Age / Seconds;
                mark.Label.style.left = p.x; mark.Label.style.top = p.y;
                mark.Label.style.scale = new Scale(Vector2.one * math.lerp(1.6f, 1f, math.saturate(k * 4)));
                mark.Label.style.opacity = 1 - k * k;
            }
        }
        void Spawn(float3 position, bool energy)
        {
            if (camera.WorldToViewportPoint(position).z <= 0) return;
            Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, position, camera);
            Mark target = null; float oldest = -1;
            foreach (var mark in marks)
            {
                if (mark.Age < Seconds && Vector2.Distance(RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, mark.World, camera), p) < MergePx) { target = mark; break; }
                float age = mark.Age >= Seconds ? float.MaxValue : mark.Age;
                if (age > oldest) { oldest = age; target = mark; }
            }
            target.World = position; target.Age = 0;
            target.Label.EnableInClassList("hit-marker--energy", energy); target.Label.RemoveFromClassList(UIStyleClasses.IsHidden);
        }
    }
}
