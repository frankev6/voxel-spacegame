# SPEC-NN — Title

| | |
|---|---|
| **Status** | Draft / Ready / In progress / Done |
| **Area code** | XXX (task IDs are `XXX-001`, `XXX-002`, …) |
| **Module(s)** | `Game.Xxx` (folder `Assets/Game/Xxx/`) |
| **Depends on specs** | SPEC-NN (what you need from it) |
| **Depended on by** | SPEC-NN (what they need from this) |
| **Milestone** | M1 / M2 / … (see `docs/ROADMAP.md`) |

> Read [01-architecture.md](../01-architecture.md), [02-conventions.md](../02-conventions.md) and [03-glossary.md](../03-glossary.md) before this spec. This spec may not contradict them.

## 1. Purpose

What this system does for the player and for other systems, in 3–6 sentences. What problem from the old prototype it avoids (one line, citing `docs/reference/audit-2026-10/` IDs if useful).

## 2. Scope

**In scope:** bullet list.

**Out of scope (and who owns it instead):** bullet list naming the other spec.

## 3. Decisions (binding)

Numbered list of the design choices a worker must not change. Each with a one-line reason. Example:
1. **Cells are 4 bytes.** Reason: memory at 1M cells.

## 4. Data model

Exact type definitions: components, buffers, blobs, managed classes, ScriptableObjects. Use C# code blocks with field types and comments. Mark each type *public contract* (other modules use it) or *internal*.

## 5. Systems, services and flow

For each system/service:
- name, group (from architecture §5), ordering constraints
- reads / writes (must match architecture §8 ownership)
- per-tick algorithm in numbered steps
- structural changes (if any) and which ECB

Include a sequence or flow diagram when more than three parts interact.

## 6. Public API

Signatures other modules call, with pre/post-conditions and error behaviour.

## 7. Edge cases and failure behaviour

Table: situation → required behaviour.

## 8. Performance budget

Numbers this system must meet (from [06-performance-budgets.md](../06-performance-budgets.md)) and how they are measured.

## 9. Tasks

Each task is small (0.5–3 days), independently reviewable, and ordered so that each depends only on earlier tasks or other specs' tasks listed.

**Dependency tokens** allowed in *Depends on*:
- a task ID from this spec (`VOX-004`);
- another spec's explicit task ID when runtime behavior is required ([ADR-0068](../decisions/ADR-0068-explicit-functional-dependencies.md)); retain every real functional prerequisite, but do not gate gameplay on advisory measurements or owner feel sign-off;
- another spec's **contract task** `<CODE>-001` — in every spec, task `-001` is *"Public contract types"*: it creates all public types of that spec (components, structs, enums, interfaces, definition classes) with no behaviour, so other modules can compile against them early;
- a whole spec, `SPEC-NN`, meaning "every task of SPEC-NN whose milestone is the same as or earlier than this task's milestone".

**Tier** (see [04-workflow.md §7](../04-workflow.md#7-task-tiers-which-model-may-take-which-task)): `frontier` = reserved for frontier-class models (Claude Opus / GPT Astra class, highest reasoning setting) and needs an independent frontier review; `standard` = standard-class models (Claude Sonnet / GPT Sol class) may take it. Write the tier, then one short reason after a dash.

Task blocks are machine-read by `tools/ledger-sync.py` and `tools/check-docs.py`. Keep this exact shape: heading `### <ID> — <title>`, then the bullet lines **Milestone**, **Tier**, **Depends on** (comma-separated task IDs in backticks, or `—`), **Do**, **Files**, **Acceptance criteria**, **Tests**.

### XXX-001 — Short imperative title
- **Milestone:** M1
- **Tier:** standard — transcribes the types in §4 exactly
- **Depends on:** — / `YYY-00N`, `XXX-000`
- **Do:** precise steps.
- **Files:** paths to create or change.
- **Acceptance criteria:**
  - [ ] observable, testable statements
- **Tests:** test class/method names to add and what they assert.

### XXX-002 — …

## 10. Open questions

| # | Question | Default if unanswered | Decider |
|---|---|---|---|
| 1 | … | The default a worker uses until answered | Owner / tech lead |

## 11. References

Audit sections, Unity docs, prior art. Background only.
