using System;
using System.Text.RegularExpressions;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class EditHistoryTests
    {
        NativeLeakDetectionMode previous;
        [SetUp] public void Setup() { previous = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace; }
        [TearDown] public void Cleanup() { NativeLeakDetection.Mode = previous; }
        static DiffCommand Command(ToolId tool = ToolId.Place, params byte[] damage)
        {
            var command = new DiffCommand(tool, "stroke");
            foreach (byte value in damage) command.Damage.Add(value);
            command.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = new BlockRecord { Block = 1, DamageOffset = damage.Length == 0 ? -1 : 0 } });
            return command;
        }
        [Test] public void SameStroke_Merges_DamageRebased()
        {
            using var history = new EditHistory();
            var first = Command(ToolId.Remove, 0, 80, 0, 0, 192, 127);
            var second = Command(ToolId.Remove, 10, 20, 0, 0, 192, 127);
            history.Push(first, 1); history.MarkSaved(); history.Push(second, 1);
            Assert.AreEqual(1, history.Count); Assert.AreEqual(1, history.Cursor); Assert.IsTrue(second.IsDisposed);
            Assert.AreEqual(0, first.Ops[0].Record.DamageOffset); Assert.AreEqual(6, first.Ops[1].Record.DamageOffset);
            Assert.AreEqual(10, first.Damage[6]); Assert.AreEqual(60, history.TotalBytes);
            history.Push(Command(ToolId.Remove), 1); Assert.AreEqual(-1, first.Ops[2].Record.DamageOffset);
            Assert.IsTrue(history.IsDirty); Assert.AreEqual(int.MinValue, history.SavedCursor);
            history.MoveUndo(); history.MoveRedo(); Assert.IsTrue(history.IsDirty);
            history.MarkSaved(); Assert.IsFalse(history.IsDirty);
        }
        [Test] public void DifferentTool_DoesNotMerge()
        {
            using var history = new EditHistory(); history.Push(Command(), 1); history.Push(Command(ToolId.Remove), 1);
            Assert.AreEqual(2, history.Count); Assert.AreEqual(48, history.TotalBytes);
            history.Push(Command(ToolId.Remove), 0); history.Push(Command(ToolId.Remove), 0);
            Assert.AreEqual(4, history.Count);
        }
        [Test] public void PushAfterUndo_DisposesRedo()
        {
            using var history = new EditHistory(); var first = Command(); var redo = Command();
            history.Push(first, 0); history.Push(redo, 0); history.MarkSaved();
            Assert.IsTrue(history.MoveUndo()); Assert.IsTrue(history.CanRedo); history.Push(Command(), 0);
            Assert.IsTrue(redo.IsDisposed); Assert.IsFalse(first.IsDisposed); Assert.IsFalse(history.CanRedo);
            Assert.AreEqual(2, history.Count); Assert.AreEqual(48, history.TotalBytes); Assert.IsTrue(history.IsDirty);
            Assert.AreEqual(int.MinValue, history.SavedCursor);
        }
        [Test] public void OverBudget_TrimsOldest_DirtyKept()
        {
            using var history = new EditHistory(48); var first = Command(); history.Push(first, 0);
            history.Push(Command(), 0); history.Push(Command(), 0);
            Assert.IsTrue(first.IsDisposed); Assert.AreEqual(2, history.Count); Assert.AreEqual(48, history.TotalBytes);
            Assert.AreEqual(int.MinValue, history.SavedCursor); history.MoveUndo(); history.MoveUndo(); Assert.IsTrue(history.IsDirty);
            history.MoveRedo(); history.MoveRedo(); history.Push(Command(), 0); Assert.AreEqual(int.MinValue, history.SavedCursor);
            history.MarkSaved(); Assert.IsFalse(history.IsDirty);
            using var tiny = new EditHistory(24); tiny.Push(Command(), 7);
            LogAssert.Expect(LogType.Warning, new Regex("undo history trimmed")); tiny.Push(Command(), 7);
            Assert.AreEqual(1, tiny.Count); Assert.AreEqual(48, tiny.TotalBytes);
            tiny.Push(Command(), 7); Assert.AreEqual(72, tiny.TotalBytes); LogAssert.NoUnexpectedReceived();
        }
        [Test] public void MaxEntries_Trims()
        {
            using var history = new EditHistory(historyMaxEntries: 2); var first = Command(); history.Push(first, 0); history.MarkSaved();
            history.Push(Command(), 0); history.Push(Command(), 0);
            Assert.IsTrue(first.IsDisposed); Assert.AreEqual(2, history.Count); Assert.AreEqual(0, history.SavedCursor);
            history.MoveUndo(); history.MoveUndo(); Assert.IsFalse(history.IsDirty); Assert.IsFalse(history.MoveUndo());
            history.MoveRedo(); history.MoveRedo(); Assert.IsFalse(history.MoveRedo());
        }
        [Test] public void Clear_NoLeaks()
        {
            using var history = new EditHistory(); var first = Command(ToolId.Place, 1, 2, 3, 4, 5);
            var second = Command(); history.Push(first, 0); history.Push(second, 0); history.MoveUndo(); history.Clear();
            Assert.IsTrue(first.IsDisposed); Assert.IsTrue(second.IsDisposed); Assert.AreEqual(0, history.Count);
            Assert.AreEqual(0, history.TotalBytes); Assert.IsFalse(history.IsDirty); Assert.IsFalse(history.CanRedo);
            Assert.Throws<ObjectDisposedException>(() => { _ = first.Ops; });
            history.Clear(); history.Dispose(); history.Dispose();
        }
    }
}
