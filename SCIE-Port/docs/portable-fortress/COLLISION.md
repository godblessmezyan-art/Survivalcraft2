# Portable Fortress Mk.I — collision and passage plan

> **Ownership (2026-10-26):** maintained by **SCIE-Port**. The collision plan is implemented in code by `Features/PortableFortress/PortableFortressGeometry.cs` (segment table) and `FortressDeploymentRules.cs` (traversal rules), both offline-tested.

## Proven engine boundary

The target x25.01.17 model mesh does not provide collision. `Game.ComponentBody` owns one axis-aligned `BoxSize` and resolves terrain/body collisions; the project vehicle guide records the same constraint. Therefore an `11 × 9 × 13` body around the fortress would make the entire interior solid and is explicitly forbidden.

## Selected architecture

The DAE is visual geometry only. A deployed fortress will use an authoritative anchor entity plus bounded, named collision/installation pieces:

1. terrain-backed floor and socket cells for walking and native block placement;
2. segmented wall/rail proxies only where terrain cells cannot express the final silhouette;
3. stair cells or individual step proxies matching each rise;
4. no proxy across the entrance, observation apertures, stairwell or roof hatch.

Terrain-backed walkable surfaces are preferred because `ComponentMiner.Place(TerrainRaycastResult, value)` places against a terrain face. An arbitrary DAE surface or `ComponentBody` face is not a valid native block-placement target.

## Proxy zoning

| Zone | Collision representation | Exclusions |
|---|---|---|
| Level-1 floor | thin terrain-backed slab/anchor grid | entrance threshold kept traversable |
| Lower walls | per-wall segments and four clipped-corner segments | rear door opening |
| Stair | eight 0.435 rises, last tread flush with the deck | 2.1-wide climb path and landing |
| Level-2 deck | three segments | X `-4.3..-2.2`, Z `-4..0.25` stair opening |
| Upper walls | sill/header/pillar segments | all window apertures |
| Roof | three deck segments | X `2.3..4`, Z `2.7..4.4` hatch |
| Rails | narrow edge segments | hatch-side access |

Proxy IDs must be children/resources of one fortress instance and enumerated in the fortress save record. Cleanup is by that exact list, never by a broad body/terrain search.

## Whitebox checks completed

- exact envelope is `11 × 13 × 9`;
- entrance, windows, stairwell and hatch are absent geometry, not texture tricks;
- deck pieces do not overlap the stairwell/hatch reservations;
- nominal occupied-level headroom exceeds three cells;
- target model importer accepted one bone and 11 meshes.

## Runtime passage test (not yet completed)

On a disposable world, test with the exact target player template:

1. walk through both airlock planes without snagging;
2. traverse all eight stair rises walking and sprinting;
3. stop, crouch and turn on the stair landing;
4. walk the full 4 × 4 activity area and around both storage banks;
5. reach the roof, circle the equipment pad and collide with every rail;
6. verify no fall through at proxy seams and no invisible collision in windows;
7. repeat after save/reload and as host/client.

Until these steps are run on Windows and Android, player-free-movement compatibility is **待验证**.
