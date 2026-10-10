"""Art directions for the 3D look-development pass, round 2: the owner's finalists (DRYDOCK, NOCTURNE) and the new
space-opera direction (OPERA). Pure data (no bpy). Round 1 (six directions) is preserved as renders in
docs/design/lookbook/img/<direction>/ and as code at commit 1534b2e.

Colour tags used by the block kit and the hull mesher:
  hull hull2 armor trim metal dark accent    structure and paint
  paint_w paint_p paint_e c_cargo            function colours: weapons, power, engines, cargo
  chrome gold ceramic hose heat glass foil   materials
"""

# Alien hull colourways (the same alien kit, re-skinned). Each world picks a default; the look book shows all four.
ALIEN_COLOURWAYS = {
    "bone": dict(name="Bone & jade", shell="#D8D0BC", shell2="#7E6E5A", groove="#2A2420", vein="#33FF9A", glow="#8CFFB8", irid=0.15),
    "obsidian": dict(name="Obsidian & acid", shell="#121315", shell2="#26272C", groove="#030304", vein="#B8FF2E", glow="#D4FF6A", irid=0.08),
    "coral": dict(name="Blood coral", shell="#7A1F27", shell2="#2E0E14", groove="#14060A", vein="#44E8FF", glow="#9AF4FF", irid=0.2),
    "slick": dict(name="Oil slick", shell="#2B2440", shell2="#123026", groove="#08070C", vein="#FFB02E", glow="#FFD27A", irid=0.9),
}

STYLES = {}

STYLES["drydock"] = dict(
    name="DRYDOCK",
    tagline="Industrial used-future: bone plating in faction liveries, safety yellow, colour-coded machinery, work lights.",
    shape="industrial", seg=16, bevel=0.03, bevel_seg=2, subdiv=0, flat=False, outline=None,
    panel=2, seam_w=0.012, seam_dark=0.5, grime=0.3, variation=0.05, streaks=0.0,
    mosaic=[("#D8D0BE", 0.62), ("#E3DCCB", 0.16), ("#C9C3B4", 0.14), ("#B9B6AE", 0.08)],
    greeble=dict(window=0.10, hatch=0.05, vent=0.06, pipe=0.12, plate=0.05, light=0.02, antenna=0.015, stripe=0.03, handrail=0.03),
    col=dict(hull="#D8D0BE", hull2="#2F6690", armor="#3C4248", trim="#4B5158", metal="#8F969D", dark="#15171A",
             accent="#F2B705", paint_w="#A8432C", paint_p="#2A8C82", paint_e="#565D66", c_cargo="#D9792B",
             chrome="#C9CED4", gold="#B9823A", ceramic="#E6DFCD", hose="#222426", glass="#16222C", foil="#B87333",
             interior="#2C2E31", solar="#141B2B"),
    rough=dict(hull=0.55, hull2=0.45, armor=0.42, trim=0.5, metal=0.32, paint_w=0.45, paint_p=0.45, paint_e=0.4, c_cargo=0.55),
    metal=dict(hull=0.05, hull2=0.1, armor=0.55, trim=0.45, metal=0.9, paint_w=0.15, paint_p=0.15, paint_e=0.5, c_cargo=0.2),
    liveries={
        "yard": dict(hull2="#2F6690", accent="#F2B705"),
        "oxide": dict(hull2="#8E3A26", accent="#E9E2CF"),
        "hauler": dict(hull2="#D9792B", accent="#22262A"),
        "teal": dict(hull2="#1F7A6E", accent="#F2B705"),
        # round 4, line B (Caisson Heavy Industries): worn safety-yellow plate, graphite frames, white tanks
        "caisson": dict(plating="caisson", hull="#D39A2A", hull2="#E3DED1", armor="#3B3F45", trim="#2D3035", accent="#17181B",
                        metal="#80868D", dark="#141518", girder="#C98F22", paint_w="#3B3F45", paint_e="#4A5058",
                        plate_paint="#3B3F45", plate_accent="#ECE6D6", plate_black="#121315", plate_metal="#3A3C40",
                        bare="#A9ACB0", plate_paint_metal=0.3, plate_paint_rough=0.45,
                        rough=dict(hull=0.48, hull2=0.42, armor=0.4, trim=0.45),
                        metal_k=dict(hull=0.12, hull2=0.08, armor=0.5, trim=0.55), wear=0.85, relief=0.025),
    },
    emit=dict(thruster=("#FF9A3D", 7), weapon=("#FF4A2E", 9), core=("#FFB347", 9), shield=("#58B7FF", 6),
              power=("#4FE3D2", 6), window=("#FFC680", 4.5), exotic=("#C4A0FF", 16), nav_r=("#FF2A1A", 22), nav_g=("#2AFF6A", 22), nav_w=("#FFFFFF", 26),
              ember=("#FF5A1A", 12), seam=None),
    world=dict(kind="deep", bg="#06070C", stars=1.3, band=("#8A7A92", "#B08A66", 0.42),
               nebula=("#B0502A", "#2A8AA0", 0.9), dust=("#0C0A08", 0.75)),
    sun=dict(color="#FFF2E0", strength=4.8, az=-35, el=35, angle=0.6), fill=dict(color="#6F8DB5", strength=0.45, az=70, el=-10),
    rim=dict(color="#A8C4E8", strength=1.4, az=160, el=25), ambient=("#18202C", 0.32),
    view="AgX", look="AgX - Medium High Contrast", exposure=0.05, bloom=(0.9, 0.35), saturation=1.05,
    planet=dict(kind="desert", base=("#7A3E22", "#B8693A", "#D9A066", "#4A2618"), cloud=("#E6D2B8", 0.06),
                atmo=("#F2A866", "#FFD9A8", 1.0), lights=None, ring=("#D8C4A0", "#8A6A4E", "#5E4632")),
    moon=dict(kind="rock", base=("#6B6A68", "#8C8A86")),
    aliens="bone",
    ui=dict(font_display="Barlow Condensed", font_body="Barlow", font_mono="IBM Plex Mono", w_display=600),
)

