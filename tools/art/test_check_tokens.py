#!/usr/bin/env python3
"""Unit tests for check_tokens.py (SPEC-80 ART-002). Run: python3 tools/art/test_check_tokens.py"""
import contextlib
import io
import os
import re
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import check_tokens as ct  # noqa: E402

DRYDOCK = os.path.join(ct.THEME, "Directions", "tokens-drydock.uss")


def run_quiet(argv):
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        code = ct.main(argv)
    return code, out.getvalue()


class ContrastTests(unittest.TestCase):
    def test_white_on_black_is_21(self):
        self.assertEqual("21.00", f"{ct.contrast(ct.parse_hex('#FFFFFF'), ct.parse_hex('#000000')):.2f}")

    def test_contrast_is_symmetric_and_one_for_equal_colours(self):
        a, b = ct.parse_hex("#2EC4B6"), ct.parse_hex("#16191C")
        self.assertAlmostEqual(ct.contrast(a, b), ct.contrast(b, a), places=12)
        self.assertAlmostEqual(1.0, ct.contrast(a, a), places=12)


class Ciede2000Tests(unittest.TestCase):
    # Sharma, Wu, Dalal (2005), Table 1: pairs 1, 7, 17 and 25.
    PAIRS = [((50.0, 2.6772, -79.7751), (50.0, 0.0, -82.7485), 2.0425),
             ((50.0, 0.0, 0.0), (50.0, -1.0, 2.0), 2.3669),
             ((50.0, 2.5, 0.0), (73.0, 25.0, -18.0), 27.1492),
             ((60.2574, -34.0099, 36.2677), (60.4626, -34.1751, 39.4387), 1.2644)]

    def test_sharma_reference_pairs(self):
        for lab1, lab2, expected in self.PAIRS:
            self.assertAlmostEqual(expected, ct.ciede2000(lab1, lab2), delta=0.0001, msg=f"{lab1} vs {lab2}")

    def test_identical_colours_have_zero_difference(self):
        self.assertEqual(0.0, ct.min_delta_e(ct.parse_hex("#E5DCC8"), ct.parse_hex("#E5DCC8")))


class SheetTests(unittest.TestCase):
    def check_without(self, token):
        with open(DRYDOCK, encoding="utf-8") as f:
            text = f.read()
        stripped = re.sub(rf"^\s*{re.escape(token)}\s*:[^;]*;\n", "", text, flags=re.M)
        self.assertNotEqual(text, stripped, f"{token} not found in the DRYDOCK sheet")
        with tempfile.TemporaryDirectory() as tmp:
            path = os.path.join(tmp, "tokens-drydock.uss")
            with open(path, "w", encoding="utf-8") as f:
                f.write(stripped)
            return run_quiet(["--sheet", path])

    def test_sheet_missing_hull_fails(self):
        code, out = self.check_without("--color-hull")
        self.assertEqual(1, code)
        self.assertIn("missing token --color-hull", out)

    def test_removing_any_binding_token_fails_naming_it(self):
        for token in ct.binding_tokens(ct.read_doc()):
            code, out = self.check_without(token)
            self.assertEqual(1, code, token)
            self.assertIn(f"missing token {token}", out)

    def test_binding_list_has_the_44_tokens_of_10_1(self):
        tokens = ct.binding_tokens(ct.read_doc())
        self.assertEqual(44, len(tokens))
        self.assertEqual(len(tokens), len(set(tokens)))
        self.assertIn("--space-5", tokens)

    def test_repository_sheets_pass_and_match_the_document(self):
        code, out = run_quiet(["--verify-doc"])
        self.assertEqual(0, code, out)


if __name__ == "__main__":
    unittest.main()
