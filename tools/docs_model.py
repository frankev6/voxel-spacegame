"""Shared parser for specs and the ledger (used by check-docs.py and ledger-sync.py)."""
import os
import re
from dataclasses import dataclass, field

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SPECS_DIR = os.path.join(ROOT, "docs", "specs")
LEDGER = os.path.join(ROOT, "docs", "LEDGER.md")

# Milestone ranks. The art track runs in parallel: A0 from the start, A1 alongside M3, A2 alongside M4.
# "SPEC-NN" dependency tokens expand to that spec's tasks whose rank <= the depending task's rank.
MILESTONE_RANK = {"M0": 0, "M1": 1, "M2": 2, "M3": 3, "M4": 4, "A0": 0, "A1": 3, "A2": 4}
MILESTONE_ORDER = ["M0", "M1", "M2", "M3", "M4", "A0", "A1", "A2"]
STATUSES = ["todo", "in-progress", "blocked", "review", "done", "dropped"]

# Task tiers (docs/04-workflow.md §7). "frontier" tasks are reserved for frontier-class models (Claude Opus / GPT Astra class)
# at their highest reasoning setting, and need an independent frontier review before `done`. "standard" tasks may be taken
# by standard-class models (Claude Sonnet / GPT Sol class) or better.
TIERS = ["frontier", "standard"]
# Model names that may NOT own a frontier task (matched case-insensitively against the "(model)" part of Owner).
NON_FRONTIER_MODEL_RE = re.compile(r"sonnet|haiku|\bsol\b|mini|nano|flash|lite|small", re.I)
OWNER_RE = re.compile(r"^(?P<who>[^()]+?)\s*\((?P<model>[^()]+)\)\s*$")

TASK_RE = re.compile(r"^###\s+([A-Z]{2,4}-\d{3})\s+[—-]+\s+(.+?)\s*$")
FIELD_RE = re.compile(r"^-\s+\*\*([A-Za-z ]+):\*\*\s*(.*)$")
SPEC_FILE_RE = re.compile(r"^SPEC-(\d{2})-[\w-]+\.md$")
DEP_TOKEN_RE = re.compile(r"\b([A-Z]{2,4}-\d{3}|SPEC-\d{2})\b")


@dataclass
class Task:
    id: str
    title: str
    spec: str            # "SPEC-10"
    spec_file: str
    line: int
    fields: dict = field(default_factory=dict)

    @property
    def milestone(self):
        return self.fields.get("Milestone", "").strip().split()[0] if self.fields.get("Milestone") else ""

    @property
    def tier(self):
        v = self.fields.get("Tier", "").strip()
        return v.split()[0].strip("*`").lower() if v else ""

    @property
    def deps_raw(self):
        return self.fields.get("Depends on", "")

    @property
    def deps(self):
        return DEP_TOKEN_RE.findall(self.deps_raw)

    @property
    def area(self):
        return self.id.split("-")[0]


@dataclass
class Spec:
    num: str             # "SPEC-10"
    path: str
    text: str
    header: dict
    tasks: list


def _header(text):
    head = {}
    for line in text.splitlines()[:30]:
        m = re.match(r"^\|\s*\*\*(.+?)\*\*\s*\|\s*(.*?)\s*\|\s*$", line)
        if m:
            head[m.group(1).strip()] = m.group(2).strip()
    return head


def load_specs():
    specs = []
    if not os.path.isdir(SPECS_DIR):
        return specs
    for name in sorted(os.listdir(SPECS_DIR)):
        m = SPEC_FILE_RE.match(name)
        if not m:
            continue
        path = os.path.join(SPECS_DIR, name)
        text = open(path, encoding="utf-8").read()
        num = f"SPEC-{m.group(1)}"
        tasks, cur, in_code = [], None, False
        for i, line in enumerate(text.splitlines(), 1):
            if line.strip().startswith("```"):
                in_code = not in_code
            if in_code:
                continue
            tm = TASK_RE.match(line)
            if tm:
                cur = Task(tm.group(1), tm.group(2), num, name, i)
                tasks.append(cur)
                continue
            if line.startswith("## "):
                cur = None
                continue
            if cur is not None:
                fm = FIELD_RE.match(line)
                if fm and fm.group(1) not in cur.fields:
                    cur.fields[fm.group(1)] = fm.group(2)
        specs.append(Spec(num, path, text, _header(text), tasks))
    return specs


def task_index(specs):
    idx = {}
    for s in specs:
        for t in s.tasks:
            idx.setdefault(t.id, []).append(t)
    return idx


def expand_deps(task, specs_by_num, idx):
    """Return the set of task IDs a task depends on, expanding SPEC-NN tokens by milestone rank."""
    out = set()
    rank = MILESTONE_RANK.get(task.milestone, 99)
    for tok in task.deps:
        if tok.startswith("SPEC-"):
            spec = specs_by_num.get(tok)
            if spec:
                for t in spec.tasks:
                    if MILESTONE_RANK.get(t.milestone, 99) <= rank and t.id != task.id:
                        out.add(t.id)
        elif tok in idx:
            out.add(tok)
    return out


# ---------------------------------------------------------------- ledger table

LEDGER_COLS = ["ID", "Title", "Spec", "Milestone", "Tier", "Depends on", "Ready", "Status", "Owner", "Updated", "Commit", "Notes"]
BEGIN, END = "<!-- tasks:begin -->", "<!-- tasks:end -->"
SUM_BEGIN, SUM_END = "<!-- summary:begin -->", "<!-- summary:end -->"


def _cells(line):
    parts = [c.strip() for c in line.strip().strip("|").split("|")]
    return parts


def load_ledger_rows():
    if not os.path.exists(LEDGER):
        return {}, None
    text = open(LEDGER, encoding="utf-8").read()
    if BEGIN not in text or END not in text:
        return {}, text
    block = text.split(BEGIN, 1)[1].split(END, 1)[0]
    rows = {}
    for line in block.splitlines():
        if not line.startswith("|") or line.startswith("|---") or line.startswith("| ID "):
            continue
        cells = _cells(line)
        if len(cells) < len(LEDGER_COLS):
            cells += [""] * (len(LEDGER_COLS) - len(cells))
        row = dict(zip(LEDGER_COLS, cells))
        rows[row["ID"]] = row
    return rows, text
