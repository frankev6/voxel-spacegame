#!/usr/bin/env python3
"""Check SPEC-80 design deliverables against their task's acceptance criteria. Exit 1 on any error.

Python 3 standard library only (SPEC-80 §4.6). Subcommands are added by the task that owns the deliverable:

  moodboards   ART-003: docs/design/moodboards/<direction>.md and img/<direction>/
               - the six reference groups are present, each with >= 2 references, 16-24 in total, plus >= 3 anti-references;
               - every reference has a title, Creator, Year, a one-sentence Take and a Link (one http(s) URL, or a
                 markdown link to an existing repository file such as an audit screenshot);
               - every visual group names at least one touchstone from the direction's DESIGN-SYSTEM §x.2 list;
               - every image is a JPEG in img/<direction>/, long side <= 1600 px, <= 400 KB, IJG quality 85 when
                 detectable, at most 12 per direction, with a "Source: <url> · Licence: <Public domain|CC0|Own work>"
                 line directly under it; no unreferenced files in img/<direction>/.
               The board format is described in docs/design/moodboards/README.md.
"""
import argparse
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DESIGN = os.path.join(ROOT, "docs", "design")
DESIGN_SYSTEM = os.path.join(DESIGN, "DESIGN-SYSTEM.md")

# direction file stem -> DESIGN-SYSTEM section number of its touchstone list (§x.2)
DIRECTIONS = {"drydock": 5, "nocturne": 6, "pulp-horizon": 7}

VISUAL_GROUPS = ["Ships and stations", "Space and lighting", "Destruction and VFX", "UI and typography", "Otherness"]
GROUPS = VISUAL_GROUPS + ["Audio playlist"]
ANTI_GROUP = "Anti-references"
REQUIRED_FIELDS = ["Creator", "Year", "Take", "Link"]
LICENCES = ("Public domain", "CC0", "Own work")

MIN_PER_GROUP, MIN_TOTAL, MAX_TOTAL, MIN_ANTI = 2, 16, 24, 3
MAX_IMAGES, MAX_LONG_SIDE, MAX_BYTES, JPEG_QUALITY = 12, 1600, 400 * 1024, 85

FIELD_RE = re.compile(r"^- \*\*(\w+):\*\*\s*(.*)$")
IMAGE_RE = re.compile(r"^!\[[^\]]*\]\(([^)\s]+)\)\s*$")
LICENCE_RE = re.compile(r"^\*?Source:\s*(https?://\S+)\s*·\s*Licence:\s*(.+?)\*?\s*$")
YEAR_RE = re.compile(r"^\d{4}(\s*[–-]\s*(\d{4}|present))?$")

# IJG (libjpeg) standard luminance quantisation table, natural order (ITU-T T.81 Annex K.1)
_STD_LUMA = [16, 11, 10, 16, 24, 40, 51, 61, 12, 12, 14, 19, 26, 58, 60, 55, 14, 13, 16, 24, 40, 57, 69, 56,
             14, 17, 22, 29, 51, 87, 80, 62, 18, 22, 37, 56, 68, 109, 103, 77, 24, 35, 55, 64, 81, 104, 113, 92,
             49, 64, 78, 87, 103, 121, 120, 101, 72, 92, 95, 98, 112, 100, 103, 99]
_ZIGZAG = [0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14,
           21, 28, 35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53,
           60, 61, 54, 47, 55, 62, 63]