STYLES["nocturne"] = dict(
    name="NOCTURNE",
    tagline="Industrial gothic horror: hunched, ribbed hulls in worn gunmetal and ochre, sodium slits, red strobes, a storm world.",
    shape="gothic", seg=16, bevel=0.025, bevel_seg=2, subdiv=0, flat=False, outline=None,
    panel=2, seam_w=0.014, seam_dark=0.6, grime=0.45, variation=0.05, streaks=0.4,
    mosaic=[("#A3A49C", 0.42), ("#AEADA4", 0.2), ("#94968E", 0.22), ("#A69E86", 0.1), ("#8A8E7E", 0.06)],
    greeble=dict(window=0.035, hatch=0.04, vent=0.07, pipe=0.16, plate=0.0, strobe=0.012, spike=0.02, rib=0.10),
    col=dict(hull="#A3A49C", hull2="#A89060", armor="#575A52", trim="#34362F", metal="#6A665B", dark="#0D0E0C",
             accent="#B23A2A", paint_w="#7A2E24", paint_p="#B08A2E", paint_e="#4E524D", c_cargo="#5E6448",
             chrome="#9A968C", gold="#8A6A3A", ceramic="#BDB49E", hose="#1C1D1A", glass="#1A1F1A", foil="#7A5A34",
             interior="#2B2622", solar="#121410"),
    rough=dict(hull=0.42, hull2=0.5, armor=0.38, trim=0.45, metal=0.35, paint_w=0.45, paint_p=0.5, paint_e=0.4, c_cargo=0.55),
    metal=dict(hull=0.35, hull2=0.15, armor=0.6, trim=0.55, metal=0.85, paint_w=0.3, paint_p=0.2, paint_e=0.6, c_cargo=0.2),
    liveries={
        "company": dict(hull2="#A89060", accent="#B23A2A"),
        "salvage": dict(hull2="#55665A", accent="#D9C27A"),
        "penal": dict(hull2="#6A2A22", accent="#D9D2C0"),
        # round 4, line B (the Knell choir): bone plate, blackened bronze ribs, gilt trim, candle-lit windows
        "knell": dict(plating="knell", hull="#CDBF9E", hull2="#6E5131", armor="#3A352F", trim="#2A2622", accent="#B98F3E",
                      metal="#8A7650", dark="#100F0D", girder="#6E5131", gold="#C79B45", paint_w="#4E2A22", paint_e="#3E3A34",
                      plate_paint="#6E5131", plate_accent="#C9A04E", plate_black="#16130F", plate_metal="#3A332B",
                      bare="#BFB59C", plate_paint_metal=0.8, plate_paint_rough=0.32,
                      rough=dict(hull=0.5, hull2=0.32, armor=0.4, trim=0.38),
                      metal_k=dict(hull=0.05, hull2=0.85, armor=0.6, trim=0.7), wear=0.6, relief=0.025, window="#FFB65A"),
    },
    emit=dict(thruster=("#BFD6FF", 8), weapon=("#FF5530", 9), core=("#FFB347", 9), shield=("#9FE0C8", 5),
              power=("#FFB347", 7), window=("#FFAE4A", 7.5), exotic=("#A8CCFF", 14), nav_r=("#FF2410", 28), nav_g=("#7CFF9A", 14), nav_w=("#FFF1D6", 24),
              ember=("#FF6A2A", 9), seam=None),
    world=dict(kind="storm", bg="#020303", stars=0.6, nebula=("#2A3A30", "#4A3A22", 0.75), glow=("#5E7A5A", 0.3),
               nebula2=("#4A2468", "#22285E", 0.55), glow2=("#8A3E1E", 0.22), filament=0.16,
               dust=("#070605", 1.0)),
    sun=dict(color="#EEF2FF", strength=3.4, az=-60, el=40, angle=0.4), fill=dict(color="#4A5A48", strength=0.18, az=80, el=-20),
    rim=dict(color="#D0E4D8", strength=3.0, az=170, el=15), ambient=("#0C100E", 0.18),
    view="AgX", look="AgX - High Contrast", exposure=-0.02, bloom=(0.85, 0.5), saturation=1.0,
    haze=dict(color="#58605A", density=0.0006, anisotropy=0.65),
    planet=dict(kind="storm", base=("#4A4232", "#7A6A4A", "#B8A47C", "#1E1B16"), cloud=("#D2C29C", 0.55),
                accents=("#7E3A26", "#4E4060", "#3E6A62"), eye="#BFE0D4",
                atmo=("#C9B98A", "#E8DCB8", 0.8), lights=("#CFE6FF", 7.0), ring=None),
    moon=dict(kind="rock", base=("#3A3A36", "#55534C")),
    aliens="obsidian",
    ui=dict(font_display="Michroma", font_body="Inter", font_mono="JetBrains Mono", w_display=400),
)

