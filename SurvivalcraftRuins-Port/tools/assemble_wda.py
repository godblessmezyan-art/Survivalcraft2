#!/usr/bin/env python3
"""Assemble multi-part WDA jigsaw structures into single SC templates.

Parts connect via minecraft:jigsaw blocks: a connector with target T and
facing F mates with another part's jigsaw named T at offset
(partOffset + jigsawPos + facing) - otherJigsawPos.

Spawner-pool jigsaws become ambush markers. Chest/barrel blocks become chest
markers (handled by the shared mapper). Optionally injects chests into
structures that have none (coliseum).
"""
import json
import sys
import os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from convert_nbt import parse_nbt, map_block

FACING_VEC = {
    "north": (0, 0, -1), "south": (0, 0, 1),
    "east": (1, 0, 0), "west": (-1, 0, 0),
    "up": (0, 1, 0), "down": (0, -1, 0),
}


def orientation_facing(orientation):
    # "east_up" -> facing east; "up_south" -> facing up.
    return orientation.split("_")[0]


def load_part(path):
    root = parse_nbt(path)
    palette = root.get("palette") or root["palettes"][0]
    jigsaws = []
    for b in root["blocks"]:
        if palette[b["state"]]["Name"] == "minecraft:jigsaw":
            props = palette[b["state"]].get("Properties", {})
            nbt = b.get("nbt", {})
            jigsaws.append({
                "pos": tuple(b["pos"]),
                "facing": orientation_facing(str(props.get("orientation", "north_up"))),
                "name": str(nbt.get("name", "")),
                "target": str(nbt.get("target", "")),
                "pool": str(nbt.get("pool", "")),
            })
    return root, palette, jigsaws


def assemble(part_paths, name, out_path, inject_chests=0, chest_y=None):
    parts = [load_part(p) for p in part_paths]
    offsets = {0: (0, 0, 0)}
    placed = [0]
    frontier = [0]
    while frontier:
        i = frontier.pop()
        _, _, jigs = parts[i]
        ox, oy, oz = offsets[i]
        for jig in jigs:
            if "spawner" in jig["pool"]:
                continue
            for j in range(len(parts)):
                if j in offsets:
                    continue
                for other in parts[j][2]:
                    if other["name"] == jig["target"] and jig["name"] == other["target"]:
                        fx, fy, fz = FACING_VEC[jig["facing"]]
                        jx, jy, jz = jig["pos"]
                        px, py, pz = other["pos"]
                        offsets[j] = (ox + jx + fx - px, oy + jy + fy - py, oz + jz + fz - pz)
                        placed.append(j)
                        frontier.append(j)
                        break
                if j in offsets:
                    break
    if len(offsets) != len(parts):
        raise SystemExit(f"{name}: only {len(offsets)}/{len(parts)} parts connected")

    report = {}
    sc_palette, lookup = [], {}

    def palette_index(block, data):
        key = (block, data)
        if key not in lookup:
            lookup[key] = len(sc_palette)
            sc_palette.append([block, data])
        return lookup[key]

    cells = {}
    chests = []
    spawners = []
    for i, (root, palette, jigs) in enumerate(parts):
        ox, oy, oz = offsets[i]
        mapped = []
        for entry in palette:
            props = {k: str(v) for k, v in entry.get("Properties", {}).items()}
            mapped.append(map_block(entry["Name"], props, report))
        for b in root["blocks"]:
            x, y, z = b["pos"]
            block, data = mapped[b["state"]]
            wx, wy, wz = x + ox, y + oy, z + oz
            if block == "__void__":
                continue
            if block == "__spawner__":
                spawners.append([wx, wy, wz])
                block, data = "AirBlock", 0
            if block == "ChestBlock":
                chests.append([wx, wy, wz])
            cells[(wx, wy, wz)] = palette_index(block, data)
        for jig in jigs:
            if "spawner" in jig["pool"]:
                jx, jy, jz = jig["pos"]
                spawners.append([jx + ox, jy + oy, jz + oz])

    # Inject chests onto solid floor cells around the structure center.
    if inject_chests > 0:
        xs = [c[0] for c in cells]
        zs = [c[2] for c in cells]
        cx, cz = (min(xs) + max(xs)) // 2, (min(zs) + max(zs)) // 2
        chest_index = palette_index("ChestBlock", 0)
        air_names = {i for i, (n, _) in enumerate(sc_palette) if n == "AirBlock"}
        candidates = []
        for (x, y, z), p in cells.items():
            if chest_y is not None and y != chest_y:
                continue
            below = cells.get((x, y - 1, z))
            above = cells.get((x, y + 1, z))
            if p in air_names and below is not None and below not in air_names \
                    and (above is None or above in air_names):
                candidates.append((x, y, z))
        candidates.sort(key=lambda c: ((c[0] - cx) ** 2 + (c[2] - cz) ** 2))
        picked = []
        for c in candidates:
            if len(picked) >= inject_chests:
                break
            if all((c[0] - p[0]) ** 2 + (c[2] - p[2]) ** 2 >= 36 for p in picked):
                picked.append(c)
        for (x, y, z) in picked:
            cells[(x, y, z)] = chest_index
            chests.append([x, y, z])

    # Markers must reflect the FINAL merged cells: boundary overlaps between
    # parts can overwrite a chest with another block (or duplicate markers).
    chest_palette = {i for i, (n, _) in enumerate(sc_palette) if n == "ChestBlock"}
    chests = sorted({(x, y, z) for (x, y, z) in map(tuple, chests)
                     if cells.get((x, y, z)) in chest_palette})
    chests = [list(c) for c in sorted(chests, key=lambda c: (c[1], c[2], c[0]))]
    spawners = [list(c) for c in sorted({tuple(s) for s in spawners}, key=lambda c: (c[1], c[2], c[0]))]
    out = {
        "name": name,
        "palette": sc_palette,
        "cells": [[x, y, z, p] for (x, y, z), p in sorted(cells.items(), key=lambda kv: (kv[0][1], kv[0][2], kv[0][0]))],
        "chests": chests,
        "spawners": spawners,
    }
    with open(out_path, "w") as f:
        json.dump(out, f, separators=(",", ":"))
    xs = [c[0] for c in cells]
    ys = [c[1] for c in cells]
    zs = [c[2] for c in cells]
    print(f"{name}: parts={len(parts)} bbox=({max(xs)-min(xs)+1}x{max(ys)-min(ys)+1}x{max(zs)-min(zs)+1}) "
          f"cells={len(cells)} chests={len(chests)} spawners={len(spawners)}")
    if report:
        top = sorted(report.items(), key=lambda kv: -kv[1])[:8]
        print("  unmapped->air: " + ", ".join(f"{k}x{v}" for k, v in top))


if __name__ == "__main__":
    base = sys.argv[1]
    out_dir = sys.argv[2]
    W = os.path.join(base, "data/dungeons_arise/structure")
    assemble(
        [os.path.join(W, f"coliseum/coliseum_part_{i}.nbt") for i in range(4)],
        "coliseum", os.path.join(out_dir, "coliseum.json"), inject_chests=8)
    assemble(
        [os.path.join(W, f"small_blimp/small_blimp_part_{i}.nbt") for i in range(2)],
        "sky_blimp", os.path.join(out_dir, "sky_blimp.json"))
    assemble(
        [os.path.join(W, f"heavenly_conqueror/heavenly_conqueror_part_{i}.nbt") for i in range(4)],
        "sky_fortress", os.path.join(out_dir, "sky_fortress.json"))