def jpeg_info(path):
    """Return (width, height, ijg_quality or None). Raises ValueError if the file is not a baseline/progressive JPEG."""
    with open(path, "rb") as f:
        data = f.read()
    if data[:2] != b"\xff\xd8":
        raise ValueError("not a JPEG (no SOI marker)")
    i, size, luma = 2, None, None
    while i + 4 <= len(data):
        if data[i] != 0xFF:
            raise ValueError(f"corrupt marker at byte {i}")
        marker = data[i + 1]
        if marker == 0xFF:
            i += 1
            continue
        if marker in (0xD9, 0xDA):  # EOI / start of scan: headers are done
            break
        length = int.from_bytes(data[i + 2:i + 4], "big")
        seg = data[i + 4:i + 2 + length]
        if marker == 0xDB:  # DQT: may hold several tables
            j = 0
            while j < len(seg):
                precision, table_id = seg[j] >> 4, seg[j] & 0x0F
                n = 128 if precision else 64
                values = seg[j + 1:j + 1 + n]
                if table_id == 0 and precision == 0:
                    luma = [0] * 64
                    for k in range(64):
                        luma[_ZIGZAG[k]] = values[k]
                j += 1 + n
        elif marker in (0xC0, 0xC1, 0xC2):  # SOF0/1/2
            size = (int.from_bytes(seg[3:5], "big"), int.from_bytes(seg[1:3], "big"))
        i += 2 + length
    if size is None:
        raise ValueError("no SOF marker (unsupported JPEG type)")
    return size[0], size[1], _ijg_quality(luma)


