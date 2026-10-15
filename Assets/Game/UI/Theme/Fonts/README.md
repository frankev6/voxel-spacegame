# Fonts

**NOCTURNE typography (AST-004, QA-001, 2026-10-15).** With the owner's approval of the OFL downloads, the theme tokens use the DESIGN-SYSTEM §10 families. Each comes from its official release and keeps its unmodified OFL text beside it (proof copies in `docs/design/licenses/`, rows in `docs/design/LICENSES.md`):

| Token | Asset | Source |
|---|---|---|
| `--font-display` | `Michroma/FNT_Michroma-Regular_SDF.asset` (falls back to Inter SemiBold, then Regular) | Google Fonts `ofl/michroma` |
| `--font-body` | `Inter/FNT_Inter-Regular_SDF.asset` | rsms/inter v4.1 static TTF |
| `--font-body-strong` | `Inter/FNT_Inter-SemiBold_SDF.asset` | rsms/inter v4.1 static TTF |
| `--font-mono` | `JetBrainsMono/FNT_JetBrainsMono-Regular_SDF.asset` (falls back to Inter Regular) | JetBrains/JetBrainsMono v2.304 |

The SDF assets use the §10.9 settings of the earlier placeholders: SDF AA, 90 pt sampling, padding 9, 1024² dynamic multi-atlas, dynamic data cleared on build. Michroma is uppercase-only display type.

**Office Code Pro** (`OfficeCodePro/`) is the former compatibility placeholder from ART-014, kept with its OFL text and GUIDs; no token references it any more. Remove it with its register rows when nothing else does.
