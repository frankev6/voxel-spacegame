# ADR-0024 — Spawn read-pin lifetime

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Context

SPEC-10 §6 promises that RequestSpawn pins its source immediately, while §5.7 also says to pin during conversion. Blindly doing both leaks a pin. A registered handle may be released while a grid exists, and scene teardown can destroy an unfinished spawn without running another gameplay update. SPEC-01 disposes composition services before the ECS world, so a cached blueprint must be unpinned before the library is disposed.

## Decision

- The runtime VoxelStructureRegistry owns a managed table of outstanding spawn reads. Each read has a session-unique token and a strong reference to the borrowed structure. It owns exactly one Pin/Unpin pair, never the structure itself.
- RequestSpawn pins immediately and puts an internal unmanaged SpawnReadToken component on the request (including the BeginInitialization ECB overload). Conversion adopts that token, instead of pinning twice. Direct requests and blueprint requests acquire a token during conversion. The same token follows the root until spawning finishes.
- Pending ECB commands must play through their owning BeginInitialization ECB. Tokens not yet bound to a real entity remain owned by the registry until playback or scene/session teardown. Recording failures release their token immediately.
- Releasing a public structure handle does not erase an outstanding read's strong reference. Invalid requests, completed spawns and destroyed bound requests/roots release their own token exactly once. GridSpawnSystem reconciles destroyed bound entities before processing spawns.
- VoxelStructureRegistry implements Core's ISceneExiting and IDisposable. Game.App registers the scene hook when it creates SceneFlow. Its early scene hook (order -10000) cancels outstanding reads before other scene owners dispose structures or destroy scene entities, then releases unpinned large blueprints. Registry disposal cancels any remaining reads but never disposes registered structures.
- GameComposition completes tracked ECS jobs and calls the public idempotent CancelSpawnReads before reverse-order service disposal, so later-created fleet owners can dispose their structures safely. Job-completion errors are logged without skipping the remaining service cleanup. Its existing RegistryLifetime wrapper disposes the registry before destroying the ScriptableObject. GridSpawnSystem.OnDestroy also cancels outstanding reads before disposing its world-owned native singletons; cancellation is idempotent.
- Public spawn request fields and existing spawn API signatures stay as specified. The token component and individual read-table operations are internal implementation details; CancelSpawnReads is the teardown-only public cancellation boundary. No package or assembly references change.

## Consequences

BlueprintLibrary can enforce its existing refusal to free pinned storage. The source remains protected between API submission and the last budgeted spawn frame, including when its public registry handle is released after conversion. Scene/session teardown has a defined cancellation path independent of GameplayActive. Callers still own and dispose their registered structures after releasing their handles.

The spawn tests must exercise immediate pinning, budgeted completion and teardown cancellation as part of the existing spawn/lifetime acceptance criteria. Rebuild and write-back use the same owner discipline in VOX-014.

## Alternatives considered

- Pin only at conversion: permits caller edits between RequestSpawn and conversion, violating §6.
- Look up the source by public handle every frame: loses the source needed to finish or unpin after Release.
- Rely only on GridSpawnSystem.OnDestroy: too late for composition-owned blueprint disposal and does not cover scene exit.
- Force-unpin every structure in BlueprintLibrary.Dispose: destroys ownership accounting and can free storage belonging to another reader.
