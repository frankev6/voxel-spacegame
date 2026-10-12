# ADR-0054 — Separate the job-safe grid serial allocator from prototype containers

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

FND-020's first live run found that `ComponentLookup<GridPrototypes>` is illegal inside a job: the component contains native hash maps, producing a nested-native-container safety exception. SPEC-10 Q6 requires SplitSystem to allocate the same session-unique grid serials as GridSpawnSystem. Passing an unsafe pointer or reserving unused serial ranges every tick would weaken ownership or exhaust serials unnecessarily.

## Decision

Move `GridPrototypes.NextSerial` into `GridSerialAllocator : IComponentData { uint Next; }`, on the same world-owned singleton entity. Initialize Next to 1 in VoxelSingletons. GridSpawnSystem reads and writes it at its existing structural initialization boundary. SplitSystem uses a cached writable component lookup in its serial scheduled job. ECS tracks the write dependency; the structural spawn boundary completes it before allocating more serials. Pass the immutable brick prototype Entity into the split job by value. No prototype map enters that job.

Update the SPEC-10/13 contract and the existing body-factory fixture to use the new allocator. Every actual new root consumes exactly one serial. No new native allocation or lifetime owner is introduced. Weapon component registration also uses Burst-safe ComponentType.ReadWrite values rather than managed Type conversion.

## Validation

Compile, live spawning plus damage splits, required FND-020 integration tests, and the existing body-factory tests. No fixed-step completion or unsafe safety bypass is introduced.
