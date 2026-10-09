# ADR-0001 — Fresh project with a clean severance from the prototype

**Status:** Accepted (owner, 2026-10-09)

## Context
The prototype's core representation (one entity per voxel, three different ship models) cannot scale, and its repo carries 1.9 GB of history, unused vendor packs and legally risky art. See docs/reference/audit-2026-10/00-synthesis.md §1 and §8.

## Decision
v2 is a new Unity 6000.6.5f1 project and a new git repository with no shared history. No code is copied from the prototype. Content arrives only as neutral data in `Import/Legacy/` and is converted by SPEC-72. A small, explicit set of placeholder art and audio was copied with its `.meta` files (listed in `Assets/Game/Art/_Placeholder/README.md`). The prototype stays available as a playable reference for feel and content.

## Consequences
No legacy bugs or patterns leak in. Every system must be rebuilt from its spec. The audit is kept under `docs/reference/` as background only; specs win where they disagree.

## Alternatives considered
Refactoring in place (rejected: the voxel model change touches everything at once; repo hygiene costs more than a fresh start).
