# Portable Fortress Mk.I — final-geometry design specification

Status: **whitebox validated offline; production model is not started**. Target: SCNET `Net_x25.01.17`, Android 7723. This document freezes the body envelope, openings, circulation and equipment reservations before production art.

## Design intent

Portable Fortress Mk.I is an integrated low-poly mechanical stronghold, not a rectangular house with a vehicle skin. Its silhouette has three readable masses:

1. a broad, low armored foundation with clipped front/rear corners and side buttresses;
2. a narrower command deck, visually recessed from the lower armor;
3. a projecting faceted observation nose and accessible guarded roof.

The production palette is dark gunmetal armor, silver-worn edges, restrained bronze framing and warm interior lamps. Windows remain dark/cool so the warm interior is readable. Material separation must come from authored geometry, UVs and texture values, not whole-object tinting.

## Frozen dimensions and axes

- Exact whitebox envelope: X `-5.5..5.5` (11 wide), Z `-6.5..6.5` (13 long), Y `0..9` (9 high).
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

## Whitebox deliverables

- Project asset: `src/BuilderAssistant/Assets/Models/PortableFortress_Whitebox.dae`
- Reproducible generator: `scripts/New-PortableFortressWhitebox.ps1`
- Import validator: `scripts/Test-PortableFortressWhitebox.ps1`
- Technical views: `docs/portable-fortress/previews/{front,side,top,section}.png`

The whitebox deliberately uses a neutral embedded Lambert material. It is a geometry and import artifact, not a disguised final model.

## Gates before production modelling

Production modelling may begin only after an x25.01.17 Windows runtime check confirms model scale/orientation and an Android 7723 check confirms the chosen deployment renderer. Player movement, window transparency, block placement on installation sockets, 48-slot UI, heating and multiplayer recovery remain implementation gates, not whitebox achievements.
