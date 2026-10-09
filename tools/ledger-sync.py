#!/usr/bin/env python3
"""Sync docs/LEDGER.md task rows with the specs.

- Adds a row (status `todo`) for every task found in docs/specs/SPEC-*.md.
- Refreshes Title / Spec / Milestone / Tier / Depends on from the spec (the spec is the source of truth for definitions).
- Recomputes Ready (= every dependency is `done`) and the milestone summary.
- Never deletes rows and never touches Status / Owner / Updated / Commit / Notes, except marking orphans in Notes.
"""
import sys
from docs_model import (LEDGER, LEDGER_COLS, BEGIN, END, SUM_BEGIN, SUM_END, MILESTONE_ORDER, STATUSES,
                        load_specs, task_index, expand_deps, load_ledger_rows)


def esc(s):
    return (s or "").replace("|", "\\|").strip()


def main():
    specs = load_specs()
    by_num = {s.num: s for s in specs}
    idx = task_index(specs)
    rows, text = load_ledger_rows()
    if text is None or BEGIN not in text:
        print(f"{LEDGER} is missing the {BEGIN} / {END} markers"); return 1

    added = 0
    for s in specs:
        for t in s.tasks:
            r = rows.get(t.id)
            if r is None:
                r = {c: "" for c in LEDGER_COLS}
                r["Status"] = "todo"
                rows[t.id] = r
                added += 1
            r["ID"], r["Title"], r["Spec"] = t.id, esc(t.title), f"[{s.num}](specs/{t.spec_file})"
            r["Milestone"] = t.milestone
            r["Tier"] = t.tier
            r["Depends on"] = ", ".join(t.deps) if t.deps else "—"
            if r.get("Notes", "").startswith("ORPHAN"):
                r["Notes"] = ""

    status = {k: v.get("Status", "todo") for k, v in rows.items()}
    for k, r in rows.items():
        if k not in idx:
            if not r.get("Notes", "").startswith("ORPHAN"):
                r["Notes"] = "ORPHAN: not in any spec " + r.get("Notes", "")
            r["Ready"] = ""
            continue
        t = idx[k][0]
        deps = expand_deps(t, by_num, idx)
        r["Ready"] = "yes" if all(status.get(d) in ("done", "dropped") for d in deps) else "no"

    def key(item):
        k, r = item
        m = r.get("Milestone", "")
        mi = MILESTONE_ORDER.index(m) if m in MILESTONE_ORDER else 99
        spec = idx[k][0].spec if k in idx else "SPEC-99"
        return (mi, spec, k)

    lines = ["| " + " | ".join(LEDGER_COLS) + " |", "|" + "---|" * len(LEDGER_COLS)]
    for k, r in sorted(rows.items(), key=key):
        lines.append("| " + " | ".join(r.get(c, "") or "" for c in LEDGER_COLS) + " |")
    table = "\n".join(lines)

    summary = ["| Milestone | " + " | ".join(STATUSES) + " | total | ready to start |", "|---|" + "---|" * (len(STATUSES) + 2)]
    for m in MILESTONE_ORDER:
        ms = [r for r in rows.values() if r.get("Milestone") == m]
        if not ms:
            continue
        counts = [sum(1 for r in ms if r.get("Status") == s) for s in STATUSES]
        ready = sum(1 for r in ms if r.get("Status") == "todo" and r.get("Ready") == "yes")
        summary.append(f"| {m} | " + " | ".join(str(c) for c in counts) + f" | {len(ms)} | {ready} |")

    before, rest = text.split(BEGIN, 1)
    _, after = rest.split(END, 1)
    text = before + BEGIN + "\n" + table + "\n" + END + after
    if SUM_BEGIN in text and SUM_END in text:
        b, rest = text.split(SUM_BEGIN, 1)
        _, a = rest.split(SUM_END, 1)
        text = b + SUM_BEGIN + "\n" + "\n".join(summary) + "\n" + SUM_END + a
    open(LEDGER, "w", encoding="utf-8").write(text)
    print(f"ledger: {len(rows)} rows ({added} added) from {len(specs)} specs")
    return 0


if __name__ == "__main__":
    sys.exit(main())
