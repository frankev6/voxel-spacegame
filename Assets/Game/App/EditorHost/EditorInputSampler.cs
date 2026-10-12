using System;
using Game.Core;
using Game.Input;
using Game.ShipEditor.Data;
using UnityEngine;

namespace Game.App.EditorHost
{
    /// <summary>SPEC-03 §5.4 / ADR-0048: one complete frame for the ship editor host.</summary>
    public sealed class EditorInputSampler
    {
        readonly InputService input;
        readonly IWorldPointerGate pointerGate;
        public EditorInputSampler(InputService input, IWorldPointerGate pointerGate)
        {
            this.input = input ?? throw new ArgumentNullException(nameof(input));
            this.pointerGate = pointerGate ?? throw new ArgumentNullException(nameof(pointerGate));
        }
        public void Sample(ref EditorInputFrame frame)
        {
            frame = default;
            var editor = input.Controls.Editor;
            if (!editor.enabled) return;
            Vector2 pointer = editor.Pointer.ReadValue<Vector2>();
            frame.PointerScreen = pointer;
            frame.PointerAllowed = pointerGate.IsWorldPointerAllowed(pointer);
            frame.PrimaryPressed = editor.Primary.WasPressedThisFrame();
            frame.PrimaryHeld = editor.Primary.IsPressed();
            frame.PrimaryReleased = editor.Primary.WasReleasedThisFrame();
            frame.SecondaryPressed = editor.Secondary.WasPressedThisFrame();
            frame.SecondaryReleased = editor.Secondary.WasReleasedThisFrame();
            frame.Shift = editor.ModShift.IsPressed();
            frame.Ctrl = editor.ModCtrl.IsPressed();
            frame.Alt = editor.ModAlt.IsPressed();
            frame.ToolWheel = frame.Ctrl ? editor.ToolWheel.ReadValue<float>() : 0;
            if (editor.Undo.WasPressedThisFrame()) frame.Actions |= EditorActions.Undo;
            if (editor.Redo.WasPressedThisFrame()) frame.Actions |= EditorActions.Redo;
            if (editor.Copy.WasPressedThisFrame()) frame.Actions |= EditorActions.Copy;
            if (editor.Cut.WasPressedThisFrame()) frame.Actions |= EditorActions.Cut;
            if (editor.Paste.WasPressedThisFrame()) frame.Actions |= EditorActions.Paste;
            if (editor.Delete.WasPressedThisFrame()) frame.Actions |= EditorActions.Delete;
            if (editor.RotateYaw.WasPressedThisFrame()) frame.Actions |= EditorActions.RotateYaw;
            if (editor.RotatePitch.WasPressedThisFrame()) frame.Actions |= EditorActions.RotatePitch;
            if (editor.RotateRoll.WasPressedThisFrame()) frame.Actions |= EditorActions.RotateRoll;
            if (editor.Tool1.WasPressedThisFrame()) frame.Actions |= EditorActions.ToolPlace;
            if (editor.Tool2.WasPressedThisFrame()) frame.Actions |= EditorActions.ToolLine;
            if (editor.Tool3.WasPressedThisFrame()) frame.Actions |= EditorActions.ToolBox;
            if (editor.Tool4.WasPressedThisFrame()) frame.Actions |= EditorActions.ToolFill;
            if (editor.Tool5.WasPressedThisFrame()) frame.Actions |= EditorActions.ToolRemove;
            if (editor.Tool6.WasPressedThisFrame()) frame.Actions |= EditorActions.ToolReplace;
            if (editor.Tool7.WasPressedThisFrame()) frame.Actions |= EditorActions.ToolPaint;
            if (editor.Tool8.WasPressedThisFrame()) frame.Actions |= EditorActions.ToolEyedropper;
            if (editor.Tool9.WasPressedThisFrame()) frame.Actions |= EditorActions.ToolSelect;
            if (editor.MirrorX.WasPressedThisFrame()) frame.Actions |= EditorActions.MirrorX;
            if (editor.MirrorY.WasPressedThisFrame()) frame.Actions |= EditorActions.MirrorY;
            if (editor.MirrorZ.WasPressedThisFrame()) frame.Actions |= EditorActions.MirrorZ;
            if (editor.SliceToggle.WasPressedThisFrame()) frame.Actions |= EditorActions.SliceToggle;
            if (editor.SliceUp.WasPressedThisFrame()) frame.Actions |= EditorActions.SliceUp;
            if (editor.SliceDown.WasPressedThisFrame()) frame.Actions |= EditorActions.SliceDown;
            if (editor.XRay.WasPressedThisFrame()) frame.Actions |= EditorActions.XRayToggle;
            if (editor.BoxModeCycle.WasPressedThisFrame()) frame.Actions |= EditorActions.BoxModeCycle;
            if (editor.MoveLeft.WasPressedThisFrame()) frame.Actions |= EditorActions.MoveLeft;
            if (editor.MoveRight.WasPressedThisFrame()) frame.Actions |= EditorActions.MoveRight;
            if (editor.MoveForward.WasPressedThisFrame()) frame.Actions |= EditorActions.MoveForward;
            if (editor.MoveBack.WasPressedThisFrame()) frame.Actions |= EditorActions.MoveBack;
            if (editor.MoveUp.WasPressedThisFrame()) frame.Actions |= EditorActions.MoveUp;
            if (editor.MoveDown.WasPressedThisFrame()) frame.Actions |= EditorActions.MoveDown;
            if (editor.MirrorSelection.WasPressedThisFrame()) frame.Actions |= EditorActions.MirrorSelection;
            if (editor.SelectAll.WasPressedThisFrame()) frame.Actions |= EditorActions.SelectAll;
        }
    }
}
