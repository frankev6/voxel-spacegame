# ADR-0011 — Cinemachine, Localization and Yarn are not installed yet

**Status:** Accepted

## Context
The audit recommended removing unused packages and adding Cinemachine 3 and Yarn only when their systems are built.

## Decision
The initial manifest contains only packages in use (see `Packages/manifest.json`). SPEC-62 decides on Cinemachine 3 vs a custom rig via its own ADR task; SPEC-41 adds Yarn after its spike; Localization returns after the narrative stabilizes. Each addition needs an ADR.

## Consequences
Smaller, faster project; each addition is deliberate.

## Alternatives considered
Installing everything up front (rejected: bloat and unused dependencies).
