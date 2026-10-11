# ADR-0025 — Stow command-buffer contract

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous implementation instruction.

## Context

SPEC-10 exposes GridStowApi to fixed-step jobs through an EntityCommandBuffer.ParallelWriter, sort key and root entity. That API cannot read enable bits or determine whether PhysicsGraphicalInterpolationBuffer is present. Its documentation nevertheless asks for a conditional interpolation reset. Stowed entities must resume with a fresh interpolation history to avoid a visual streak.

## Decision

Keep the public signatures and end-of-tick playback behavior. Callers enforce the documented fully-spawned, body-initialized precondition using their own read-only lookups. Stow records SetEnabled(false), which cascades through LinkedEntityGroup. Unstow records pose and velocity, AddComponent(value) for PhysicsGraphicalInterpolationBuffer (sets the existing component or adds it), optional SplitGrace, and SetEnabled(true). The interpolation buffer is therefore guaranteed after unstow, including when absent before it. Neither API attempts an impossible synchronous state inspection through an ECB.

## Consequences

Dynamic and kinematic bodies retain the interpolation component they already have. A caller unstowing a body without that buffer gets a correctly initialized one. PhysicsVelocity is still a precondition: the API is for initialized moving craft, not arbitrary static grid roots. No assembly references, packages, timing, or payload signatures change. SPEC-10's required playback test checks both the entity group and the initialized values.
