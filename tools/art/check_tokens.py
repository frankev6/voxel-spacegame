#!/usr/bin/env python3
"""Check UI token sheets against DESIGN-SYSTEM §10.1–§10.3 and §11 (SPEC-80 ART-002). Exit 1 on any error.

Python 3 standard library only (SPEC-80 §4.6).

  check_tokens.py                       checks Assets/Game/UI/Theme/tokens.uss and every Directions/tokens-*.uss
  check_tokens.py --sheet PATH [...]    checks the given sheets only
  check_tokens.py --verify-doc          also compares the sheets and the computed tables with DESIGN-SYSTEM.md

Rules (DESIGN-SYSTEM):
- every binding token of §10.1 is defined exactly once in each sheet's :root; direction sheets also define every §11 token;
- T1: --color-text, --color-text-muted, --color-accent and the six semantic colours >= 4.5:1 on --color-bg, -surface-1, -surface-2
  (T3: semantic colours are not text colours on --color-surface-3, so that surface is not measured);
- T2: --color-border-strong >= 3:1 on --color-surface-1..3 (sheets that define it);
- C4: minimum CIEDE2000 over the normal view and the Machado-2009 protan/deutan/tritan simulations (severity 1.0, applied in
  linear RGB, clamped to [0, 1]): ally-enemy >= 30, ally-neutral and enemy-neutral >= 20, hull-shield-energy pairs >= 12,
  positive-danger >= 10.
Contrast is WCAG 2.x. Lab uses the sRGB/D65 matrices. Values are compared unrounded; tables print 2 decimals (contrast) and
1 decimal (ΔE) like DESIGN-SYSTEM §8.1 and §10.2.
"""
import argparse
import glob
import math
import os
import re
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DESIGN_SYSTEM = os.path.join(ROOT, "docs", "design", "DESIGN-SYSTEM.md")
THEME = os.path.join(ROOT, "Assets", "Game", "UI", "Theme")

T1_TOKENS = ["--color-text", "--color-text-muted", "--color-accent", "--color-positive", "--color-warning",
             "--color-danger", "--color-info", "--color-ally", "--color-enemy"]
T1_BACKGROUNDS = ["--color-bg", "--color-surface-1", "--color-surface-2"]
T1_MIN = 4.5
T2_TOKEN, T2_BACKGROUNDS, T2_MIN = "--color-border-strong", ["--color-surface-1", "--color-surface-2", "--color-surface-3"], 3.0
C4_PAIRS = [("ally", "enemy", 30.0), ("ally", "neutral", 20.0), ("enemy", "neutral", 20.0), ("hull", "shield", 12.0),
            ("hull", "energy", 12.0), ("shield", "energy", 12.0), ("positive", "danger", 10.0)]

# Machado, Oliveira, Fernandes (2009), severity 1.0, for linear RGB.
MACHADO = {
    "protan": ((0.152286, 1.052583, -0.204868), (0.114503, 0.786281, 0.099216), (-0.003882, -0.048116, 1.051998)),
    "deutan": ((0.367322, 0.860646, -0.227968), (0.280085, 0.672501, 0.047413), (-0.011820, 0.042940, 0.968881)),
    "tritan": ((1.255528, -0.076749, -0.178779), (-0.078411, 0.930809, 0.147602), (0.004733, 0.691367, 0.303900)),
}


# ------------------------------------------------------------------ colour maths
def parse_hex(value):
    m = re.fullmatch(r"#([0-9a-fA-F]{6})", value.strip())
    if not m:
        raise ValueError(f"not a #RRGGBB colour: {value!r}")
    h = m.group(1)
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def relative_luminance(rgb):
    r, g, b = (to_linear(c) for c in rgb)
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def contrast(a, b):
    hi, lo = sorted((relative_luminance(a), relative_luminance(b)), reverse=True)
    return (hi + 0.05) / (lo + 0.05)


def simulate_linear(rgb, matrix):
    """Machado simulation of an sRGB colour; returns linear RGB clamped to [0, 1]."""
    lin = [to_linear(c) for c in rgb]
    return [min(1.0, max(0.0, sum(matrix[i][j] * lin[j] for j in range(3)))) for i in range(3)]


def lab_from_linear(lin):
    x = 0.4124564 * lin[0] + 0.3575761 * lin[1] + 0.1804375 * lin[2]
    y = 0.2126729 * lin[0] + 0.7151522 * lin[1] + 0.0721750 * lin[2]
    z = 0.0193339 * lin[0] + 0.1191920 * lin[1] + 0.9503041 * lin[2]

    def f(t):
        return t ** (1.0 / 3.0) if t > 216.0 / 24389.0 else (24389.0 / 27.0 * t + 16.0) / 116.0

    fx, fy, fz = f(x / 0.95047), f(y / 1.0), f(z / 1.08883)
    return (116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz))


