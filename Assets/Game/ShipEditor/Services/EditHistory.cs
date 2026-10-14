using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 §5.3. Owns diffs; EditSession applies voxel changes before moving this cursor.</summary>
    public sealed class EditHistory : IDisposable
    {
        readonly List<DiffCommand> commands = new();
        readonly long budget;
        readonly int maxEntries;
        int cursor, savedCursor;
        long totalBytes;
        bool disposed, warnedOversize;
        public EditHistory(long historyBudgetBytes = 64L * 1024 * 1024, int historyMaxEntries = 4096)
        {
            if (historyBudgetBytes < 0) throw new ArgumentOutOfRangeException(nameof(historyBudgetBytes));
            if (historyMaxEntries < 1) throw new ArgumentOutOfRangeException(nameof(historyMaxEntries));
            budget = historyBudgetBytes; maxEntries = historyMaxEntries;
        }
        public int Count => commands.Count;
        public int Cursor => cursor;
        public int SavedCursor => savedCursor;
        public long TotalBytes => totalBytes;
        public bool CanUndo => cursor > 0;
        public bool CanRedo => cursor < commands.Count;
        public bool IsDirty => cursor != savedCursor;
        public DiffCommand this[int index] { get { CheckAlive(); return commands[index]; } }
        public void Push(DiffCommand command, int strokeId)
        {
            CheckAlive(); if (command == null) throw new ArgumentNullException(nameof(command));
            command.CheckAlive(); if (command.Owned) throw new InvalidOperationException("Command already belongs to a history.");
            if (savedCursor > cursor) savedCursor = int.MinValue;
            for (int i = commands.Count - 1; i >= cursor; i--)
            { totalBytes -= commands[i].ByteSize; commands[i].Dispose(); commands.RemoveAt(i); }
            command.StrokeId = strokeId;
            if (strokeId != 0 && cursor > 0 && commands[cursor - 1].StrokeId == strokeId && commands[cursor - 1].Tool == command.Tool)
            {
                var top = commands[cursor - 1]; long previous = top.ByteSize;
                top.Append(command); totalBytes += top.ByteSize - previous;
                if (savedCursor == cursor) savedCursor = int.MinValue;
                command.Owned = true; command.Dispose();
            }
            else { command.Owned = true; commands.Add(command); cursor++; totalBytes += command.ByteSize; }
            while ((totalBytes > budget || commands.Count > maxEntries) && commands.Count > 1)
            {
                totalBytes -= commands[0].ByteSize; commands[0].Dispose(); commands.RemoveAt(0); cursor--;
                if (savedCursor != int.MinValue) savedCursor = savedCursor <= 0 ? int.MinValue : savedCursor - 1;
            }
            if (totalBytes > budget && !warnedOversize)
            { warnedOversize = true; GameLog.Warn("EDT", "undo history trimmed"); }
        }
        public bool MoveUndo() { CheckAlive(); if (!CanUndo) return false; cursor--; return true; }
        public bool MoveRedo() { CheckAlive(); if (!CanRedo) return false; cursor++; return true; }
        public void MarkSaved() { CheckAlive(); savedCursor = cursor; }
        public void Clear()
        {
            CheckAlive(); foreach (var command in commands) command.Dispose(); commands.Clear();
            cursor = savedCursor = 0; totalBytes = 0;
        }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(EditHistory)); }
        public void Dispose() { if (disposed) return; Clear(); disposed = true; }
    }
}
