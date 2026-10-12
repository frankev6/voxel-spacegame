# ADR-0051 — Development command recording and replay

**Status:** Accepted (Tech lead, 2026-10-12)

## Context

SPEC-71 explicitly requires a managed replay system to overwrite PlayerCommand after the input sampler, but architecture §6 still names the sampler as its only writer and the managed replay/record systems are absent from the pipeline. The text format deliberately omits FrameIndex and does not describe how programmatically built scripts obtain their readonly endpoints.

## Decision

- In Editor/development builds only, GameInitGroup runs PlayerInputSampleSystem, then PlayerCommandReplaySystem, then PlayerCommandRecordSystem. Add the two managed App systems to the architecture pipeline. Replay is the explicit diagnostic override of the sampled PlayerCommand; simulation still consumes it exclusively through PlayerCommandApplySystem. Release builds contain neither hook.
- Keep SPEC-71 §5.4's frame-stage scheduling. The prescribed 30/60/144 FPS comparison uses scripts whose actual command changes occur at even ticks. ChangesOnlyAtEvenTicks exposes that restriction; do not claim arbitrary catch-up schedules or odd-tick scripts are frame-rate invariant through this frame-stage hook.
- FrameIndex is transient sample metadata: omit it from files and command-change comparisons; replay supplies the current frame index. Text round trips compare all serialized fields bit-exactly. Preserve optional seed/content header values.
- A new programmatic script starts with its first Set tick; Set extends EndTick to at least tick + 1 even when the held command is unchanged. Same-tick Set replaces that tick's value. Recorder Start fixes the starting tick from SimClock, records the current command, and Stop fixes the exclusive ending tick from SimClock; a last sample at that exclusive endpoint is discarded. At returns the last command at or before its argument, or default before the first record.
- The recorder observes the effective command after replay. Hook instances belong to the world, with no static active recorder; world destruction detaches the recorder. No new assemblies or production input behavior.

## Consequences

Development replays have an explicit place in the single-writer rules rather than an undocumented exception. The supplied even-tick test scenarios can be reproduced across the specified frame rates. Per-fixed-tick replay for arbitrary tick changes is not claimed by this implementation. Public format/API names remain those in SPEC-71.
