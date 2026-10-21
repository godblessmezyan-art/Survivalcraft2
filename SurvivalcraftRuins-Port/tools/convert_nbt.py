#!/usr/bin/env python3
"""Convert Minecraft structure .nbt templates to SurvivalcraftRuins JSON templates.

Usage: convert_nbt.py <in.nbt> <out.json> <template-name> [--sink N]

The output format is consumed by Core/StructureTemplate.cs:
{ "name": ..., "palette": [["BlockName", data], ...],
  "cells": [[x,y,z,paletteIndex], ...], "chests": [[x,y,z], ...], "spawners": [[x,y,z], ...] }

Mapping notes:
- MC block states are reduced to SC's much smaller palette; unmappable blocks
  become air and are counted in the conversion report.
- chests/barrels emit chest markers; mob spawners emit ambush markers.
- structure_void cells are dropped entirely (terrain shows through).
"""
import gzip
import json
import struct
import sys

# ---------------------------------------------------------------- NBT parser

class Reader:
    def __init__(self, data):
        self.d = data
        self.o = 0

    def u8(self):
        v = self.d[self.o]
        self.o += 1
        return v

    def i16(self):
        v = struct.unpack_from(">h", self.d, self.o)[0]
        self.o += 2
        return v

    def i32(self):
        v = struct.unpack_from(">i", self.d, self.o)[0]
        self.o += 4
        return v

    def i64(self):
        v = struct.unpack_from(">q", self.d, self.o)[0]
        self.o += 8
        return v

    def f32(self):
        v = struct.unpack_from(">f", self.d, self.o)[0]
        self.o += 4
        return v

    def f64(self):
        v = struct.unpack_from(">d", self.d, self.o)[0]
        self.o += 8
        return v

    def string(self):
        n = struct.unpack_from(">H", self.d, self.o)[0]
        self.o += 2
        s = self.d[self.o:self.o + n].decode("utf-8", "replace")
        self.o += n
        return s


def parse_payload(r, tag):
    if tag == 1:
        return r.u8()
    if tag == 2:
        return r.i16()
    if tag == 3:
        return r.i32()
    if tag == 4:
        return r.i64()
    if tag == 5:
        return r.f32()
    if tag == 6:
        return r.f64()
    if tag == 7:
        n = r.i32()
        v = r.d[r.o:r.o + n]
        r.o += n
        return v
    if tag == 8:
        return r.string()
    if tag == 9:
        item_tag = r.u8()
        n = r.i32()
        return [parse_payload(r, item_tag) for _ in range(n)]
    if tag == 10:
        obj = {}
        while True:
            t = r.u8()
            if t == 0:
                return obj
            name = r.string()
            obj[name] = parse_payload(r, t)
    if tag == 11:
        n = r.i32()
        return [r.i32() for _ in range(n)]
    if tag == 12:
        n = r.i32()
        return [r.i64() for _ in range(n)]
    raise ValueError(f"unknown tag {tag}")


def parse_nbt(path):
    with open(path, "rb") as f:
        raw = f.read()
    if raw[:2] == b"\x1f\x8b":
        raw = gzip.decompress(raw)
    r = Reader(raw)
    tag = r.u8()
    assert tag == 10, "root must be a compound"
    r.string()
    return parse_payload(r, 10)

# ------------------------------------------------------------- block mapping

FACING = {"north": 0, "east": 1, "south": 2, "west": 3}

WOOD_KINDS = {
    "oak": "OakWoodBlock", "spruce": "SpruceWoodBlock", "birch": "BirchWoodBlock",
    "jungle": "MimosaWoodBlock", "acacia": "MimosaWoodBlock", "dark_oak": "SpruceWoodBlock",
    "mangrove": "OakWoodBlock", "cherry": "BirchWoodBlock", "crimson": "OakWoodBlock",
    "warped": "SpruceWoodBlock", "bamboo": "OakWoodBlock", "pale_oak": "PoplarWoodBlock",
}

FLOWER_MAP = {
    "poppy": "RedFlowerBlock", "red_tulip": "RedFlowerBlock", "rose_bush": "RedFlowerBlock",
    "blue_orchid": "PurpleFlowerBlock", "cornflower": "PurpleFlowerBlock",
    "allium": "PurpleFlowerBlock", "lilac": "PurpleFlowerBlock",
}

SKIP = None          # unmappable -> air, counted
VOID = "__void__"    # cell not written at all


def axis_data(props):
    return {"y": 0, "x": 1, "z": 2}.get(props.get("axis", "y"), 0)