def ciede2000(lab1, lab2):
    """CIEDE2000 (Sharma, Wu, Dalal 2005), kL = kC = kH = 1."""
    l1, a1, b1 = lab1
    l2, a2, b2 = lab2
    c_bar = (math.hypot(a1, b1) + math.hypot(a2, b2)) / 2.0
    g = 0.5 * (1.0 - math.sqrt(c_bar ** 7 / (c_bar ** 7 + 25.0 ** 7)))
    a1p, a2p = (1.0 + g) * a1, (1.0 + g) * a2
    c1p, c2p = math.hypot(a1p, b1), math.hypot(a2p, b2)
    h1p = math.degrees(math.atan2(b1, a1p)) % 360.0 if c1p else 0.0
    h2p = math.degrees(math.atan2(b2, a2p)) % 360.0 if c2p else 0.0
    dlp, dcp = l2 - l1, c2p - c1p
    if c1p * c2p == 0:
        dhp = 0.0
    else:
        dhp = h2p - h1p
        if dhp > 180.0:
            dhp -= 360.0
        elif dhp < -180.0:
            dhp += 360.0
    d_hp = 2.0 * math.sqrt(c1p * c2p) * math.sin(math.radians(dhp / 2.0))
    lbp, cbp = (l1 + l2) / 2.0, (c1p + c2p) / 2.0
    if c1p * c2p == 0:
        hbp = h1p + h2p
    elif abs(h1p - h2p) <= 180.0:
        hbp = (h1p + h2p) / 2.0
    elif h1p + h2p < 360.0:
        hbp = (h1p + h2p + 360.0) / 2.0
    else:
        hbp = (h1p + h2p - 360.0) / 2.0
    t = (1.0 - 0.17 * math.cos(math.radians(hbp - 30.0)) + 0.24 * math.cos(math.radians(2.0 * hbp))
         + 0.32 * math.cos(math.radians(3.0 * hbp + 6.0)) - 0.20 * math.cos(math.radians(4.0 * hbp - 63.0)))
    d_theta = 30.0 * math.exp(-(((hbp - 275.0) / 25.0) ** 2))
    rc = 2.0 * math.sqrt(cbp ** 7 / (cbp ** 7 + 25.0 ** 7))
    sl = 1.0 + 0.015 * (lbp - 50.0) ** 2 / math.sqrt(20.0 + (lbp - 50.0) ** 2)
    sc = 1.0 + 0.045 * cbp
    sh = 1.0 + 0.015 * cbp * t
    rt = -math.sin(math.radians(2.0 * d_theta)) * rc
    return math.sqrt((dlp / sl) ** 2 + (dcp / sc) ** 2 + (d_hp / sh) ** 2 + rt * (dcp / sc) * (d_hp / sh))


def min_delta_e(a, b):
    """Minimum CIEDE2000 between two sRGB colours over the normal view and the three simulations (rule C4)."""
    values = [ciede2000(lab_from_linear([to_linear(c) for c in a]), lab_from_linear([to_linear(c) for c in b]))]
    for matrix in MACHADO.values():
        values.append(ciede2000(lab_from_linear(simulate_linear(a, matrix)), lab_from_linear(simulate_linear(b, matrix))))
    return min(values)


# ------------------------------------------------------------------ sheets
def parse_sheet(path):
    """Return ({token: value}, [duplicate token names]) from the sheet's :root block."""
    with open(path, encoding="utf-8") as f:
        text = re.sub(r"/\*.*?\*/", "", f.read(), flags=re.S)
    m = re.search(r":root\s*\{([^}]*)\}", text)
    if not m:
        return {}, []
    values, duplicates = {}, []
    for decl in m.group(1).split(";"):
        if ":" not in decl:
            continue
        name, value = decl.split(":", 1)
        name = name.strip()
        if not name.startswith("--"):
            continue
        if name in values:
            duplicates.append(name)
        values[name] = value.strip()
    return values, duplicates


def column_of(path):
    """DESIGN-SYSTEM column name for a sheet: tokens.uss → Placeholder, tokens-pulp-horizon.uss → PULP HORIZON."""
    name = os.path.basename(path)
    if name == "tokens.uss":
        return "Placeholder"
    m = re.fullmatch(r"tokens-([a-z0-9-]+)\.uss", name)
    return m.group(1).replace("-", " ").upper() if m else None


# ------------------------------------------------------------------ DESIGN-SYSTEM parsing
def read_doc():
    with open(DESIGN_SYSTEM, encoding="utf-8") as f:
        return f.read()


