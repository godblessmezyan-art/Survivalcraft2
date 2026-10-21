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

Version 0.10.0 is ATV v0.3. The compact four-wheel visual is replaced by a
project-created tandem off-road motorcycle while ID 923, `SCIEPortATV`, its
entity GUID and the v0.2 driving rules remain stable. The driver keeps the
vehicle's native mount. A reusable seat manager associates both riders directly
with the vehicle body, avoiding duplicate Mount components, nested collision
anchors and per-frame player teleportation. Offline driving and host-player driving compile;
ordinary clients never write vehicle velocity or another player's position. A
remote client can be a passenger, but a remote client acting as driver is not
supported until a server-authoritative input/correction protocol is implemented.
All Android two-device behavior remains pending real-device verification.

Version 0.9.0 adds an independently implemented, project-created low-poly T-34
style tank prototype. It uses the native ground body, mount and rider chain
rather than any boat physics: single-player touch input supports forward,
reverse, hull steering, braking, gravity, collision and a conservative
0.3-block smooth-rise limit. The rebuilt model contains hull, tracks, ten road
wheels, turret, hatch, gun and muzzle meshes grouped under three articulated
bones, with an opaque eight-color palette. Occupied driving uses one explicit
longitudinal/lateral calculation and preserves airborne inertia instead of
fighting native ground drag. Independent persisted turret/gun angle fields are
ready for a later aiming/fire feature. There is deliberately no fuel, weapon,
multiplayer authority or Android compatibility claim yet.

Version 0.9.2 responds to Android testing of the base tank and first cannon build. The complete model
is enlarged by 35 percent with a matching 3.6×2.1×5.9 native body, smooth-rise
clearance is increased to 1.05 cells, and the attack/hit touch action fires a
native musket-ball projectile at 42 cells/second. The turret and gun bones track
the aiming ray; an owned shell produces a pressure-18 non-incendiary explosion
when removed after impact, with a 1.6-second cooldown and horizontal recoil.
Firing, articulation, explosion damage and one-cell climbing remain pending
Android verification and should first be tested in a disposable world.

Tank v0.3.1 makes turret aim follow the active camera instead of the mounted
creature eye transform. Shells now start beyond the tank body's actual AABB
without invoking the native hand-projectile owner correction, which pulled the
first v0.3 shell back inside the vehicle. Damage ownership is restored after
creation, and a 1.5-cell arming distance suppresses residual near-tank blasts.
These corrections pass Release/static tests but still need Android verification.

Version 0.11.0 upgrades the authorized SCIE steam boat into a correctly sized
two-person speedboat while preserving block ID 999, entity name and GUID. The
Y-up model is lifted to ground level and enclosed by a 2.3×1.75×5.6 body; driver
and passenger sit side-by-side at X=-0.42/+0.42. The shared seat manager assigns
the first rider as driver, the second as passenger and rejects a third. Native
`ComponentBoat` is deliberately absent because it accepts input from every rider;
the replacement controller consumes only driver input while `ComponentBody`
retains buoyancy, drag, collision and persistence. Android rendering, dual-device
riding and save/reload remain pending real-device verification.

Version 0.12.0 adds a separate project-created side-by-side two-seat off-road
car at content ID 925. Its open 4x4 body, four wheels, seats and roll cage fit a
2.4×1.55×4.0 native collision body; the seats are 0.9 cells apart. The first
rider drives, the second is a non-driving passenger, and a third is rejected by
the shared seat manager. Ground propulsion is independent from boat physics and
uses native gravity, collision and 1.05-cell smooth rise. Android appearance,
driving, save/reload and host-driver/client-passenger behavior remain pending.

Version 0.12.1 fixes the remaining tank self-explosion path found by Android
testing. The old 1.5-cell fuse only measured travel from the muzzle, so a fast
shell could arm in one frame and detonate beside the large vehicle after a
near-ground or near-wall tap. Explosion eligibility now also measures the
rounded explosion-cell center against the tank's current collision AABB and
suppresses every blast within an 8-cell safety envelope. Android confirmation
of the fix remains pending; the 0.12.1 Release build and package guards pass
against the fixed x25.01.17 Windows assemblies.

Version 0.12.2 changes tank aiming from click-to-hit to visible mechanical
traversal. The active camera continuously commands the turret (1.2 rad/s) and
gun elevation (0.65 rad/s); a touch hit now only triggers the shot, whose
trajectory is calculated from the current articulated barrel angles. The tank
entity no longer contains the native depleting `ComponentDamage`, making its
armor non-depleting against animals and explosion damage. Main-gun blast
pressure is raised to 2160, the exact `LargeGunpowderKegBlock` value audited
from the fixed x25.01.17 `Content.scpak`, and therefore uses the native block
destruction/particle/sound pipeline. The firing-vehicle exclusion is widened to
12 cells. Release/static/package tests pass; all new behavior remains pending
Android verification in a disposable world.

Version 0.12.3 replaces the tiny flat musket-ball projectile with an internal
ID 926 tank shell. It reuses the target runtime's `Models/Snowball` geometry
without redistributing that asset, draws the sphere solid black at a 3.5 render
multiplier, and hides it from the creative inventory. A dedicated block
behavior receives the native first-collision callback, queues the tank blast at
the hit cell and marks the shell for removal. The native routine can still
calculate an unused reflected velocity later in that same update, but the shell
is removed before another frame can display or simulate a rebound. Release/static/package checks
pass; size, color and first-impact behavior remain pending Android verification.

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
Before implementing a cannon or any other large-vehicle weapon, read
`docs/LARGE-VEHICLE-WEAPONS.md`; it records the native musket owner-correction
trap, safe muzzle calculation, fuse rules and mandatory Android first-shot tests.
Porting rules and licensing requirements are in `AGENTS.md`,
`docs/PORTING-GUIDELINES.md` and `THIRD-PARTY-NOTICES.md`.

Agents taking over development should start with `docs/AGENT-HANDOFF.md`.
