# 荒野探险家 / Wilderness Explorer

> 游戏内模组显示名称为「荒野探险家」（英文 Wilderness Explorer，作者 zyan，v0.36.0 起）；`SCIE-Port` 仍作为仓库内的工作区目录名与内部标识保留，`PackageName` 等内部 ID、GUID、网络包与存档兼容逻辑不变。下方保留全部既有开发记录（版本历史自 v0.28 起向下罗列，v0.29–v0.35 的完整开发记录见 `docs/COMPATIBILITY.md` 与 `docs/` 下各审计文档）。

Version 0.37.0 corrects Gungnir's x25.01.17 touch-aim return contract so a
long hold remains active until release. Gungnir also receives a distinct
dark-teal/old-gold inventory skin and a delayed gold number for its divine
follow-up hit. Rune Blessing and Eternal Judgment cooldowns are now 8 and 50
seconds. True Mjolnir grants 20% ground movement and 10% melee speed while
equipped. Offline Release/package validation passes; Android touch timing and
two-device authority remain pending.

Version 0.28.1 fixes the V3.2 loader regression: vehicle activation keeps
network package 242 and Skywrath now uses the unique package 243. A package-ID
uniqueness guard prevents this whole-mod startup failure from recurring.

Version 0.28.0 rebuilds the separate awakened Stormhammer as Skywrath V3.2.
Equipping it now activates a bone-following cyan avatar aura, layered weapon
glow, authoritative five-point lightning retaliation and one native physical
plus one controlled lightning melee record. The HUD contains exactly two
skills: the radial Thunderous Judgment and the target-locked Myriad
Tribulation. Package 243 carries cast IDs, server game time and target IDs;
the original Stormhammer and all vehicle features remain unchanged. Static ABI,
Release and package checks pass; Android combat values, visual quality and real
multiplayer timing remain pending. See `docs/SKYWRATH-V3.2-AUDIT.md`.

Version 0.27.0 integrates the original Stormhammer and its separate awakened
Skywrath V2 form on top of the 0.26.0 helicopter presentation release. It adds
original weapon models, authored UV/glow/effect textures, three PCM sounds,
bounded lightning effects, exactly three awakened skill buttons and
server-authoritative skill sequencing through package 242. The base form stays
independent. Android 7723 presentation, touch performance and real multiplayer
synchronization remain pending; see `docs/TEST-RESULTS.md`.

Version 0.26.0 upgrades the existing two-seat helicopter to presentation V0.4
without replacing its flight, seat, spawn or network paths. The new clean-room
model is a 758-triangle red/white light helicopter with an opaque dark canopy,
two visible seats, dashboard, tail boom, fins, skids and navigation-light
geometry. Six bones drive a visual-attitude root plus independent solid/blur
main and tail rotors; five merged meshes keep draw count bounded. Rotor state,
pitch/roll damping and blur selection are frame-rate independent and visual
only. A generated mono PCM WAV loop uses the target `SubsystemAudio` APIs with
distance attenuation and explicit disposal. The target x25.01.17 Engine imports
the model as 6 bones/5 meshes and decodes the WAV as 22050 Hz mono PCM. Release,
database, package and 41 helicopter-rule assertions pass. Android 7723 world/
inventory rendering, audio lifecycle, save/reload and real two-client behavior
remain pending. See `docs/HELICOPTER-V0.4-AUDIT.md`.

Version 0.25.0 adds a clean-room two-seat helicopter V0.1 as an isolated
`Features/AirVehicles` slice. ID 941 creates a dedicated persistent entity with
one native mount, explicit driver/passenger roles, configurable gravity and
flight limits, automatic unattended landing, native body collision/save paths,
and independently animated main/tail rotor bones. The passenger is never a
control source. The target x25.01.17 Engine imports the generated model as
3 bones/17 meshes and all Release, database, package and 28 helicopter-rule
assertions pass. Android 7723 creation, touch controls, rendering, save/reload
and real two-client synchronization remain pending; strict server-authoritative
flight is not supported. See `docs/HELICOPTER-V0.1-AUDIT.md`.

Version 0.22.3 fixes the invisible carriage. The authorized source DAE uses a
single polygon index for position, normal and UV while declaring all three
inputs at offset zero; the target x25.01.17 Collada loader throws from
`LoadPolygons` on that layout. Packaging now emits a deterministic compatible
derivative with offsets 0/1/2 and interleaved indices, and verifies the result
with the target Engine as 3 bones/1 `obj1` mesh. Rail-vehicle inventory drawing
also enumerates every imported mesh. Release/static checks pass; Android 7723
world and inventory rendering remain pending real-device verification.

Version 0.22.2 corrects the T-34 weapon lifecycle. Recoil is applied immediately
after a successful shot and after the frame's drive integration, the gun uses a
realistic -5/+25 degree mechanical pitch range, and every first physical shell
impact explodes regardless of distance while non-impact removal stays silent.
Finite native durability is restored (`Hitpoints=1`, `AttackResilience=600`),
with no fire component. Release/static checks pass; Android 7723 behavior
remains pending real-device verification.

