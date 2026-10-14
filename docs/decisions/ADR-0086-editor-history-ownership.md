# ADR-0086 — Edit history ownership and saved positions

**Status:** Accepted (Tech lead, 2026-10-13).

## Decision

Complete SPEC-51 EDT-004's unspecified history accessors within Game.ShipEditor. DiffCommand owns persistent Ops and Damage native lists, StrokeId, Tool and Label, reports logical ByteSize (24 bytes per op plus damage), and disposes idempotently. EditHistory takes budget/max-entry constructor arguments from ShipEditorConfig defaults. Push transfers command ownership; callers stop mutating/disposal after transfer. Reusing an owned/disposed command is rejected. History exposes Count, Cursor, SavedCursor, TotalBytes, CanUndo/CanRedo, IsDirty and indexed commands. MoveUndo/MoveRedo only move the cursor, called by EditSession after successful voxel application. MarkSaved records the current cursor. Clear/Dispose release owned commands; disposed history rejects further operations.

A merge rewrites the top history state: if that state was saved, invalidate the saved cursor. Dropping a redo branch containing the saved state likewise invalidates it. Trimming never decrements int.MinValue (the unreachable saved-state sentinel); it remains dirty until MarkSaved or Clear. This preserves SPEC-51 dirty tracking when two distinct states occupy the same numerical cursor. Damage offsets are rebased only when nonnegative; pristine -1 remains unchanged. Trim runs after both insertion and merging.

No Voxel writes happen here. EDT-005 still owns inverse traversal, atomic application and cursor movement after success. No assembly references or other module APIs change.
