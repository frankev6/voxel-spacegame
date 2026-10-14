# ADR-0081 — Editor content context ownership

**Status:** Accepted (Tech lead, 2026-10-13).

## Context

EDT-001 exposes an empty public EditorContext because EditSession's constructor is public. EDT-002 must provide its specified tables, but constructor/accessor signatures and disposal ownership are unspecified.

## Decision

EditorContext accepts ShipEditorConfig, ContentCatalog, borrowed BlockDefsBlob/OrientationTablesBlob references and palette colour count (1–16). It owns/disposes FootprintCache only; content blobs remain owned by ContentService. Expose read-only properties for those references, tables, category values in enum order and colour count. FootprintCache.Get(block,orient) returns an entry with read-only native views, inclusive bounds and rotated face mask. Native allocations are Persistent and released on disposal or constructor failure; views cannot outlive the context. BlockItemMap exposes Count, HasItem, ItemOf and PriceOf; air/Missing/invalid indices have no item and zero price. No existing signatures, writer ownership or assembly references change.

## Consequences

EDT-022 can compose a session without duplicating content or owning shared blobs. EDT-003 onward consume the same canonical rotated-cell order. The five EDT-002 required tests provide acceptance; no editor UI or draft mutation is claimed here.