def map_block(name, props, report):
    n = name.removeprefix("minecraft:")
    if ":" in n:
        # Modded block (betterdungeons:, dungeons_arise:, yungsapi:) — air.
        report[name] = report.get(name, 0) + 1
        return ("AirBlock", 0)
    if n in ("air", "cave_air"):
        return ("AirBlock", 0)
    if n == "structure_void" or n == "jigsaw":
        return (VOID, 0)
    if n in ("chest", "trapped_chest", "barrel"):
        return ("ChestBlock", 0)
    if n == "spawner":
        return ("__spawner__", 0)

    if n.endswith("_planks") or n in ("bookshelf", "chiseled_bookshelf", "hay_block"):
        return ("PlanksBlock", 0)
    for kind, sc in WOOD_KINDS.items():
        if n in (f"{kind}_log", f"{kind}_wood", f"stripped_{kind}_log", f"stripped_{kind}_wood"):
            return (sc, axis_data(props))
    if n.endswith("_wool"):
        return ("PlanksBlock", 0)
    if n.endswith("_carpet"):
        return ("CarpetBlock", 0)

    if n.endswith("_stairs"):
        data = FACING.get(props.get("facing", "north"), 0)
        if props.get("half") == "top":
            data |= 4
        if any(k in n for k in ("cobblestone",)):
            return ("CobblestoneStairsBlock", data)
        if "stone_brick" in n:
            return ("StoneBrickStairsBlock", data)
        if "brick" in n:
            return ("BrickStairsBlock", data)
        if "sandstone" in n:
            return ("SandstoneStairsBlock", data)
        if any(k in n for k in ("stone", "andesite", "diorite", "granite", "deepslate", "blackstone", "tuff")):
            return ("StoneStairsBlock", data)
        return ("WoodenStairsBlock", data)

    if n.endswith("_slab"):
        data = 1 if props.get("type") == "top" else 0
        if props.get("type") == "double":
            data = None  # handled below as full block
        if "cobblestone" in n:
            return ("CobblestoneBlock", 0) if data is None else ("CobblestoneSlabBlock", data)
        if "stone_brick" in n:
            return ("StoneBrickBlock", 0) if data is None else ("StoneBrickSlabBlock", data)
        if "brick" in n:
            return ("BricksBlock", 0) if data is None else ("BrickSlabBlock", data)
        if "sandstone" in n:
            return ("SandstoneBlock", 0) if data is None else ("SandstoneSlabBlock", data)
        if any(k in n for k in ("stone", "andesite", "diorite", "granite", "deepslate", "blackstone", "tuff")):
            return ("GraniteBlock", 0) if data is None else ("StoneSlabBlock", data)
        return ("PlanksBlock", 0) if data is None else ("WoodenSlabBlock", data)

    if n.endswith("_fence_gate"):
        return ("WoodenFenceGateBlock", FACING.get(props.get("facing", "north"), 0))
    if n.endswith("_fence"):
        return ("IronFenceBlock", 0) if "nether" in n or "iron" in n else ("WoodenFenceBlock", 0)
    if n.endswith("_wall"):
        return ("StoneFenceBlock", 0)
    if n in ("iron_bars", "chain"):
        return ("IronFenceBlock", 0)

    if n in ("cobblestone", "mossy_cobblestone"):
        return ("CobblestoneBlock", 0)
    if "stone_brick" in n or n in ("smooth_stone", "chiseled_stone_bricks") or n.startswith("polished_"):
        return ("StoneBrickBlock", 0)
    if n in ("stone", "andesite", "diorite", "granite", "tuff") or n.startswith("deepslate") or n.startswith("blackstone") or n == "cobbled_deepslate":
        return ("GraniteBlock", 0)
    if "sandstone" in n:
        return ("SandstoneBlock", 0)
    if n in ("bricks", "mud_bricks", "packed_mud") or n.endswith("_terracotta") or n == "terracotta":
        return ("BricksBlock", 0)
    if n in ("dirt", "coarse_dirt", "rooted_dirt", "podzol", "mud", "dirt_path"):
        return ("DirtBlock", 0)
    if n == "grass_block":
        return ("GrassBlock", 0)
    if n == "farmland":
        return ("SoilBlock", 0)
    if n == "gravel":
        return ("GravelBlock", 0)
    if n in ("sand", "red_sand"):
        return ("SandBlock", 0)
    if n == "clay":
        return ("ClayBlock", 0)
    if n == "water":
        return ("WaterBlock", 0)
    if n == "lava":
        return ("MagmaBlock", 0)
    if n == "snow_block":
        return ("SnowBlock", 0)
    if n in ("ice", "packed_ice", "blue_ice"):
        return ("IceBlock", 0)
    if n == "obsidian":
        return ("BasaltBlock", 0)
    if n == "bone_block":
        return ("LimestoneBlock", 0)

    if n.endswith("_leaves"):
        return ("OakLeavesBlock", 0)
    if n == "mushroom_stem":
        return ("BirchWoodBlock", 0)
    if n == "red_mushroom_block":
        return ("BricksBlock", 29)      # painted red
    if n == "brown_mushroom_block":
        return ("PlanksBlock", 25)      # painted brown
    if n in ("glass", "tinted_glass") or n.endswith("_stained_glass"):
        return ("GlassBlock", 0)
    if n == "glass_pane" or n.endswith("_stained_glass_pane"):
        return ("WindowBlock", 0)

    if n in ("torch", "soul_torch") or n.endswith("_candle"):
        return ("TorchBlock", 4)
    if n in ("wall_torch", "soul_wall_torch"):
        return ("TorchBlock", FACING.get(props.get("facing", "north"), 0))
    if n in ("lantern", "soul_lantern", "sea_lantern", "glowstone", "shroomlight", "ochre_froglight", "verdant_froglight", "pearlescent_froglight"):
        return ("WickerLampBlock", 0)
    if n == "ladder":
        return ("WoodenLadderBlock", FACING.get(props.get("facing", "north"), 0))
    if n == "vine":
        return ("IvyBlock", 0)
    if n == "crafting_table":
        return ("CraftingTableBlock", 0)
    if n in ("furnace", "smoker", "blast_furnace"):
        return ("FurnaceBlock", 0)
    if n in ("campfire", "soul_campfire"):
        return ("CampfireBlock", 0)
    if n in ("pumpkin", "carved_pumpkin"):
        return ("PumpkinBlock", 0)
    if n == "jack_o_lantern":
        return ("JackOLanternBlock", 0)
    if n in ("short_grass", "grass", "tall_grass", "fern", "large_fern"):
        return ("TallGrassBlock", 0)
    if n == "dead_bush":
        return ("DryBushBlock", 0)
    if n in FLOWER_MAP:
        return (FLOWER_MAP[n], 0)
    if n in ("dandelion", "oxeye_daisy", "azure_bluet", "white_tulip", "lily_of_the_valley", "chamomile"):
        return ("WhiteFlowerBlock", 0)

    # Doors and everything else unmappable become air (doorways stay open).
    report[name] = report.get(name, 0) + 1
    return ("AirBlock", 0)

