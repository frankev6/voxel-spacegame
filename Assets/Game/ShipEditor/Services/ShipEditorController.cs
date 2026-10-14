using System;
using System.Collections.Generic;
using Game.ShipEditor.Data;
using Game.ShipEditor.Tools;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §5.11: input, tool lifecycle and cached previews over one borrowed session.</summary>
    public sealed class ShipEditorController : IDisposable
    {
        readonly EditSession session;
        readonly EditorContext content;
        readonly ToolContext context;
        readonly Clipboard clipboard;
        readonly PasteTool paste;
        readonly Dictionary<ToolId, IEditorTool> tools = new();
        readonly Action requestLeave;
        readonly ProfilerMarker marker = new("ShipEditorController.Tick");
        internal readonly PreviewBuffer Preview = new();
        internal Selection Selection => context.Selection;
        IEditorTool active;
        ToolId previousTool = ToolId.Place;
        bool disposed, previewValid;
        (DraftPicker.HitKind, int3, int3, ushort, byte, byte, int, int, ToolId, int, bool, int) previewKey;
        public ShipEditorController(EditSession session, EditorContext content, Action requestLeave = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.requestLeave = requestLeave;
            context = new ToolContext(session, content);
            clipboard = new Clipboard(content); paste = new PasteTool(clipboard, session, content);
            Register(new PlaceTool()); Register(new RemoveTool()); Register(new PaintTool()); Register(new EyedropperTool());
            Register(new ReplaceTool()); Register(new SelectTool()); Register(paste);
            Register(new LineTool()); Register(new BoxTool()); Register(new FillTool());
            active = tools[ToolId.Place];
            for (int block = 1; block < content.Footprints.BlockCount; block++)
                if (content.Items.HasItem(block) && content.Definitions.Value.Blocks[block].AllowedOrientations != 0)
                { SelectBlock((ushort)block); break; }
        }
        internal void Register(IEditorTool tool) => tools.Add(tool.Id, tool);
        public ToolId ActiveTool => active.Id;
        public SymmetrySettings Symmetry => context.Symmetry;
        public bool ConsumesWheel => active.ConsumesWheel;
        public int StateVersion { get; private set; }
        public ushort SelectedBlock => context.SelectedBlock;
        public byte Orient => context.Orient;
        public byte Color => context.Color;
        public bool SliceEnabled { get; private set; }
        public int SliceLevel { get; private set; }
        public bool XRay { get; private set; }
        public BoxMode BoxMode { get; private set; }
        public DraftPicker.Hit Hover => context.Hover;
        public PlacementError LastError => context.Error;
        public event Action<float3, float> FocusRequested;
        internal void Focus(float3 centre, float radius) => FocusRequested?.Invoke(centre, radius);
        public void Tick(in EditorInputFrame input, Ray gridRay)
        {
            CheckAlive(); using var scope = marker.Auto();
            Actions(input.Actions, input.Shift);
            var previous = context.Hover;
            context.GridRay = gridRay;
            context.Hover = input.PointerAllowed ? DraftPicker.Raycast(session.Draft, content, gridRay, SliceEnabled, SliceLevel, XRay) : default;
            if (previous.Kind != context.Hover.Kind || !previous.Cell.Equals(context.Hover.Cell) || !previous.Normal.Equals(context.Hover.Normal)) StateVersion++;
            var palette = (SelectedBlock, Orient, Color); int toolVersion = active.Version, revision = session.Revision, selectionVersion = Selection.Version;
            context.BoxMode = BoxMode;
            active.Tick(context, input);
            if (palette != (SelectedBlock, Orient, Color) || toolVersion != active.Version || revision != session.Revision || selectionVersion != Selection.Version) StateVersion++;
            var key = (context.Hover.Kind, context.Hover.Cell, context.Hover.Normal, SelectedBlock, Orient, Color,
                Symmetry.Version, session.Revision, ActiveTool, active.Version, input.PointerAllowed, Selection.Version);
            if (!previewValid || !previewKey.Equals(key))
            {
                if (input.PointerAllowed) active.BuildPreview(context, Preview); else Preview.Clear();
                previewKey = key; previewValid = true;
            }
        }
        void Actions(EditorActions actions, bool reverse)
        {
            bool Has(EditorActions action) => (actions & action) != 0;
            if (Has(EditorActions.Undo)) { active.Cancel(); if (session.Undo()) StateVersion++; }
            if (Has(EditorActions.Redo)) { active.Cancel(); if (session.Redo()) StateVersion++; }
            for (int i = 0; i < 9; i++) if (((ulong)actions & (1ul << (9 + i))) != 0) SelectTool((ToolId)(i + 1));
            if (ActiveTool == ToolId.Select)
            {
                int before = Selection.Version; context.BeginStroke();
                if (Has(EditorActions.RotateYaw)) Selection.Rotate(reverse ? -1 : 1, context.StrokeId);
                if (Has(EditorActions.MirrorSelection)) Selection.Mirror(context.StrokeId);
                int3 move = new((Has(EditorActions.MoveRight) ? 1 : 0) - (Has(EditorActions.MoveLeft) ? 1 : 0),
                    (Has(EditorActions.MoveUp) ? 1 : 0) - (Has(EditorActions.MoveDown) ? 1 : 0),
                    (Has(EditorActions.MoveForward) ? 1 : 0) - (Has(EditorActions.MoveBack) ? 1 : 0));
                if (math.any(move != 0)) Selection.Move(move, context.StrokeId);
                if (before != Selection.Version) Changed(); context.SetError(Selection.Error);
            }
            else if (ActiveTool == ToolId.Paste)
            {
                if (Has(EditorActions.RotateYaw)) { paste.Rotate(content, reverse); Changed(); }
            }
            else
            {
                if (Has(EditorActions.RotateYaw)) Rotate(1, reverse);
                if (Has(EditorActions.RotatePitch)) Rotate(0, reverse);
                if (Has(EditorActions.RotateRoll)) Rotate(2, reverse);
            }
            if (Has(EditorActions.SelectAll)) { SelectTool(ToolId.Select); Selection.SelectAll(); Changed(); }
            if (Has(EditorActions.Delete) && Selection.Count != 0)
            { context.BeginStroke(); Selection.Delete(context.StrokeId); context.SetError(Selection.Error); Changed(); }
            if ((Has(EditorActions.Copy) || Has(EditorActions.Cut)) && Selection.Count != 0)
            {
                if (clipboard.Copy(Selection))
                {
                    paste.Reset();
                    if (Has(EditorActions.Cut))
                    { active.Cancel(); context.BeginStroke(); Selection.Delete(context.StrokeId, "Cut selection"); context.SetError(Selection.Error); }
                    else context.SetError(PlacementError.None);
                    Changed();
                }
                else { context.SetError(clipboard.Error); Changed(); }
            }
            if (Has(EditorActions.Paste) && clipboard.Count != 0) { paste.Reset(); SelectTool(ToolId.Paste); Changed(); }
            if (Has(EditorActions.MirrorX)) ToggleMirror(0);
            if (Has(EditorActions.MirrorY)) ToggleMirror(1);
            if (Has(EditorActions.MirrorZ)) ToggleMirror(2);
            if (Has(EditorActions.SliceToggle)) ToggleSlice();
            if (Has(EditorActions.SliceUp)) StepSlice(1);
            if (Has(EditorActions.SliceDown)) StepSlice(-1);
            if (Has(EditorActions.XRayToggle)) ToggleXRay();
            if (Has(EditorActions.BoxModeCycle)) CycleBoxMode();
        }
        internal void Rotate(int axis, bool reverse)
        {
            if (SelectedBlock == 0) return;
            ref var table = ref content.Orientations.Value;
            byte rotation = table.AxisTurn(axis, reverse ? -1 : 1), next = Orient;
            uint allowed = content.Definitions.Value.Blocks[SelectedBlock].AllowedOrientations;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                next = table.Compose(rotation, next);
                if ((allowed & (1u << next)) == 0) continue;
                if (next != context.Orient) { context.Orient = next; Changed(); } return;
            }
        }
        public void SelectTool(ToolId tool)
        {
            CheckAlive();
            if (active.Id == tool || !tools.TryGetValue(tool, out var next)) return;
            // TODO(EDT-015): register line, box and fill.
            active.Cancel(); if (tool == ToolId.Paste) previousTool = active.Id;
            active = next; Changed();
        }
        public void SelectBlock(ushort block)
        {
            CheckAlive(); if (block == 0 || block >= content.Footprints.BlockCount) throw new ArgumentOutOfRangeException(nameof(block));
            uint allowed = content.Definitions.Value.Blocks[block].AllowedOrientations;
            if (allowed == 0) return;
            byte orientation = Orient;
            if ((allowed & (1u << orientation)) == 0)
                for (byte i = 0; i < 24; i++) if ((allowed & (1u << i)) != 0) { orientation = i; break; }
            if (SelectedBlock == block && Orient == orientation) return;
            context.SelectedBlock = block; context.Orient = orientation; Changed();
        }
        public void SelectColor(byte color)
        {
            CheckAlive(); if (color >= content.ColorCount) throw new ArgumentOutOfRangeException(nameof(color));
            if (Color != color) { context.Color = color; Changed(); }
        }
        public void ToggleMirror(int axis)
        {
            CheckAlive(); if ((uint)axis >= 3) throw new ArgumentOutOfRangeException(nameof(axis));
            bool enabled = axis == 0 ? Symmetry.MirrorX : axis == 1 ? Symmetry.MirrorY : Symmetry.MirrorZ;
            context.Mapper.SetEnabled(ref context.Symmetry, axis, !enabled, session.Placement.Core); Changed();
        }
        public void NudgeMirrorPlane(int axis, int halfCells)
        { CheckAlive(); Validation.SymmetryMapper.Nudge(ref context.Symmetry, axis, halfCells); Changed(); }
        public void ToggleSlice() { CheckAlive(); SliceEnabled = !SliceEnabled; Changed(); }
        public void StepSlice(int delta) { CheckAlive(); SliceLevel = checked(SliceLevel + delta); Changed(); }
        public void ToggleXRay() { CheckAlive(); XRay = !XRay; Changed(); }
        public void CycleBoxMode() { CheckAlive(); BoxMode = (BoxMode)(((int)BoxMode + 1) % 3); Changed(); }
        public void BeginStamp(VoxelStructure stampSource)
        {
            CheckAlive();
            if (clipboard.Copy(stampSource)) { paste.Reset(); SelectTool(ToolId.Paste); }
            context.SetError(clipboard.Error); Changed();
        }
        public bool Cancel()
        {
            CheckAlive();
            if (active.Cancel()) { Changed(); return true; }
            if (ActiveTool == ToolId.Paste) { SelectTool(previousTool); return true; }
            if (Selection.Count != 0) { Selection.Clear(); Changed(); return true; }
            requestLeave?.Invoke(); return true;
        }
        void Changed() { StateVersion++; previewValid = false; }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(ShipEditorController)); }
        public void Dispose()
        {
            if (disposed) return;
            active?.Cancel(); foreach (var tool in tools.Values) if (tool is IDisposable owner) owner.Dispose();
            context.Dispose(); clipboard.Dispose(); Preview.Dispose(); tools.Clear(); FocusRequested = null; disposed = true;
        }
    }
}
