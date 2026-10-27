# Portable Fortress Mk.I — final-geometry design specification

> **Ownership (2026-10-26):** this specification and the model it describes are now maintained by **SCIE-Port** as an independent gameplay extension. The authoritative asset is `SCIE-Port/src/SCIEPort/Assets/Models/PortableFortress.dae`; the pre-migration copy under `src/BuilderAssistant/` is kept only as a rollback reference and is not built or packaged.

Status: **Hearthguard production art complete offline; runtime verification pending**. Target: SCNET `Net_x25.01.17`, Android 7723. It freezes the body envelope, openings, circulation and equipment reservations; the art pass in section 4 is now generated from the same constants.

## Design intent

Portable Fortress Mk.I is an integrated low-poly mechanical stronghold, not a rectangular house with a vehicle skin. Its silhouette has three readable masses:

1. a broad, low armored foundation with clipped front/rear corners and side buttresses;
2. a narrower command deck, visually recessed from the lower armor;
3. a projecting faceted observation nose and accessible guarded roof.

The production palette is fog-blue armour with silver-worn edges, restrained bronze framing and warm interior lamps. Windows stay cool so the warm interior reads through them. Material separation comes from authored geometry, UVs and texture values, never whole-object tinting.

## Frozen dimensions and axes

- Exact envelope: X `-5.5..5.5` (11 wide), Z `-6.5..6.5` (13 long), Y `0..9` (9 high).
- `Y_UP`, one model unit = one game cell/metre.
- Front is `-Z`; rear entrance is `+Z`; origin is foundation-centre at ground level.
- Root transform is identity. All production transforms must be baked before export.

These limits are hard. Antennae, handles, hinges and armor lips must remain inside the envelope unless a later gameplay requirement explicitly changes the deployment footprint.

## Structural language for the production model

- Lower hull: layered 0.25–0.4-cell armor bands, clipped corners, recessed door frame, four grounded load paths. Avoid a continuous featureless wall.
- Upper hull: 8.5-cell-wide recessed command deck with repeated window mullion rhythm. The observation nose has a flat centre bay plus two diagonal side bays.
- Roof: walkable plate, open hatch, 1.1-cell guard rail, and a `3.8 × 3.0` reserved equipment pad.
- Interior: structural ribs should align through both decks. Equipment is modular and must not narrow the mandatory paths in `LAYOUT.md`.
- Window apertures, rear entrance, stairwell and roof hatch are real holes in geometry. A painted black/blue rectangle is not acceptable.

## Production asset plan (next phase, not claimed complete)

| Asset | Planned contract |
|---|---|
| Static model | COLLADA 1.4.1 DAE, one static root/bone, multiple meshes, identity transform |
| LOD0 | target 8k–16k triangles after art review; Android profile must be measured before raising it |
| LOD1 | target 3k–6k triangles, retains silhouette/openings |
| Materials | armor, trim/interior, window; minimize draw calls and atlas where practical |
| Texture | authored 1024² atlas baseline; optional separate window/emissive-equivalent only after target renderer proof |
| Collision | never derived from render mesh; use the segmented plan in `COLLISION.md` |

No shader, bloom, emissive map, dynamic light or transparent-glass promise is made here. The target renderer must be proven on the Android client before any such feature enters the production acceptance criteria.

## Production art deliverables

- Project asset: `src/SCIEPort/Assets/Models/PortableFortress.dae`
- UV atlas: `src/SCIEPort/Assets/Textures/PortableFortress.png` (1024 x 1024, 16 material regions)
- Reproducible generator: `SCIE-Port/scripts/New-PortableFortressArt.py` (model, atlas, glow/glass maps and previews in one run)
- Import validator: `SCIE-Port/scripts/Test-PortableFortressModel.ps1`
- Generated views: `SCIE-Port/docs/portable-fortress/previews/{front,side,rear,entrance,interior-lower,interior-upper,roof}.png`

The art direction is "Twilight Hearthguard": fog-blue armour (`#687B88`), silver structure
(`#A6B1AF`), aged bronze framing (`#AE8456`) and dark load-bearing structure (`#35444B`) outside;
warm plaster (`#DDD2BC`), wood (`#996F4D`), leather, cloth and hearth light (`#E9AA68`) inside.
Material separation comes from the UV atlas and authored geometry, never from tinting the whole
model. See `MODEL-AUDIT.md` for the measured atlas regions and the target-import contract.

## Remaining gates

Model scale/orientation, entrance traversal, glazing readability, interior legibility at night and
7723 frame cost are still unverified in game. Player movement, storage UI, heating and multiplayer
recovery remain implementation gates; the model carries reserved structures for them and no more.
