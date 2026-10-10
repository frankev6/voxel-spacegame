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

  blocks       ART-004: docs/design/board/img/blocks/<direction>/ (written by ArtSource/StyleTest/build_style_test.py)
               - stats.json, composite.png (1920 × 1080), damage_strip.png and <block>_{050,150,300}.png for the five blocks;
               - every render listed in stats.json exists with the recorded size, and its pixels per cell
                 (long side ÷ (orthographic scale ÷ 0.5 m)) is within ±10 % of DESIGN-SYSTEM §4 (14.8 / 4.9 / 2.5);
               - every mesh is within its triangle budget (DESIGN-SYSTEM §10.11; hull patches ≤ 2 per exposed face);
               - ArtSource/StyleTest/<direction>/style_test.blend exists.

  board        ART-005: docs/design/board/ (index.html, board.css, board.js, tokens/*.css)
               - tokens/*.css equal what tools/art/export_board_tokens.py generates from the .uss sheets (so every token
                 value the board reads with getComputedStyle equals its sheet);
               - the only external URLs are on fonts.googleapis.com and fonts.gstatic.com;
               - every local file the page references exists (src/href, CSS url(), block renders and mood images in board.js);
               - the HUD zone table in board.js equals DESIGN-SYSTEM §10.4.2, and the colour-vision matrices in index.html
                 equal tools/art/check_tokens.py MACHADO;
               - page weight (page files plus every referenced image) is at most 25 MB.
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


BLOCKS = ["block_hull", "block_hull_angled", "block_thruster_small", "weapon_plasma_turret_small", "block_core_small"]
REFERENCE_PPC = {"050": 14.8, "150": 4.9, "300": 2.5}       # DESIGN-SYSTEM §4
CELL_METRES = 0.5


def png_size(path):
    """(width, height) from the IHDR chunk; ValueError if the file is not a PNG."""
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n" or head[12:16] != b"IHDR":
        raise ValueError("not a PNG")
    return int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big")


def check_blocks(errors):
    import json
    for direction in DIRECTIONS:
        out = os.path.join(DESIGN, "board", "img", "blocks", direction)
        rel = os.path.relpath(out, ROOT)
        blend = os.path.join(ROOT, "ArtSource", "StyleTest", direction, "style_test.blend")
        if not os.path.exists(blend):
            errors.append(f"{os.path.relpath(blend, ROOT)}: missing")
        stats_path = os.path.join(out, "stats.json")
        if not os.path.exists(stats_path):
            errors.append(f"{rel}/stats.json: missing")
            continue
        stats = json.load(open(stats_path, encoding="utf-8"))
        required = ["composite.png", "damage_strip.png"] + [f"{b}_{d}.png" for b in BLOCKS for d in REFERENCE_PPC]
        for name in required:
            if not os.path.exists(os.path.join(out, name)):
                errors.append(f"{rel}/{name}: missing")
        composite = os.path.join(out, "composite.png")
        if os.path.exists(composite) and png_size(composite) != (1920, 1080):
            errors.append(f"{rel}/composite.png: {png_size(composite)}, must be 1920 × 1080")
        listed = set()
        for r in stats.get("renders", []):
            path = os.path.join(out, r["file"])
            listed.add(r["file"])
            if not os.path.exists(path):
                errors.append(f"{rel}/{r['file']}: listed in stats.json but missing")
                continue
            size = png_size(path)
            if list(size) != list(r["size"]):
                errors.append(f"{rel}/{r['file']}: {size} but stats.json says {tuple(r['size'])}")
            ppc = max(size) / (r["ortho_scale_m"] / CELL_METRES)
            target = REFERENCE_PPC.get(r["view"])
            if target and abs(ppc - target) > 0.10 * target:
                errors.append(f"{rel}/{r['file']}: {ppc:.2f} px per cell, §4 wants {target} ± 10 %")
        for name in required[2:]:
            if name not in listed:
                errors.append(f"{rel}/{name}: not listed in stats.json renders")
        meshes = {m["block"]: m for m in stats.get("meshes", [])}
        for block in BLOCKS:
            m = meshes.get(block)
            if m is None:
                errors.append(f"{rel}/stats.json: no mesh entry for {block}")
            elif m["triangles"] > m["budget"]:
                errors.append(f"{rel}/stats.json: {block} has {m['triangles']} triangles, budget {m['budget']} ({m['budget_rule']})")
        print(f"blocks: {direction}: {len(stats.get('renders', []))} renders, "
              + ", ".join(f"{b.replace('block_', '').replace('weapon_', '')} {meshes[b]['triangles']}/{meshes[b]['budget']}" for b in BLOCKS if b in meshes))


ALLOWED_HOSTS = {"fonts.googleapis.com", "fonts.gstatic.com"}
MAX_PAGE_BYTES = 25 * 1024 * 1024


def check_board(errors):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import export_board_tokens
    from check_tokens import MACHADO
    board = os.path.join(DESIGN, "board")
    rel = os.path.relpath(board, ROOT)
    files = ["index.html", "board.css", "board.js"]
    for f in files:
        if not os.path.exists(os.path.join(board, f)):
            errors.append(f"{rel}/{f}: missing")
    if errors:
        return
    if export_board_tokens.main(["--check"]) != 0:
        errors.append(f"{rel}/tokens: out of date with the .uss sheets (run tools/art/export_board_tokens.py)")
    token_files = sorted(f"tokens/{n}" for n in os.listdir(os.path.join(board, "tokens")) if n.endswith(".css"))
    texts = {f: open(os.path.join(board, f), encoding="utf-8").read() for f in files + token_files}

    for f, text in texts.items():
        for m in re.finditer(r"https?://([^/\s\"')]+)", text):
            if m.group(1) not in ALLOWED_HOSTS:
                errors.append(f"{rel}/{f}: external host '{m.group(1)}' (allowed: {', '.join(sorted(ALLOWED_HOSTS))})")

    referenced = set()
    html = texts["index.html"]
    for m in re.finditer(r'(?:src|href)="([^"#:]+)"', html):
        referenced.add(os.path.normpath(os.path.join(board, m.group(1))))
    for f in ["board.css"] + token_files:
        for m in re.finditer(r'url\(\s*["\']?([^"\')]+)["\']?\s*\)', texts[f]):
            if not m.group(1).startswith(("http", "data:", "#")):
                referenced.add(os.path.normpath(os.path.join(board, os.path.dirname(f), m.group(1))))
    js = texts["board.js"]
    directions = re.findall(r'^\s+"([a-z-]+)": \{\s*$', js[js.index("const DATA"):js.index("const MOOD")], re.M)
    blocks = re.findall(r'\["(\w+)", "[^"]+"\]', js[js.index("const BLOCKS"):js.index("const COLOR_TOKENS")])
    for d in directions:
        for b in blocks:
            for v in ("005", "050", "150", "300"):
                referenced.add(os.path.join(board, "img", "blocks", d, f"{b}_{v}.png"))
        for name in ("composite.png", "damage_strip.png", "damage_strip_005.png"):
            referenced.add(os.path.join(board, "img", "blocks", d, name))
    mood = js[js.index("const MOOD"):js.index("const BLOCKS")]
    for d, body in re.findall(r'"([a-z-]+)": \[(.*?\])\],?\s*(?=\n\s*"[a-z-]+": \[|\n\s*\};)', mood, re.S):
        for name in re.findall(r'\["([a-z0-9-]+)", "', body):
            referenced.add(os.path.normpath(os.path.join(DESIGN, "moodboards", "img", d, name + ".jpg")))
        on_disk = {n[:-4] for n in os.listdir(os.path.join(DESIGN, "moodboards", "img", d)) if n.endswith(".jpg")} \
            if os.path.isdir(os.path.join(DESIGN, "moodboards", "img", d)) else set()
        for missing in sorted(on_disk - set(re.findall(r'\["([a-z0-9-]+)", "', body))):
            errors.append(f"{rel}/board.js: mood image {d}/{missing}.jpg is not shown on the board")
    total = sum(os.path.getsize(os.path.join(board, f)) for f in texts)
    for path in sorted(referenced):
        if not os.path.exists(path):
            errors.append(f"{rel}: referenced file {os.path.relpath(path, ROOT)} not found")
        elif os.path.isfile(path):
            total += os.path.getsize(path)
    if total > MAX_PAGE_BYTES:
        errors.append(f"{rel}: page weight {total / 1048576:.1f} MB, budget 25 MB")

    zones_js = {m[0]: tuple(int(v) for v in m[1:]) for m in re.findall(r'\["(Z-[A-Z]+)", (\d+), (\d+), (\d+), (\d+)\]', js)}
    doc = open(DESIGN_SYSTEM, encoding="utf-8").read()
    zones_doc = {m[0]: tuple(int(v) for v in m[1:]) for m in re.findall(r"^\| (Z-[A-Z]+) \| (\d+), (\d+), (\d+), (\d+) \|", doc, re.M)}
    if not zones_doc:
        errors.append("DESIGN-SYSTEM.md: §10.4.2 zone table not found")
    elif zones_js != zones_doc:
        errors.append(f"{rel}/board.js: HUD zones {zones_js} differ from DESIGN-SYSTEM §10.4.2 {zones_doc}")

    for kind, matrix in MACHADO.items():
        m = re.search(rf'id="cvd-{kind}"[^>]*>\s*<feColorMatrix type="matrix" values="([^"]+)"', html)
        if not m:
            errors.append(f"{rel}/index.html: no feColorMatrix for {kind}")
            continue
        values = [float(v) for v in m.group(1).split()]
        expected = [x for row in matrix for x in (*row, 0, 0)] + [0, 0, 0, 1, 0]
        if len(values) != 20 or any(abs(a - b) > 1e-6 for a, b in zip(values, expected)):
            errors.append(f"{rel}/index.html: {kind} matrix differs from tools/art/check_tokens.py MACHADO")
    print(f"board: {len(directions)} directions, {len(referenced)} referenced files, page weight {total / 1048576:.1f} MB, "
          f"{len(zones_js)} HUD zones")


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("deliverable", choices=["moodboards", "blocks", "board"])
    args = ap.parse_args()
    errors = []
    if args.deliverable == "moodboards":
        check_moodboards(errors)
    elif args.deliverable == "blocks":
        check_blocks(errors)
    elif args.deliverable == "board":
        check_board(errors)
    for e in errors:
        print("ERROR", e)
    print(f"check_art_docs {args.deliverable}: {len(errors)} errors")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
