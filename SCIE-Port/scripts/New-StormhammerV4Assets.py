"""Generates the Skywrath V4 lightning textures and thunder sounds.

Textures are deliberately authored as additive-friendly alpha maps: white RGB with an
alpha profile, so the ribbon shader can tint them per vertex without washing out.

Run with the system Python that has Pillow/numpy installed:
    "C:/Program Files/Python311/python.exe" SCIE-Port/scripts/New-StormhammerV4Assets.py
"""

from __future__ import annotations

import math
import random
import struct
import wave
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
TEXTURE_DIR = ROOT / "src" / "SCIEPort" / "Assets" / "Textures"
AUDIO_DIR = ROOT / "src" / "SCIEPort" / "Assets" / "Audio" / "Stormhammer"


def write_beam() -> Path:
    """128x16 cross-section: transparent edge, hot white centre."""
    width, height = 128, 16
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    pixels = image.load()
    for x in range(width):
        u = (x + 0.5) / width
        centre = 1.0 - abs(u * 2.0 - 1.0)
        # Hot core with a soft skirt: pow keeps the centre crisp, the shoulder wide.
        alpha = (centre ** 0.55) * 0.45 + (centre ** 4.0) * 0.55
        r = 255
        g = int(248 + 7 * (1.0 - centre))
        b = 255
        for y in range(height):
            v = (y + 0.5) / height
            vertical = 0.82 + 0.18 * math.sin(math.pi * v)
            pixels[x, y] = (r, g, b, max(0, min(255, int(255 * alpha * vertical))))
    path = TEXTURE_DIR / "LightningBeam.png"
    image.save(path)
    return path


def write_rune() -> Path:
    """256x256 ground seal: two rings, spokes, ticks and eight glyph strokes."""
    size = 256
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    centre = size / 2

    def ring(radius: float, thickness: int, alpha: int, colour=(196, 226, 255)) -> None:
        box = [centre - radius, centre - radius, centre + radius, centre + radius]
        draw.ellipse(box, outline=colour + (alpha,), width=thickness)

    ring(118, 3, 210)
    ring(104, 2, 150, (140, 198, 255))
    ring(70, 2, 120, (150, 205, 255))
    ring(36, 2, 130, (210, 238, 255))

    for i in range(24):
        angle = i * math.tau / 24
        inner = 104 if i % 3 else 96
        outer = 118
        x0, y0 = centre + math.cos(angle) * inner, centre + math.sin(angle) * inner
        x1, y1 = centre + math.cos(angle) * outer, centre + math.sin(angle) * outer
        draw.line((x0, y0, x1, y1), fill=(222, 240, 255, 190), width=2)

    for i in range(12):
        angle = i * math.tau / 12 + math.tau / 24
        x0 = centre + math.cos(angle) * 72
        y0 = centre + math.sin(angle) * 72
        x1 = centre + math.cos(angle) * 100
        y1 = centre + math.sin(angle) * 100
        draw.line((x0, y0, x1, y1), fill=(168, 214, 255, 160), width=2)

    # Eight rune strokes between the inner rings.
    for i in range(8):
        angle = i * math.tau / 8
        cx = centre + math.cos(angle) * 87
        cy = centre + math.sin(angle) * 87
        tangent = angle + math.pi / 2
        dx, dy = math.cos(tangent), math.sin(tangent)
        draw.line((cx - dx * 9, cy - dy * 9, cx + dx * 9, cy + dy * 9), fill=(236, 250, 255, 220), width=3)
        normal = angle
        draw.line((cx, cy, cx + math.cos(normal) * 7, cy + math.sin(normal) * 7), fill=(236, 250, 255, 220), width=3)

    # Cross hair through the core, reads as the aim seal on the ground.
    draw.line((centre - 22, centre, centre + 22, centre), fill=(255, 255, 255, 235), width=3)
    draw.line((centre, centre - 22, centre, centre + 22), fill=(255, 255, 255, 235), width=3)
    draw.ellipse([centre - 7, centre - 7, centre + 7, centre + 7], fill=(255, 255, 255, 240))

    path = TEXTURE_DIR / "LightningRune.png"
    image.save(path)
    return path


def write_wav(path: Path, duration: float, generator, sample_rate: int = 44100) -> Path:
    frames = bytearray()
    rng = random.Random(path.name)
    for i in range(int(duration * sample_rate)):
        t = i / sample_rate
        value = max(-1.0, min(1.0, generator(t, duration, rng)))
        frames.extend(struct.pack("<h", int(value * 32767)))
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(sample_rate)
        output.writeframes(frames)
    return path


def write_audio() -> list[Path]:
    AUDIO_DIR.mkdir(parents=True, exist_ok=True)

    def strike(t, d, rng):
        # Attack transient: broadband crack that decays in ~120 ms.
        crack_env = math.exp(-26.0 * t)
        crack = (rng.random() * 2 - 1) * crack_env
        # Body: sub thump that gives the hit weight on phone speakers.
        thump_env = math.exp(-5.5 * t)
        thump = math.sin(2 * math.pi * (58 - 14 * min(1.0, t / d)) * t) * thump_env * 0.55
        # Tail: rolling rumble that outlives the crack.
        tail_env = math.exp(-2.1 * t) * min(1.0, t * 24.0)
        tail = (0.30 * math.sin(2 * math.pi * 41 * t) + 0.18 * math.sin(2 * math.pi * 63 * t + 1.3)) * tail_env
        hiss = (rng.random() * 2 - 1) * 0.10 * math.exp(-8.0 * t)
        return 0.72 * crack + 0.55 * thump + 0.7 * tail + hiss

    def rumble(t, d, rng):
        # Slow swell then a long decay, plus sparse distant crackle.
        swell = min(1.0, t * 3.2) * math.exp(-1.25 * t)
        body = 0.34 * math.sin(2 * math.pi * (37 + 4 * math.sin(t * 0.9)) * t)
        body += 0.22 * math.sin(2 * math.pi * 52 * t + 0.7)
        body += 0.12 * math.sin(2 * math.pi * 79 * t + 2.1)
        crackle = 0.0
        if rng.random() < 0.012:
            crackle = (rng.random() * 2 - 1) * 0.35
        return (body * swell) + crackle * math.exp(-1.6 * t)

    return [
        write_wav(AUDIO_DIR / "Strike.wav", 1.65, strike),
        write_wav(AUDIO_DIR / "Rumble.wav", 3.30, rumble),
    ]


def main() -> None:
    TEXTURE_DIR.mkdir(parents=True, exist_ok=True)
    beam = write_beam()
    rune = write_rune()
    audio = write_audio()
    for path in [beam, rune, *audio]:
        print(f"Generated: {path} ({path.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
