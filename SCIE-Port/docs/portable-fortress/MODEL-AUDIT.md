# Portable Fortress Mk.I — model and engine audit

> **Ownership (2026-10-26):** maintained by **SCIE-Port**. Paths and recorded results below refer to the SCIE-Port copy.

Audit date: 2026-10-26. Target references: local SCNET `Net_x25.01.17` installation under `SCNetGameDir`; proprietary assemblies are not committed.

## Source evidence reviewed

- `SCIE-Port/docs/VEHICLE-MODEL-GUIDE.md`: DAE axis/unit/root/index requirements and the separation of model/collision.
- Target `Engine.Media.ModelData.Load(Stream)`: used directly by the repeatable validator, not inferred from another engine version.
- Target `Game.ComponentBody`: one AABB `BoxSize`; model mesh is not collision.
- Target `Game.ComponentMiner.Place(TerrainRaycastResult,int)`: native placement consumes a terrain hit/face and terrain cell coordinates.
- Target `Game.ComponentInventoryBase` / `ComponentChest` / `SubsystemInventories`: slot persistence and inventory registration.
- Target `Game.ChestWidget`: shipped grid drives the visible chest slot widgets; no 48-slot claim is made.
- Target `Game.ComponentVitalStats.UpdateTemperature` and `SubsystemMetersBlockBehavior.CalculateTemperature`: player temperature is derived from terrain/environment heat.
- Target `Game.SubsystemBlockEntities.CreateBlockEntity` and chest behavior: block-entity creation is authority-sensitive and tied to terrain coordinates.

These are ABI findings for the supplied Windows x25.01.17 assemblies. A 7723-repacked Android binary must still be rechecked by hash/API surface.

## Generated DAE contract

| Check | Result |
|---|---|
| Format | COLLADA 1.4.1 DAE |
| Units / axis | metre=1, Y_UP |
| Scene root | one `PortableFortress` node, identity matrix |
| Imported bones | 1 |
| Geometry meshes | 11 |
| Authored structural parts | 104 |
| Triangles | 1248 |
| Exact bounds | X `-5.5..5.5`, Y `0..9`, Z `-6.5..6.5` |
| Vertex streams | position, normal, UV |
| Triangle inputs | interleaved offsets 0/1/2 |
| Material | one embedded neutral Lambert whitebox material |
| Openings | entrance, observation windows, stairwell and roof hatch are true holes |

Command:

```powershell
./scripts/New-PortableFortressModel.ps1
./scripts/Test-PortableFortressModel.ps1 -GameDir E:\SCNET\SCNETx25.01.17
```

Recorded result:

```text
Portable Fortress whitebox: PASS
Target importer: Engine.Media.ModelData.Load(Stream)
Bones/Meshes/Triangles: 1/11/1248
Bounds: 11 x 13 x 9 (X -5.5..5.5, Z -6.5..6.5, Y 0..9)
```

This proves that the exact target Windows engine can parse the generated file as one static bone with multiple geometries. It does **not** prove that a fortress entity currently requests/draws it; runtime integration is intentionally outside this round.

## Rendering and glass conclusion

The whitebox contains no glass panes, because that would falsely claim transparent-material support. It validates unobstructed sight openings only. Production glass requires an explicit x25.01.17 content/material experiment for alpha blending, sorting from inside/outside, double-sided behavior and Android cost. If that fails, the accepted fallback is an open aperture with a physical frame—not an opaque painted window.

## Preview review

- `previews/front.png`: entrance and panoramic-frame proportions.
- `previews/side.png`: lower/upper massing, stair run and repeated side apertures.
- `previews/top.png`: roof guard rail, hatch position and equipment reserve.
- `previews/section.png`: level program and vertical separation.

The previews are deterministic technical drawings generated from the same part list as the DAE. They are not screenshots from the game.

## Verification status

| Item | Status | Evidence / remaining work |
|---|---|---|
| DAE structure and target Windows import | 已确认 | repeatable validator passes |
| Exact envelope/opening geometry | 已确认 | generated-coordinate validation |
| Release package contains DAE | 待验证 | the SCIE-Port package guard and `.netmod` build for this asset have not run yet this round; previous Builder Assistant package claim is not carried over |
| Runtime draw, orientation and scale | 待验证 | needs entity/content integration and Windows run |
| Player collision/traversal | 待验证 | collision proxies are designed, not implemented |
| See-through glass | 待验证 | no glass material shipped |
| Native block placement in reserved areas | 待验证 | requires terrain socket implementation |
| 48-slot storage/save/load | 待验证 | component/widget not implemented |
| Heating | 待验证 | heat socket/block not implemented |
| Android 7723 rendering/performance | 待验证 | no device run performed |
| Multiplayer deploy/storage/recovery | 待验证 | no network implementation or two-device run |

Production high-detail modelling is blocked on the runtime items above, exactly as required by this milestone.

## Recorded Release build

Command:

```powershell
./scripts/Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17 -Configuration Release
```

Result on 2026-10-26:

- environment/ABI checks: passed; known Windows assembly hashes matched;
- offline Builder Assistant checks: `492/492` passed;
- target-import check: passed (`1/11/1248`, bounds `11 × 13 × 9`) on 2026-10-26 for the migrated SCIE-Port copy;
- net472 Release: succeeded, 0 reported errors and 0 reported warnings;
- package entry validation and XDB/block guards: passed;
- artifact: `artifacts/SCIEPort.netmod` (future build); the pre-migration Builder Assistant package is not part of this deliverable;
- SHA-256: `5D89A3DF7D1A87FAC85AAB766B1C8D983B5D26609519497C2565E25052EECF8F`.

No Windows game session, Android device session or multiplayer session was run; those rows remain **待验证**.