def section(doc, heading):
    """Text from the line starting with `heading` to the next heading of the same or higher level."""
    lines = doc.splitlines()
    for i, line in enumerate(lines):
        if line.startswith(heading):
            level = len(line) - len(line.lstrip("#"))
            out = []
            for nxt in lines[i + 1:]:
                if nxt.startswith("#") and len(nxt) - len(nxt.lstrip("#")) <= level:
                    break
                out.append(nxt)
            return "\n".join(out)
    raise SystemExit(f"DESIGN-SYSTEM.md: heading '{heading}' not found")


def tables(text):
    """Every markdown table in text as a list of rows (lists of stripped cells), header row first, separator dropped."""
    result, current = [], []
    for line in text.splitlines():
        if line.startswith("|"):
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            if not all(re.fullmatch(r":?-+:?", c) for c in cells):
                current.append(cells)
        elif current:
            result.append(current)
            current = []
    if current:
        result.append(current)
    return result


def expand_names(cell):
    """`--radius-s` / `-m` / `-l` → [--radius-s, --radius-m, --radius-l]; `--space-1` … `--space-8` → 8 names."""
    cell = cell.replace("`", "")
    m = re.fullmatch(r"(--[a-z-]+?)(\d+)\s*…\s*\1(\d+)", cell.strip())
    if m:
        return [f"{m.group(1)}{i}" for i in range(int(m.group(2)), int(m.group(3)) + 1)]
    names = []
    for part in (p.strip() for p in cell.split("/")):
        if part.startswith("--"):
            names.append(part)
        elif part.startswith("-") and names:
            names.append(names[0].rsplit("-", 1)[0] + part)
    return names


def binding_tokens(doc):
    sec = section(doc, "### 10.1 Token contract")
    line = next(l for l in sec.splitlines() if "**Binding names.**" in l)
    line = line.split("no other file defines them:", 1)[1].split("They are never renamed", 1)[0]
    line = re.sub(r"`(--[a-z-]+?)(\d+)`\s*…\s*`\1(\d+)`",
                  lambda m: " ".join(f"`{m.group(1)}{i}`" for i in range(int(m.group(2)), int(m.group(3)) + 1)), line)
    return re.findall(r"`(--[a-z0-9-]+)`", line)


def proposed_tokens(doc):
    rows = tables(section(doc, "## 11. Proposed additional tokens"))[0]
    names = []
    for row in rows[1:]:
        names.extend(expand_names(row[0]))
    return names


def doc_value_cells(cell):
    """A DESIGN-SYSTEM value cell → list of normalised values ("12 px" → "12px", "2 / 4 / 8 px" → three values)."""
    text = re.sub(r"\s*\((?:in |white-out|black-out|sky-key|shared).*?\)", "", cell).replace("`", "").strip()
    unit = re.search(r"\s(px|ms)$", text)
    if unit and re.fullmatch(r"[\d.,/\s]+(px|ms)", text):
        parts = re.split(r"\s*[,/]\s*", text[: unit.start()].strip())
        return [p + unit.group(1) for p in parts]
    return [text]


def normalise(value):
    m = re.search(r'url\("project://database/[^"]*/([^"/]+)"\)', value)
    if m:
        return m.group(1).lower()
    return re.sub(r"\s+", "", value).lower()


def doc_expected_values(doc):
    """{column: {token: value}} from the §8.1 value table and the §11 table."""
    expected = {}
    for table, first_value_col in ((tables(section(doc, "### 8.1 UI token values"))[0], 1),
                                   (tables(section(doc, "## 11. Proposed additional tokens"))[0], 2)):
        header = [re.sub(r"\s*\(.*\)", "", h) for h in table[0]]
        columns = header[first_value_col:]
        for row in table[1:]:
            names = expand_names(row[0])
            previous = None
            for col, cell in zip(columns, row[first_value_col:]):
                values = previous if cell.startswith("same") else doc_value_cells(cell)
                previous = values
                if len(values) != len(names):
                    continue                                     # not a value row this checker can compare
                for name, value in zip(names, values):
                    expected.setdefault(col, {})[name] = value
    return expected


def doc_contrast_table(doc):
    table = tables(section(doc, "### 8.1 UI token values"))[1]
    cols = table[0][1:]
    return {(row[0].strip("`"), col): cell for row in table[1:] for col, cell in zip(cols, row[1:])}


def doc_delta_e_table(doc):
    table = tables(section(doc, "### 10.2 Colour-blind safety"))[0]
    cols = table[0][1:]
    return {(row[0].replace("–", "-"), col): cell for row in table[1:] for col, cell in zip(cols, row[1:])}