# ------------------------------------------------------------------- convert

def convert(in_path, out_path, name, sink=0):
    root = parse_nbt(in_path)
    palette_src = root.get("palette") or root["palettes"][0]
    blocks = root["blocks"]
    report = {}

    mapped = []
    for entry in palette_src:
        props = {k: str(v) for k, v in entry.get("Properties", {}).items()}
        mapped.append(map_block(entry["Name"], props, report))

    sc_palette = []
    palette_lookup = {}

    def palette_index(block, data):
        key = (block, data)
        if key not in palette_lookup:
            palette_lookup[key] = len(sc_palette)
            sc_palette.append([block, data])
        return palette_lookup[key]

    cells = []
    chests = []
    spawners = []
    for blk in blocks:
        x, y, z = blk["pos"]
        block, data = mapped[blk["state"]]
        if block == VOID:
            continue
        if block == "__spawner__":
            spawners.append([x, y - sink, z])
            block, data = "AirBlock", 0
        if block == "ChestBlock":
            chests.append([x, y - sink, z])
        cells.append([x, y - sink, z, palette_index(block, data)])

    chests.sort(key=lambda c: (c[1], c[2], c[0]))
    spawners.sort(key=lambda c: (c[1], c[2], c[0]))
    out = {
        "name": name,
        "palette": sc_palette,
        "cells": cells,
        "chests": chests,
        "spawners": spawners,
    }
    with open(out_path, "w") as f:
        json.dump(out, f, separators=(",", ":"))

    size = root.get("size")
    print(f"{name}: size={size} cells={len(cells)} chests={len(chests)} spawners={len(spawners)}")
    if report:
        total = sum(report.values())
        top = sorted(report.items(), key=lambda kv: -kv[1])[:10]
        print(f"  unmapped->air ({total} blocks): " + ", ".join(f"{k}x{v}" for k, v in top))


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    sink = 0
    for a in sys.argv[1:]:
        if a.startswith("--sink"):
            sink = int(a.split("=", 1)[1])
    convert(args[0], args[1], args[2], sink)
