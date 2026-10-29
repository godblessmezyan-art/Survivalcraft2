#!/usr/bin/env python3
"""Strip useless air cells from structure templates.

MC saves some templates as full cuboids including air; every air cell costs
~18 bytes of JSON and a write at generation time. Air is only meaningful when
it carves something: interiors and anything below the structure's own roof
line. Air above the topmost solid block of a column — and, for sky structures,
all air — does nothing.

Usage: strip_air.py <structures-dir>
Sky templates (name starts with "sky_") drop every air cell; others keep air
only at y < the column's topmost solid block.
"""
import json
import os
import sys


def strip(path):
    d = json.load(open(path))
    pal = d["palette"]
    air = {i for i, (n, _) in enumerate(pal) if n == "AirBlock"}
    cells = d["cells"]
    before = len(cells)
    if d["name"].startswith("sky_"):
        kept = [c for c in cells if c[3] not in air]
    else:
        top_solid = {}
        for x, y, z, p in cells:
            if p not in air:
                key = (x, z)
                if y > top_solid.get(key, -10_000):
                    top_solid[key] = y
        kept = [c for c in cells
                if c[3] not in air or c[1] < top_solid.get((c[0], c[2]), -10_000)]
    if len(kept) == before:
        return
    d["cells"] = kept
    json.dump(d, open(path, "w"), separators=(",", ":"))
    print(f"{d['name']}: {before} -> {len(kept)} cells "
          f"({os.path.getsize(path) // 1024} KB)")


if __name__ == "__main__":
    for f in sorted(os.listdir(sys.argv[1])):
        if f.endswith(".json"):
            strip(os.path.join(sys.argv[1], f))
