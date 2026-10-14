#!/usr/bin/env python3
"""QA-001: procedural UI sound set (own work, deterministic, standard library only).

Writes 48 kHz 16-bit mono WAVs to Assets/Game/Audio/Sfx/Ui/. Re-running reproduces identical files.
Design: soft, short and tonal in one family (sine partials with fast attack and exponential decay, a little
band-limited noise for the click transients), so hover/click stay subtle and open/close/success/fail read clearly.
"""
import math, pathlib, random, struct, wave

RATE = 48000
OUT = pathlib.Path(__file__).resolve().parents[2] / "Assets/Game/Audio/Sfx/Ui"


def tone(freq, length, decay, gain=1.0, attack=0.002, partials=((1, 1.0), (2, 0.25), (3, 0.08)), glide=0.0):
    """Sine partials; glide multiplies the frequency linearly over the length (e.g. 0.2 = +20%)."""
    n = int(length * RATE); out = [0.0] * n; phase = [0.0] * len(partials)
    for i in range(n):
        t = i / RATE
        env = min(1.0, t / attack) * math.exp(-t / decay)
        f = freq * (1 + glide * t / length)
        s = 0.0
        for k, (mult, amp) in enumerate(partials):
            phase[k] += 2 * math.pi * f * mult / RATE
            s += amp * math.sin(phase[k])
        out[i] = s * env * gain
    return out


def noise(length, decay, gain, cutoff=0.25, seed=7):
    """One-pole low-passed noise burst (cutoff as a 0..1 smoothing factor)."""
    rng = random.Random(seed); n = int(length * RATE); out = [0.0] * n; y = 0.0
    for i in range(n):
        y += cutoff * (rng.uniform(-1, 1) - y)
        out[i] = y * math.exp(-(i / RATE) / decay) * gain
    return out


def mix(*parts):
    """parts: (offset_seconds, samples)."""
    n = max(int(o * RATE) + len(s) for o, s in parts); out = [0.0] * n
    for o, s in parts:
        start = int(o * RATE)
        for i, v in enumerate(s):
            out[start + i] += v
    return out


def write(name, samples, peak=0.7):
    top = max(1e-9, max(abs(v) for v in samples)); scale = peak / top
    fade = int(0.004 * RATE)   # click-free tail
    for i in range(1, fade + 1):
        samples[-i] *= i / fade
    OUT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v * scale)) * 32767)) for v in samples))


LOW = ((1, 1.0), (2, .4), (3, .2))
BELL = ((1, 1.0), (2.76, .3))
SOUNDS = {
    # Subtle: hover is a barely-there high tick; clicks are short, rounded and woody rather than metallic.
    "SFX_Ui_Hover_01": (lambda: mix((0, tone(2400, .035, .008, partials=((1, 1.0),))), (0, noise(.02, .004, .15, .5))), .22),
    "SFX_Ui_Click_01": (lambda: mix((0, tone(1180, .07, .018)), (0, noise(.03, .005, .5, .35))), .55),
    "SFX_Ui_SlotClick_01": (lambda: mix((0, tone(820, .07, .02)), (0, noise(.03, .005, .4, .3))), .5),
    # Screens: a quick upward two-note for open, the mirror for close.
    "SFX_Ui_Open_01": (lambda: mix((0, tone(660, .14, .05, glide=.06)), (.045, tone(990, .16, .06))), .5),
    "SFX_Ui_Close_01": (lambda: mix((0, tone(880, .12, .045, glide=-.06)), (.04, tone(587, .14, .05))), .45),
    # Outcomes: bright major arpeggio, dull low double-thud, two-tone register "ding" for trades.
    "SFX_Ui_Success_01": (lambda: mix((0, tone(784, .22, .08)), (.06, tone(988, .22, .08)), (.12, tone(1319, .32, .12))), .6),
    "SFX_Ui_Fail_01": (lambda: mix((0, tone(196, .16, .05, partials=LOW)), (.11, tone(165, .2, .06, partials=LOW))), .6),
    "SFX_Ui_Trade_01": (lambda: mix((0, tone(1568, .25, .09, partials=BELL)), (.07, tone(2093, .3, .11, partials=BELL))), .5),
}

if __name__ == "__main__":
    for name, (make, peak) in SOUNDS.items():
        write(name, make(), peak)
        print("wrote", OUT / f"{name}.wav")
