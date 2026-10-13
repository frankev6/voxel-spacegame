using System;
using Game.UI.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Services
{
    /// <summary>Implements SPEC-60 §5.7: target callbacks owned and removed by TooltipService.</summary>
    internal sealed class TooltipManipulator : Manipulator
    {
        readonly TooltipService service;
        readonly Action<TooltipContent> fill;
        internal TooltipManipulator(TooltipService service, Action<TooltipContent> fill)
        { this.service = service; this.fill = fill; }
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerEnterEvent>(OnEnter);
            target.RegisterCallback<PointerLeaveEvent>(OnLeave);
            target.RegisterCallback<PointerMoveEvent>(OnMove);
            target.RegisterCallback<PointerDownEvent>(OnDown);
            target.RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }
        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerEnterEvent>(OnEnter);
            target.UnregisterCallback<PointerLeaveEvent>(OnLeave);
            target.UnregisterCallback<PointerMoveEvent>(OnMove);
            target.UnregisterCallback<PointerDownEvent>(OnDown);
            target.UnregisterCallback<DetachFromPanelEvent>(OnDetach);
        }
        void OnEnter(PointerEnterEvent evt) => service.Enter(target, fill, (Vector2)evt.position);
        void OnLeave(PointerLeaveEvent evt) => service.Cancel(target);
        void OnMove(PointerMoveEvent evt) => service.Move(target, (Vector2)evt.position);
        void OnDown(PointerDownEvent evt) => service.Cancel(target);
        void OnDetach(DetachFromPanelEvent evt) => service.Cancel(target);
    }
}
