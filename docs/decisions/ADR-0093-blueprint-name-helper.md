# ADR-0093 — Blueprint name helper

**Status:** Proposed (EDT-020, 2026-10-14; lead acceptance pending).

## Context

SPEC-51 assigns BlueprintNames.Sanitize to ShipEditor while the profile store is in App. The task names the helper but leaves its signatures and collision predicate unspecified. Existing IBlueprintStore, editor, persistence and UI interfaces already support the feature.

## Proposal

Add the task-owned public static BlueprintNames helper with Sanitize(string), Unique(string, Func<string, bool>) and IsId(string). App supplies the collision predicate against its profile files and reserved content IDs. Names use ASCII lowercase, replace invalid characters with a collapsed underscore, trim edge separators, fall back to blueprint, and remain at most 48 characters including suffixes beginning _2.

ProfileBlueprintStore and BlueprintBrowserActions remain internal App composition types. The browser uses the existing view-model/command contracts. No existing public signature, binding type, assembly reference, package, save-container or VOXS format changes.

## Consequences

One sanitizer owns file IDs; the storage owner retains authority over paths, content protection and file collisions. EDT-022 composes these existing-interface implementations and performs the existing full-design save validation before calling the storage interface.
