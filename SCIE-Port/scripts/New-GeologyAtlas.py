"""Rebuild the original pixel tiles for the geology cubes."""

from math import sin
from pathlib import Path

from PIL import Image


def hash_byte(x, y, seed):
    value = (x * 374761393 + y * 668265263 + seed * 2246822519) & 0xFFFFFFFF
    value = (value ^ (value >> 13)) * 1274126177 & 0xFFFFFFFF
    return (value ^ (value >> 16)) & 255


def line_noise(x, y, seed):
    # The coarse cells wrap at the tile edge, so broad tones repeat without a hard seam.
    u, v = (x % 8) / 8, (y % 8) / 8
    cx, cy = x // 8, y // 8
    a = hash_byte(cx, cy, seed) * (1 - u) + hash_byte((cx + 1) % 4, cy, seed) * u
    b = hash_byte(cx, (cy + 1) % 4, seed) * (1 - u) + hash_byte((cx + 1) % 4, (cy + 1) % 4, seed) * u
    return (a * (1 - v) + b * v - 128) / 9


PALETTE = [
    (51, 47, 50), (59, 64, 69), (66, 71, 75), (48, 45, 49), (217, 206, 169),
    (156, 78, 59), (181, 114, 67), (100, 118, 134), (211, 168, 107),
    (177, 170, 152), (68, 142, 179), (220, 219, 207),
]


def pixel(slot, x, y):
    high = hash_byte(x, y, slot + 9)
    relief = line_noise(x, y, slot + 200)
    shade = relief + (high - 128) / 19
    if slot == 0:
        cell = hash_byte(x // 3, y // 3, 90)
        if cell > 206 and (x % 3, y % 3) in ((1, 1), (1, 2), (2, 1)):
            shade -= 15
        if high > 244:
            shade += 13
    elif slot == 1:
        stagger = 1 if relief > 4 else 0
        groove = (x + stagger) % 11
        shade += -20 if groove == 0 else 14 if groove == 1 else 0
        if (y + 4 * (x // 11)) % 16 == 0:
            shade -= 9
    elif slot == 2:
        if (x + y // 2 + int(relief / 5)) % 17 == 4 or (y - x // 3) % 21 == 10:
            shade -= 18
    elif slot == 3:
        shade = relief * .5 + (high - 128) / 8
    elif slot in (4, 5, 6, 7, 8, 9):
        layer = int(y + 2.6 * sin(x * .195 + slot) + relief / 5)
        band = layer % (6 if slot == 4 else 9)
        shade += (-13, 10, 3)[band] if band <= 2 else 0
        if slot == 9 and (x + y // 3) % 14 == 0:
            shade -= 13
    elif slot == 10:
        fracture = (x - y // 2 + int(relief / 5)) % 14
        shade += 43 if fracture == 1 else 19 if fracture == 2 else -16 if fracture == 0 else 0
        shade += 10 * sin((x + y) * .24)
    elif slot == 11:
        cx, cy = x // 8, y // 8
        distances = []
        for j in range(cy - 1, cy + 2):
            for i in range(cx - 1, cx + 2):
                dx = x - (8 * i + 2 + hash_byte(i % 4, j % 4, 71) % 5)
                dy = y - (8 * j + 2 + hash_byte(i % 4, j % 4, 78) % 5)
                distances.append(dx * dx + dy * dy)
        nearest, next_nearest = sorted(distances)[:2]
        shade = -21 + shade * .4 if next_nearest - nearest < 7 else 9 + shade * .7
    return tuple(max(0, min(255, round(component + shade))) for component in PALETTE[slot]) + (255,)


def main():
    atlas = Image.new("RGBA", (512, 512))
    for slot in range(len(PALETTE)):
        for y in range(32):
            for x in range(32):
                atlas.putpixel((slot * 32 + x, y), pixel(slot, x, y))
    output = Path(__file__).resolve().parents[1] / "src/SCIEPort/Assets/Textures/Geology.png"
    atlas.save(output, optimize=True)


if __name__ == "__main__":
    main()