def _ijg_quality(luma):
    if luma is None:
        return None
    for q in range(1, 101):
        scale = 5000 // q if q < 50 else 200 - 2 * q
        if all(min(255, max(1, (s * scale + 50) // 100)) == v for s, v in zip(_STD_LUMA, luma)):
            return q
    return None


def touchstones(section):
    """Text of DESIGN-SYSTEM '### <section>.2 Touchstones' (lower case)."""
    text = open(DESIGN_SYSTEM, encoding="utf-8").read()
    m = re.search(rf"^### {section}\.2 Touchstones.*?$(.*?)^###? ", text, re.S | re.M)
    if not m:
        raise SystemExit(f"DESIGN-SYSTEM.md: section §{section}.2 Touchstones not found")
    return m.group(1).lower()


def parse_board(path):
    """Return {group: [ref]} where ref = {title, fields, images: [(src, licence_line or None, lineno)], line}."""
    groups, group, ref = {}, None, None
    lines = open(path, encoding="utf-8").read().splitlines()
    for n, line in enumerate(lines, 1):
        if line.startswith("## "):
            group, ref = line[3:].strip(), None
            groups.setdefault(group, [])
        elif line.startswith("### ") and group is not None:
            ref = {"title": line[4:].strip(), "fields": {}, "images": [], "line": n}
            groups[group].append(ref)
        elif ref is not None:
            fm, im = FIELD_RE.match(line), IMAGE_RE.match(line)
            if fm:
                ref["fields"][fm.group(1)] = fm.group(2).strip()
            elif im:
                nxt = next((l.strip() for l in lines[n:] if l.strip()), "")
                ref["images"].append((im.group(1), nxt, n))
    return groups


def check_moodboards(errors):
    board_dir = os.path.join(DESIGN, "moodboards")
    for direction, section in DIRECTIONS.items():
        path = os.path.join(board_dir, f"{direction}.md")
        rel = os.path.relpath(path, ROOT)
        if not os.path.exists(path):
            errors.append(f"{rel}: missing")
            continue
        groups = parse_board(path)
        stones = touchstones(section)
        total, image_paths = 0, []

        for g in GROUPS + [ANTI_GROUP]:
            if g not in groups:
                errors.append(f"{rel}: missing group '## {g}'")
        for g in groups:
            if g not in GROUPS and g != ANTI_GROUP and groups[g]:
                errors.append(f"{rel}: unknown group '## {g}' holds references (allowed: {', '.join(GROUPS + [ANTI_GROUP])})")

        for g in GROUPS + [ANTI_GROUP]:
            refs = groups.get(g, [])
            if g == ANTI_GROUP:
                if len(refs) < MIN_ANTI:
                    errors.append(f"{rel}: '{g}' has {len(refs)} references, needs >= {MIN_ANTI}")
            else:
                total += len(refs)
                if len(refs) < MIN_PER_GROUP:
                    errors.append(f"{rel}: '{g}' has {len(refs)} references, needs >= {MIN_PER_GROUP}")
            if g in VISUAL_GROUPS:
                named = [r for r in refs if r["fields"].get("Touchstone")]
                for r in named:
                    phrase = r["fields"]["Touchstone"].lower()
                    if phrase not in stones:
                        errors.append(f"{rel}:{r['line']}: touchstone '{r['fields']['Touchstone']}' is not in "
                                      f"DESIGN-SYSTEM §{section}.2")
                if not any(r["fields"]["Touchstone"].lower() in stones for r in named):
                    errors.append(f"{rel}: '{g}' names no touchstone from DESIGN-SYSTEM §{section}.2")
            for r in refs:
                where = f"{rel}:{r['line']} '{r['title']}'"
                for fld in REQUIRED_FIELDS:
                    if not r["fields"].get(fld):
                        errors.append(f"{where}: missing **{fld}:**")
                year = r["fields"].get("Year", "")
                if year and not YEAR_RE.match(year):
                    errors.append(f"{where}: Year '{year}' must be YYYY or YYYY–YYYY")
                take = r["fields"].get("Take", "")
                if take and (not take.endswith(".") or re.search(r"[.!?] [A-Z]", take)):
                    errors.append(f"{where}: Take must be one sentence ending with '.'")
                link = r["fields"].get("Link", "")
                local = re.match(r"^\[[^\]]+\]\(([^)\s]+)\)$", link)
                if local and not local.group(1).startswith("http"):
                    if not os.path.exists(os.path.join(board_dir, local.group(1))):
                        errors.append(f"{where}: Link target '{local.group(1)}' not found")
                elif link and not re.match(r"^<?https?://\S+>?$", link):
                    errors.append(f"{where}: Link must be one http(s) URL or a markdown link to a repository file")
                for src, licence_line, n in r["images"]:
                    image_paths.append(os.path.normpath(os.path.join(board_dir, src)))
                    check_image(errors, rel, n, board_dir, direction, src, licence_line)

        if not MIN_TOTAL <= total <= MAX_TOTAL:
            errors.append(f"{rel}: {total} references in the six groups, needs {MIN_TOTAL}-{MAX_TOTAL}")
        if len(image_paths) > MAX_IMAGES:
            errors.append(f"{rel}: {len(image_paths)} images, at most {MAX_IMAGES}")
        img_dir = os.path.join(board_dir, "img", direction)
        if os.path.isdir(img_dir):
            for name in sorted(os.listdir(img_dir)):
                p = os.path.normpath(os.path.join(img_dir, name))
                if not name.startswith(".") and p not in image_paths:
                    errors.append(f"{os.path.relpath(p, ROOT)}: not referenced by {rel}")
        print(f"moodboards: {direction}: {total} references, {len(groups.get(ANTI_GROUP, []))} anti-references, "
              f"{len(image_paths)} images")


def check_image(errors, rel, n, board_dir, direction, src, licence_line):
    where = f"{rel}:{n} image '{src}'"
    m = LICENCE_RE.match(licence_line or "")
    if not m:
        errors.append(f"{where}: next line must be 'Source: <url> · Licence: <{'|'.join(LICENCES)}>'")
    elif m.group(2).strip() not in LICENCES:
        errors.append(f"{where}: licence '{m.group(2).strip()}' not allowed (only {', '.join(LICENCES)})")
    if not src.startswith(f"img/{direction}/") or not src.lower().endswith((".jpg", ".jpeg")):
        errors.append(f"{where}: must be a .jpg under img/{direction}/")
    path = os.path.join(board_dir, src)
    if not os.path.exists(path):
        errors.append(f"{where}: file not found")
        return
    if os.path.getsize(path) > MAX_BYTES:
        errors.append(f"{where}: {os.path.getsize(path) // 1024} KB, at most {MAX_BYTES // 1024} KB")
    try:
        w, h, q = jpeg_info(path)
    except ValueError as e:
        errors.append(f"{where}: {e}")
        return
    if max(w, h) > MAX_LONG_SIDE:
        errors.append(f"{where}: {w}×{h}, long side must be <= {MAX_LONG_SIDE} px")
    if q is not None and q != JPEG_QUALITY:
        errors.append(f"{where}: JPEG quality {q}, must be {JPEG_QUALITY}")


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("deliverable", choices=["moodboards"])
    args = ap.parse_args()
    errors = []
    if args.deliverable == "moodboards":
        check_moodboards(errors)
    for e in errors:
        print("ERROR", e)
    print(f"check_art_docs {args.deliverable}: {len(errors)} errors")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
