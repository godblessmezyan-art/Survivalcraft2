# Portable Fortress Mk.I — interaction, persistence and multiplayer plan

> **Ownership (2026-10-26):** maintained by **SCIE-Port**. Recovery stays reserved; see `PortableFortress.md` for this round's implemented boundary.

This file separates feasible engine-backed designs from work that has not been implemented or tested.

## Native block and item placement

Target `ComponentMiner.Place(TerrainRaycastResult, value)` places into terrain coordinates adjacent to a terrain face. Render meshes and entity-body faces do not become placement surfaces. The fortress therefore reserves terrain-backed installation strips in the work zone, storage wall and roof equipment pad. Native blocks/chests can be placed only on those authored terrain surfaces; free placement on visual DAE panels is not claimed.

Recovery must refuse while foreign/player blocks occupy installation cells unless the user explicitly removes or packages them through a later, separately audited workflow. It must never silently delete them.

## 48-slot storage

The visual model contains two cabinet banks (24 + 24), but they map to one logical inventory.

Planned data identity:

- `FortressId`: generated GUID at authoritative deployment; immutable across save/load and recovery/redeploy.
- inventory record key: `(world identity, FortressId, container kind)`.
- exactly 48 `Slot` records, each `Contents` and `Count`.
- save is committed before recovery removes any block entity/proxy.

`ComponentInventoryBase` already persists an entity inventory ID and non-empty slots and participates in `SubsystemInventories`. `ComponentChest` is a specialization. The shipped `ChestWidget` derives visible slot count from its fixed XML grid, so it must not be presented as a verified 48-slot UI. The implementation phase needs a custom 48-slot or paged touch widget, Android scale testing, authority checks and network synchronization.

Status: persistence mechanism is **feasible from audited APIs**; custom component/UI and save/reload are **not implemented**.

## Heating

`ComponentVitalStats.UpdateTemperature` periodically asks `SubsystemMetersBlockBehavior.CalculateTemperature` for the terrain cell at the player. That calculation scans real terrain heat blocks (`Block.GetHeat(value)`) with range/occlusion rules. An entity-only visual heater is therefore not a native heat source.

Selected design: the heating-room plinth owns a fortress heat socket backed by a validated terrain heat-source block or a new Builder Assistant block whose `GetHeat` behavior is proven against x25.01.17. Deployment/recovery must add/remove that socket transactionally and restore the exact previous cell. Directly writing player temperature is rejected because it bypasses the native environment calculation.

Status: API path audited; heat block choice and real temperature-rise test are **待验证**.

## Deployment and recovery state machine

```text
Packaged -> Preflight -> Deploying -> Deployed -> Recovering -> Packaged
                         \-> Faulted <-/
```

- **Preflight:** authority, bounds, chunks, footprint, unique ID, collision/socket conflicts.
- **Deploying:** capture prior terrain first; create anchor; add terrain surfaces/sockets and segmented proxies in bounded batches; publish only when complete.
- **Deployed:** storage/heat/interaction enabled; anchor record is authoritative.
- **Recovering:** lock interaction; flush inventory; reject occupied/player-blocked state; remove exact owned resources; restore captured terrain; package state.
- **Faulted:** stop further mutation and retain a recovery journal.

This is independent of ordinary Builder Assistant Undo. A fortress has a long-lived identity, inventory and entity set; ordinary block-edit history is not an adequate lifecycle transaction.

## Save record

Minimum record: schema version, `FortressId`, state, anchor/quarter-turn rotation, exact terrain-before journal, installed socket coordinates, proxy/entity IDs, 48 slots, owner/ACL, and deployment revision. Load reconciliation must be idempotent: duplicate IDs quarantine the later record; missing owned pieces are repaired only by the authority; a partial record enters `Faulted` rather than deleting nearby world content.

## Multiplayer boundary

- Host/server creates IDs, mutates terrain, creates/removes entities, grants storage access and commits inventory.
- Client sends an interaction request and renders acknowledged state; it never independently deploys, heats, restores terrain or writes storage.
- Deployment and recovery use an operation ID/revision so retries cannot duplicate proxies or inventory.
- Storage writes require server-side distance, fortress state, slot bounds and permission checks.
- Joining clients require the exact same `.netmod`: SCNET compares Mod name/version/resource hash. Adding this packaged DAE changes the resource hash.

No custom fortress network event or Android multiplayer behavior was implemented in this whitebox round. Single-player, host and ordinary-client behavior are all **待验证**.
