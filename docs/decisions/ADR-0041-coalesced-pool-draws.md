# ADR-0041 — Coalesce visible pooled index ranges

**Status:** Accepted — 2026-10-11, Tech lead (astra, gpt-6), under Frank's autonomous technical-decision instruction. Adoption remains subject to REN-003's unchanged gates.

## Evidence

ADR-0032's one-command-per-brick implementation passes pixel parity, growth, retirement and the reference memory check. On Metal, its 2,500-command workload is too expensive: a 60-sample diagnostic after constant-buffer isolation measured GPU median **22.000125 ms**; raw profiler data showed a **29.35204 ms** render-thread root. Inclusive adapter/transform CPU was 0.119400 ms, but that does not cover native render-thread submission. These are diagnostic results, not a passed 300-frame gate. A thread sample shows substantial constant-buffer copying and per-command driver work. Splitting the indirect command ID out of URP globals did not meet the GPU gate.

## Decision

Keep the same pool, exact range allocator, eight-byte CPU packed vertices, generational handles, three-frame retirement, culling and workload. Replace per-brick commands with commands for contiguous visible index ranges:

- GPU indices are rebased to global vertex indices during the upload compute copy. All indirect commands have base vertex and base instance zero.
- A separate raw uint owner buffer has one brick slot per reserved vertex. The upload compute shader fills owners; growth copies them and retires old buffers alongside geometry. The vertex shader reads owner → world matrix, then the existing eight-byte vertex data. It no longer needs command ID, instance-base helpers or a per-slot vertex-start buffer. The CPU packed format remains unchanged; GPU ownership adds four bytes per reserved vertex.
- The Burst cull job collects selected index intervals, sorts by start, and coalesces adjacent intervals. It writes one indexed argument per resulting run. Gaps and rejected bricks are never drawn; retained capacity beyond an applied index count is not part of an interval. Each selected brick still uses its own matrix and surface data.
- Expose both visible-brick count and actual command count. One API submission per material remains. Fragmentation may require multiple commands; no instance-count or geometry simplification makes the reference workload easier.
- Account owner buffers, retired owner buffers and the native interval list in the same unchanged 36 MB memory gate. Default owners add 2,621,440 bytes. No CPU geometry mirror or second full index pool is introduced.

This supersedes ADR-0032's per-brick command and per-slot vertex-start details and SPEC-11 §5.10. REN-010/011 retain the same handle/upload/cleanup integration. Functional Entities Graphics is unchanged.

## Validation

Rerun the three prescribed REN-003 tests, including actual reference-station meshes, pixel parity through growth and slot reuse, four-cascade 2,500-brick rendering, moving parent and 128-brick rewrites. Collect 300 samples for final timing gates. Neither fewer native commands nor this accepted contract constitutes a performance pass by itself.