# ------------------------------------------------------------------ main check
def check(paths, verify_doc):
    doc = read_doc()
    binding, proposed = binding_tokens(doc), proposed_tokens(doc)
    errors, contrast_rows, delta_rows = [], {}, {}
    for path in paths:
        rel = os.path.relpath(path, ROOT)
        values, duplicates = parse_sheet(path)
        for name in duplicates:
            errors.append(f"{rel}: {name} is defined more than once")
        required = binding + ([] if os.path.basename(path) == "tokens.uss" else proposed)
        missing = [t for t in required if t not in values]
        for name in missing:
            errors.append(f"{rel}: missing token {name}")
        col = column_of(path) or rel
        try:
            colour = {t: parse_hex(values[t]) for t in set(T1_TOKENS + T1_BACKGROUNDS + T2_BACKGROUNDS + [T2_TOKEN] +
                                                         [f"--color-{n}" for p in C4_PAIRS for n in p[:2]]) if t in values}
        except ValueError as e:
            errors.append(f"{rel}: {e}")
            continue
        for t in T1_TOKENS:
            if t in colour and all(b in colour for b in T1_BACKGROUNDS):
                worst = min(contrast(colour[t], colour[b]) for b in T1_BACKGROUNDS)
                contrast_rows.setdefault(t, {})[col] = worst
                if worst < T1_MIN:
                    errors.append(f"{rel}: T1 {t} worst contrast {worst:.2f} < {T1_MIN} on bg/surface-1/surface-2")
        if T2_TOKEN in colour:
            worst = min(contrast(colour[T2_TOKEN], colour[b]) for b in T2_BACKGROUNDS if b in colour)
            contrast_rows.setdefault(T2_TOKEN + " (T2)", {})[col] = worst
            if worst < T2_MIN:
                errors.append(f"{rel}: T2 {T2_TOKEN} worst contrast {worst:.2f} < {T2_MIN} on surface-1..3")
        for a, b, minimum in C4_PAIRS:
            ta, tb = f"--color-{a}", f"--color-{b}"
            if ta in colour and tb in colour:
                de = min_delta_e(colour[ta], colour[tb])
                delta_rows.setdefault(f"{a}-{b}", {})[col] = de
                if de < minimum:
                    errors.append(f"{rel}: C4 {a}-{b} minimum ΔE2000 {de:.2f} < {minimum:g}")
        if verify_doc:
            expected = doc_expected_values(doc).get(col)
            if expected is None:
                errors.append(f"{rel}: no DESIGN-SYSTEM column '{col}' to verify against")
            else:
                for name, want in expected.items():
                    if name in values and normalise(values[name]) != normalise(want):
                        errors.append(f"{rel}: {name} is '{values[name]}', DESIGN-SYSTEM says '{want}'")

    if verify_doc:
        doc_contrast, doc_de = doc_contrast_table(doc), doc_delta_e_table(doc)
        for (token, col), want in doc_contrast.items():
            got = contrast_rows.get(token, {}).get(col)
            if got is not None and f"{got:.2f}" != want:
                errors.append(f"DESIGN-SYSTEM §8.1 contrast {token} {col}: document {want}, computed {got:.2f}")
        for (pair, col), want in doc_de.items():
            got = delta_rows.get(pair, {}).get(col)
            if got is not None and f"{got:.1f}" != want:
                errors.append(f"DESIGN-SYSTEM §10.2 ΔE {pair} {col}: document {want}, computed {got:.1f}")
    return errors, contrast_rows, delta_rows


def print_table(title, rows, fmt):
    cols = []
    for r in rows.values():
        cols += [c for c in r if c not in cols]
    print(f"\n{title}")
    print("| " + " | ".join(["Token"] + cols) + " |")
    for name, r in rows.items():
        print("| " + " | ".join([name] + [format(r[c], fmt) if c in r else "—" for c in cols]) + " |")


def default_sheets():
    return [os.path.join(THEME, "tokens.uss")] + sorted(glob.glob(os.path.join(THEME, "Directions", "tokens-*.uss")))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--sheet", action="append", help="sheet to check (repeatable); default: tokens.uss and Directions/")
    ap.add_argument("--verify-doc", action="store_true", help="compare values and tables with DESIGN-SYSTEM.md")
    args = ap.parse_args(argv)
    start = time.perf_counter()
    paths = [os.path.abspath(p) for p in args.sheet] if args.sheet else default_sheets()
    errors, contrast_rows, delta_rows = check(paths, args.verify_doc)
    print_table("Contrast, worst case on bg / surface-1 / surface-2 (T2 row: surface-1..3)", contrast_rows, ".2f")
    print_table("Minimum ΔE2000 over normal + protan/deutan/tritan (C4)", delta_rows, ".1f")
    print()
    for e in errors:
        print("ERROR", e)
    print(f"check_tokens: {len(paths)} sheets, {len(errors)} errors ({time.perf_counter() - start:.2f} s)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