Version 0.22.1 fixes the remaining two-seat rider/input conflict on ATV,
speedboat and off-road car. Driver movement is captured before native rider
locomotion, both riders retain distinct seat offsets, horizontal view remains
independent, and mounted players keep native aim, attack, interaction and block
use. It builds on the v0.22 native mounted-parent multiplayer path. Release,
ABI, rule, database and package checks pass; Android 7723 two-device behavior
remains pending real-device verification.
The original Stormhammer legendary weapon was produced as a formal art
slice: a 1368-triangle/30-mesh Y-up COLLADA model, authored 1024 UV atlas,
dedicated glow and particle maps, three original PCM sounds, separate inventory/
first-person/third-person/drop transforms, bounded lightning effects, charged
strike and three-link chain damage. The target Engine importer and Release
package checks pass. Windows in-game rendering, Android 7723 hand fit/performance
and multiplayer effect synchronization remain pending; see
`docs/STORMHAMMER-AUDIT.md`.

Version 0.22.0 adds the first complete multiplayer-driving pass for every current
vehicle: ATV, two-seat speedboat, off-road car, airship, train, carriage and T-34.
It follows x25.01.17's native mounted-parent body synchronization, keeps the
driver first in two-seat body ordering, suppresses passenger state echo, and
routes remote tank fire through a server-validated request so clients never
create explosive shells. Release/static checks pass; Android 7723 host/client
behavior remains pending real-device verification. See
`docs/VEHICLE-MULTIPLAYER-AUDIT.md`.

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

Version 0.13.0 adds the first creature migration probe: a manually spawned fox.
The reference SCAPI projects are pinned and license-audited, but their glTF/GLB,
animation controller and SCAPI binaries are not shipped because the fixed SCNET
runtime only exposes DAE/JSON/OBJ readers and native procedural creature models.
`SCIEPortFox` inherits the target database's real Wolf template and therefore
reuses native AI, locomotion, health, sound and four-legged animation components.
Its clean-room Y_UP model imports as six bones and thirteen meshes. There is no
natural spawning, client-side entity authority or multiplayer compatibility
claim in this phase; Windows gameplay and Android 7723 remain pending.

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
# 苍穹之怒 V2.0（0.24.0）

0.24.0 在普通雷神之锤之外新增独立觉醒物品“苍穹之怒”（ID 942）：恰好三个右侧弧形技能按钮、服务端权威冷却/伤害/雷印/领域/雷劫调度，以及包 242 的多人演出同步。正式资源包括 1604 三角觉醒锤、108 三角雷矛、1024 UV/发光图与三枚技能图标。完整数值、ABI 和真机验收见 `docs/SKYWRATH-V2-AUDIT.md`。Android 与多人运行仍为待验证，不以离线构建替代。
# 昆古尼尔 V5.2 / 0.35.0 神王敕令·天穹审判

0.35.0 将永恒裁决扩展为九秒四阶段天穹演出：暗青天顶、暗金地平线、汇聚远景色带、天空符文、三层法环、真实巨型神枪、金白落地脉冲与 7–9 秒自然恢复。天空 Hook 与妙尔尼尔雷暴显式加权，不覆盖任何一方状态；只对本地施法者改变天空，旁观者继续从既有包 244 快照看到世界几何。同步修复普通客户端长按投枪偶发不发射，并删除技能图标外的矩形框。146 项 Gungnir、667 项 Stormhammer、资源、16 XDB、net472 Release 和包结构离线通过；Android/7723 画面、性能、多点触控与双机联机待验证。详见 [V5.2 审查与真机清单](docs/GUNGNIR-V5.2-SKY-JUDGMENT.md)。

# 昆古尼尔 V5.1 / V5.1b / 0.34.0

0.34.0 在原生 Look 锚定 HUD 上增加青金/白金视觉区分、就绪呼吸与大招外环，并把投枪去程和回程拆成两套明确语言：去程是锐利白芯+金色外辉，回程是更长的暖金轨迹+螺旋+符文+轴向环；枪体同时叠加能量脊和逐段亮起的模型符文。130 项规则/布局、真实模型/资源、16 XDB、net472 Release 与 99 条目包均通过离线验证。数值、Phase 时序、ID948、包244和包字段未改；Android/7723 多点触控、帧率、截图及双机联机仍待验证。详见 [V5.1 审查与真机清单](docs/GUNGNIR-V5.1-AUDIT.md)。

# 昆古尼尔 V4 / 0.33.0 HUD 与演出重构

0.33.0 将两技能 HUD 改为读取目标原生 Look/快捷栏实际矩形的触屏安全布局，新增透明八字形符文图集、TexturedBatch3D 双层符文、枪身逐段能量流和分层神佑/裁决演出。105 项规则/布局、真实模型导入、资源、16 XDB、net472 Release 与包内容离线检查通过；ID948、包244、协议、伤害/范围/冷却和投枪行为未改。严格远程运动权威仍不支持，Android/7723 触控、截图、性能与双机联机仍待验证。详见 [0.33 审查与真机验收](docs/GUNGNIR-HUD-VFX-0.33-AUDIT.md) 与 [V4 基线审查](docs/GUNGNIR-V4-AUDIT.md)。