STYLES["opera"] = dict(
    name="OPERA",
    tagline="Space opera: sculpted ships in bold liveries, chrome and gold, monumental capital ships, painted skies and ringed giants.",
    shape="opera", seg=32, bevel=0.06, bevel_seg=3, subdiv=0, flat=False, outline=None,
    panel=4, seam_w=0.010, seam_dark=0.35, grime=0.0, variation=0.03, streaks=0.0,
    mosaic=None, bands=dict(period=24, width=3, stripe=0.6),
    greeble=dict(window=0.10, hatch=0.03, vent=0.03, pipe=0.0, plate=0.0, light=0.02, fairing=0.05),
    col=dict(hull="#E8E0CC", hull2="#C8492A", armor="#2E3540", trim="#2C2F38", metal="#C9CED6", dark="#14151C",
             accent="#E9A92A", paint_w="#3A4250", paint_p="#1E8C8A", paint_e="#E8E0CC", c_cargo="#E9A92A",
             chrome="#E6EAF0", gold="#D9A83E", ceramic="#F5F0E6", hose="#2A2A30", glass="#16222E", foil="#D9A83E",
             interior="#3A2A45", solar="#16204A"),
    rough=dict(hull=0.42, hull2=0.38, armor=0.38, trim=0.4, metal=0.2, paint_w=0.38, paint_p=0.38, paint_e=0.42, c_cargo=0.4),
    metal=dict(hull=0.0, hull2=0.0, armor=0.0, trim=0.3, metal=0.9, paint_w=0.0, paint_p=0.0, paint_e=0.0, c_cargo=0.0),
    coat=0.25,
    liveries={
        "crown": dict(hull2="#C8492A", accent="#E9A92A"),
        "guild": dict(hull2="#23252B", accent="#D9A83E"),
        "solar": dict(hull2="#F3B229", accent="#14151C"),
        "lagoon": dict(hull2="#1E9C9A", accent="#F2ECDF"),
    },
    emit=dict(thruster=("#FFD88A", 8), weapon=("#FF4FA8", 9), core=("#FFD36B", 9), shield=("#7FE0FF", 5),
              power=("#6BFFE8", 6), exotic=("#FF7AD8", 14), window=("#FFE6B0", 4.5), nav_r=("#FF3B6E", 18), nav_g=("#3BFFB0", 18), nav_w=("#FFFFFF", 22),
              ember=("#FF8A3A", 10), seam=None),
    world=dict(kind="opera", bg="#0A0B2A", sky=("#04061A", "#141048", "#4A1A5E", "#B8456A"), stars=1.4,
               nebula=("#E0409A", "#1FB4D8", 1.1), clusters=1.0),
    sun=dict(color="#FFF0DC", strength=4.4, az=-40, el=35, angle=0.8), fill=dict(color="#7A70D8", strength=0.5, az=70, el=10),
    rim=dict(color="#FFB8D8", strength=1.6, az=185, el=25), ambient=("#2A2850", 0.4),
    view="AgX", look="AgX - Medium High Contrast", exposure=0.1, bloom=(1.0, 0.45), saturation=1.08,
    planet=dict(kind="giant", base=("#1E8C8A", "#E8DCC0", "#D8603A", "#E0A030"), cloud=None, gain=0.82,
                atmo=("#9FE8FF", "#FFFFFF", 0.8), lights=None, ring=("#F0DCB0", "#C08048", "#6A4A90")),
    moon=dict(kind="rock", base=("#8A6AA0", "#D8B0C8")),
    aliens=None,
    ui=dict(font_display="Cinzel", font_body="Josefin Sans", font_mono="Space Mono", w_display=600),
)

ORDER = ["drydock", "nocturne", "opera"]
