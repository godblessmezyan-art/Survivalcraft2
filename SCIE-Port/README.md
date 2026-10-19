# SCIE-Port

Independent migration workspace for authorized third-party Mods targeting
Survivalcraft NET 2.4 `Net_x25.01.17`. It is deliberately separate from
Builder Assistant, whose scope remains automatic building and blueprints.

The original repository is used as a read-only input at the pinned commit
recorded in `UPSTREAM-BASELINE.md` and is excluded from version control. The
user has stated that explicit permission was obtained for the ported vehicle
assets. The public license-text caveat and attribution are recorded in
`THIRD-PARTY-NOTICES.md`.

The current milestone contains a SCNET-native loader, one creative-only test
material, authorized previews of all four SCIE transport entities:
SteamBoat, Airship, Train and Carriage. Version 0.5.1 also adds the required
rail block and uses x25-native entity, mount, body and input interfaces. It is
an early single-player runtime probe, not a completed SCIE port; steam fuel,
carriage coupling/storage and multiplayer authority are still pending.

Version 0.6.0 adds the authorized Kelly Expansion 1.1.0 plant-resource port:
14 blocks/items, the plant atlas, fruit model, bounded new-chunk ecology,
seasonal berries, soybean growth, cactus/acorn behavior and rain-driven
mushrooms. Inventory entries retain the exact upstream Chinese names and must
not gain parenthesized port labels. These runtime behaviors remain pending
Android/7723 verification.

Version 0.7.0 adds a first low-risk SCIE industrial furniture batch: steel
gear, steel wheel, gear mould, wheel mould, industrial piston and cylinder.
They are static placeable decorations with four horizontal orientations. No
machine, inventory, recipe, gas, chemistry or circuit behavior is claimed.
The five Z-up source models are packaged as Y-up derivatives; the cylinder was
already Y-up. Android placement, rendering and save/reload remain pending.

Version 0.7.2 fixes two Android-facing rendering faults found by user testing:
the Kelly atlas is shipped as PNG rather than unsupported WebP, and the
airship now declares a packaged white entity texture. All ported vehicle
entities also apply a small non-zero emission floor so terrain light sampling
cannot turn their world models completely black. Android verification remains
pending.

Version 0.8.1 adds the first independent land-vehicle preview: a project-created
low-poly, single-seat four-wheel ATV. It reuses only the verified native entity,
body collision, mount/rider and persistence infrastructure; it does not reuse
boat buoyancy or rail alignment. The controller is deliberately single-player
only and has no fuel, multiplayer authority or suspension model. Android/7723
spawn, riding, collision, camera and save/reload remain pending real-device tests.

Version 0.8.2 is ATV v0.2 stabilization. The model is rebuilt as nine independent
Y-up meshes with baked coordinates, opaque four-color UV palette, seat and
handlebar; the inventory path no longer duplicates every triangle with reversed
winding. While occupied, the controller disables native `6*dt` ground drag and
uses one frame-rate-independent drive/grip calculation, preserves horizontal
inertia when briefly airborne, and emits one diagnostic line every five seconds.
Native `ComponentBody` continuous smooth rise is set to 1.05 cells and the body
box now covers the whole visible model, allowing a one-cell step while retaining
terrain, entity and overhead collision checks. Android results remain pending.

Version 0.8.3 fixes the Android world-model blank result reported for 0.8.2.
The x25.01.17 renderer exposes seven model-transform slots by default, while the
old DAE imported as ten bones. The nine geometries now share one identity root
bone, and the inventory path enumerates all meshes instead of relying on unique
mesh names. Android confirmation of the rebuilt package remains pending.

Version 0.9.0 adds an independently implemented, project-created low-poly T-34
style tank prototype. It uses the native ground body, mount and rider chain
rather than any boat physics: single-player touch input supports forward,
reverse, hull steering, braking, gravity, collision and a conservative
0.3-block smooth-rise limit. The hull, turret and gun are separate named model
nodes; hull movement state and independent persisted turret/gun angle fields
are ready for a later aiming/fire feature. There is deliberately no fuel,
weapon, multiplayer authority or Android compatibility claim yet.

Version 0.5.1 packages Y-up derivatives of the authorized SteamBoat, Airship
and Train models. The Airship collision body is limited to its cabin so the
native 2.5-block mount search can reach the pilot seat.

```powershell
.\scripts\Fetch-Upstream.ps1
.\scripts\Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
.\scripts\Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
```

See `docs/TEST-RESULTS.md` before interpreting any compatibility claim.
Before creating another vehicle asset, read `docs/VEHICLE-MODEL-GUIDE.md`; it records
the Android-safe single-bone DAE layout, inventory/world rendering differences,
texture rules, target-Engine import checks and device acceptance procedure.
Porting rules and licensing requirements are in `AGENTS.md`,
`docs/PORTING-GUIDELINES.md` and `THIRD-PARTY-NOTICES.md`.

Agents taking over development should start with `docs/AGENT-HANDOFF.md`.
