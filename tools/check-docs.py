#!/usr/bin/env python3
"""Validate specs and the ledger so parallel workers can't drift. Exit 1 on any error.

Checks:
- every spec has the header table and the 11 template sections;
- task IDs are unique and use the spec's area code; task -001 exists ("Public contract types");
- every task has Milestone (valid), Tier (valid), Depends on, Do, Files, Acceptance criteria, Tests;
- frontier-tier tasks are owned only by frontier-class models (per-task reviews were replaced by system reviews, ADR-0019);
- dependency tokens are valid: own task IDs, another spec's <CODE>-001, or SPEC-NN;
- no task depends on a task scheduled in a later milestone; no dependency cycles;
- the ledger has exactly one row per spec task (run tools/ledger-sync.py to fix), valid statuses;
- every spec listed in docs/01-architecture.md §10 exists.
"""
import os
import re
import sys
from docs_model import (ROOT, MILESTONE_RANK, STATUSES, TIERS, NON_FRONTIER_MODEL_RE, OWNER_RE,
                        load_specs, task_index, expand_deps, load_ledger_rows)

REQUIRED_HEADER = ["Status", "Area code", "Module(s)", "Depends on specs", "Milestone"]
REQUIRED_SECTIONS = ["1. Purpose", "2. Scope", "3. Decisions", "4. Data model", "5. Systems", "6. Public API",
                     "7. Edge cases", "8. Performance", "9. Tasks", "10. Open questions", "11. References"]
REQUIRED_FIELDS = ["Milestone", "Tier", "Depends on", "Do", "Files", "Acceptance criteria", "Tests"]


def main():
    errors, warnings = [], []
    specs = load_specs()
    by_num = {s.num: s for s in specs}
    idx = task_index(specs)

    for s in specs:
        rel = os.path.relpath(s.path, ROOT)
        for h in REQUIRED_HEADER:
            if h not in s.header:
                errors.append(f"{rel}: header table missing **{h}**")
        heads = [l[3:].strip() for l in s.text.splitlines() if l.startswith("## ")]
        for sec in REQUIRED_SECTIONS:
            if not any(h.startswith(sec) for h in heads):
                errors.append(f"{rel}: missing section '## {sec}…'")
        codes = re.findall(r"[A-Z]{2,4}", s.header.get("Area code", ""))
        if not s.tasks:
            errors.append(f"{rel}: no tasks found (heading format '### ABC-001 — Title')")
        if s.tasks and not any(t.id.endswith("-001") for t in s.tasks):
            errors.append(f"{rel}: no -001 'Public contract types' task")
        for t in s.tasks:
            where = f"{rel}:{t.line} {t.id}"
            if codes and t.area not in codes:
                errors.append(f"{where}: area code {t.area} not in spec header {codes}")
            for f in REQUIRED_FIELDS:
                if f not in t.fields:
                    errors.append(f"{where}: missing '- **{f}:**' line")
            if "Tier" in t.fields and t.tier not in TIERS:
                errors.append(f"{where}: invalid tier '{t.fields.get('Tier')}' (use {' or '.join(TIERS)})")
            if t.milestone not in MILESTONE_RANK:
                errors.append(f"{where}: invalid milestone '{t.milestone}' (use {', '.join(MILESTONE_RANK)})")
            for tok in t.deps:
                if tok.startswith("SPEC-"):
                    if tok not in by_num:
                        errors.append(f"{where}: depends on unknown {tok}")
                    continue
                if tok not in idx:
                    errors.append(f"{where}: depends on unknown task {tok}")
                    continue
                dep = idx[tok][0]
                if dep.spec != t.spec and not tok.endswith("-001"):
                    errors.append(f"{where}: cross-spec dependency {tok} must be {tok[:-3]}001 or {dep.spec}")
                if MILESTONE_RANK.get(dep.milestone, 99) > MILESTONE_RANK.get(t.milestone, 99):
                    errors.append(f"{where} ({t.milestone}) depends on {tok} scheduled later ({dep.milestone})")

    for tid, ts in idx.items():
        if len(ts) > 1:
            errors.append(f"duplicate task ID {tid} in " + ", ".join(f"{t.spec_file}:{t.line}" for t in ts))

    # cycles (with SPEC-NN expansion)
    graph = {tid: expand_deps(ts[0], by_num, idx) for tid, ts in idx.items()}
    state = {}

    def visit(n, stack):
        state[n] = 1
        for d in graph.get(n, ()):
            if state.get(d) == 1:
                cyc = stack[stack.index(d):] + [d] if d in stack else [n, d]
                errors.append("dependency cycle: " + " -> ".join(cyc))
            elif state.get(d) is None:
                visit(d, stack + [d])
        state[n] = 2

    sys.setrecursionlimit(10000)
    for n in sorted(graph):
        if state.get(n) is None:
            visit(n, [n])

    # ledger
    rows, text = load_ledger_rows()
    if text is None:
        errors.append("docs/LEDGER.md missing")
    else:
        for tid in idx:
            if tid not in rows:
                errors.append(f"ledger: no row for {tid} (run tools/ledger-sync.py)")
        for tid, r in rows.items():
            if tid not in idx:
                warnings.append(f"ledger: {tid} is not defined in any spec")
            if r.get("Status") not in STATUSES:
                errors.append(f"ledger: {tid} has invalid status '{r.get('Status')}' (use {', '.join(STATUSES)})")
            if r.get("Status") == "done" and not r.get("Commit"):
                errors.append(f"ledger: {tid} is done but has no commit hash")
            st, owner = r.get("Status"), (r.get("Owner") or "").strip()
            if st in ("in-progress", "review", "done") and owner:
                m = OWNER_RE.match(owner)
                if not m:
                    errors.append(f"ledger: {tid} owner '{owner}' must be 'name (model)', e.g. 'alex (claude-opus-5-5)' or 'sam (human)'")
                elif tid in idx and idx[tid][0].tier == "frontier" and NON_FRONTIER_MODEL_RE.search(m.group("model")):
                    errors.append(f"ledger: {tid} is a frontier task but is owned by a non-frontier model '{m.group('model')}' (04-workflow §7)")

    # spec map
    arch = open(os.path.join(ROOT, "docs", "01-architecture.md"), encoding="utf-8").read()
    for path in sorted(set(re.findall(r"\(specs/(SPEC-\d{2}-[\w-]+\.md)\)", arch))):
        if not os.path.exists(os.path.join(ROOT, "docs", "specs", path)):
            errors.append(f"architecture §10 links missing spec docs/specs/{path}")

    # every system/group in the architecture pipeline (§5) is specified by the spec whose code is next to it
    code_to_spec = {}
    for s in specs:
        for c in re.findall(r"[A-Z]{2,4}", s.header.get("Area code", "")):
            code_to_spec.setdefault(c, s)
    pipeline = arch.split("## 5. Frame pipeline", 1)[-1].split("### 5.1", 1)[0]
    for name, code in re.findall(r"[├└]─\s+(\w+(?:System|Group))\s+([A-Z]{2,4})\b", pipeline):
        spec = code_to_spec.get(code)
        if spec is None:
            warnings.append(f"pipeline: {name} belongs to {code} but no spec has that area code")
        elif name not in spec.text:
            errors.append(f"pipeline: {name} ({code}) is not specified in {os.path.relpath(spec.path, ROOT)}")

    for w in warnings:
        print("WARN ", w)
    for e in errors:
        print("ERROR", e)
    total = sum(len(s.tasks) for s in specs)
    print(f"check-docs: {len(specs)} specs, {total} tasks, {len(errors)} errors, {len(warnings)} warnings")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
