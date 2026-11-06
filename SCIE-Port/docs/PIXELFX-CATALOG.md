# PixelFX 固定版本效果目录

神器特效选型前先读[开发规范](PIXELFX-DEVELOPMENT-STANDARD.md)。本表是离线检索索引，不代表这些效果已移植或Android已验证。

来源：[patch-gg/pixelfx 固定提交](https://github.com/patch-gg/pixelfx/tree/a84159688af3ca37a4be05a0d422b94451b2e24e)，从该提交的Git tree提取 `src/fx/<类别>/<名称>.js`，排除下划线辅助模块，共244种/21类；目录日期2026-11-06。不从浮动main继承接口/许可证结论。
当前已纳入工程：`lightning/charge-up`、`lightning/lightning-strike`、`lightning/thunderclap`，及0.78.0新增的`holy/blessing`、`holy/sunburst`、`holy/smite`、`water/splash`、`water/wave-crash`、`shadow/dark-explosion`，离线/Windows原生GPU隔离结果见[多神器专项](PIXELFX-ARTIFACTS-0.78.0.md)，Android/真实联机仍待验证。其它条目只有名称/路径线索，采用前必须取得相应固定源码并检查依赖、meta、许可、导出和实际效果。

```powershell
rg -n 'holy|shadow|water|beam|charge|slash|portal' SCIE-Port/docs/PIXELFX-CATALOG.md
# 例如 holy/smite 对应上游 src/fx/holy/smite.js
```

| 类别 | 名称（与类别组合为效果ID） |
|---|---|
| arcane | `arcane-beam`、`arcane-missiles`、`arcane-orb`、`blink-in`、`blink-out`、`magic-circle-cast`、`mana-burst`、`portal`、`rune-circle`、`spell-charge`、`star-fall`、`time-stop` |
| critters | `bat`、`bird`、`butterfly`、`eyeball`、`fish-school`、`flying-skull`、`frog`、`ghost`、`jellyfish`、`slime`、`snake`、`wisp` |
| explosions | `barrel-explosion`、`big-explosion`、`cartoon-pow`、`dirt-explosion`、`firework-burst`、`firework-rocket`、`grenade`、`implosion`、`nuke`、`plasma-explosion`、`small-explosion`、`smoke-bomb` |
| fire | `brazier`、`campfire`、`candle`、`fire-breath`、`fire-ring`、`fire-tornado`、`fireball`、`flame-pillar`、`flamethrower`、`meteor`、`spirit-flame`、`torch` |
| holy | `blessing`、`divine-shield`、`feather-burst`、`halo`、`heal`、`heal-aoe`、`holy-pillar`、`resurrection`、`smite`、`sunburst` |
| ice | `blizzard-vortex`、`freeze`、`frost-breath`、`frost-nova`、`ice-crystal`、`ice-lance`、`ice-shatter`、`ice-spikes`、`icicle-fall`、`snowflake-burst` |
| impacts | `blunt-hit`、`bullet-impact`、`crit-hit`、`ground-slam`、`heavy-landing`、`hit-flash`、`hit-spark`、`impact-frame`、`parry`、`ricochet`、`shield-block` |
| lightning | `ball-lightning`、`chain-lightning`、`charge-up`、`electrified`、`lightning-strike`、`plasma-ball`、`spark-arc`、`storm-cloud`、`tesla-coil`、`thunderclap`、`zap-hit` |
| melee | `claw-swipe`、`dagger-flurry`、`double-slash`、`hammer-smash`、`heavy-cleave`、`scythe-reap`、`spear-sweep`、`spin-slash`、`sword-slash`、`thrust`、`uppercut`、`whip-crack` |
| moves | `arrow-shot`、`dash-strike`、`earth-stomp`、`fireball-throw`、`ground-pound`、`holy-smite`、`ice-lance-throw`、`lightning-call`、`meteor-call`、`pistol-shots`、`rocket-launcher`、`sword-combo` |
| nature | `acid-splash`、`boulder`、`earth-spike`、`healing-bloom`、`leaf-storm`、`poison-cloud`、`quake`、`root-snare`、`spore-burst`、`thorns`、`tornado`、`toxic-puddle`、`vine-whip` |
| pickups | `chest-open`、`coin-spin`、`collect-burst`、`crystal-shard`、`gem-spin`、`heart-pickup`、`key-glint`、`loot-beam`、`potion`、`power-star`、`xp-orb` |
| projectiles | `arrow`、`bullet-tracer`、`cannonball`、`energy-disc`、`flare`、`homing-missile`、`laser-bolt`、`magic-missile`、`muzzle-pistol`、`muzzle-shotgun`、`muzzle-smg`、`plasma-shot`、`rocket` |
| scifi | `emp-blast`、`energy-core`、`force-field`、`glitch`、`hologram`、`jetpack`、`laser-grid`、`scanner`、`teleporter-pad`、`tractor-beam`、`warp-in`、`warp-out` |
| shadow | `black-hole`、`curse`、`dark-explosion`、`eclipse`、`grasping-hands`、`hex-sigil`、`life-steal`、`necro-skulls`、`shadow-flames`、`shadow-tendrils`、`soul-drain`、`void-orb` |
| spawn | `coin-drop`、`disintegrate`、`dissolve-burn`、`egg-hatch`、`poof`、`respawn-beam`、`shatter-death`、`soul-rise`、`spawn-portal`、`summon-rise` |
| status | `bleeding`、`burning`、`charmed`、`confused`、`cursed`、`frozen`、`haste`、`petrified`、`poisoned`、`rage`、`sleep`、`slowed`、`stunned` |
| transitions | `blinds`、`burn-away`、`crt-off`、`diamond-wipe`、`glitch-wipe`、`iris`、`pixel-dissolve`、`shatter-wipe`、`slash-wipe`、`star-iris` |
| ui | `achievement`、`coin-collect`、`combo-counter`、`confetti`、`countdown`、`crit-number`、`damage-number`、`exclamation`、`health-bar-hit`、`heart-lost`、`level-up`、`quest-complete` |
| water | `bubbles`、`drip`、`fountain`、`geyser`、`rain-splash`、`ripples`、`splash`、`water-jet`、`water-orb`、`waterfall`、`wave-crash`、`whirlpool` |
| weather | `aurora`、`blizzard`、`cherry-blossoms`、`clouds`、`falling-leaves`、`fireflies`、`fog`、`god-rays`、`rain`、`shooting-stars`、`snow`、`storm` |

索引不能代替观看帧序列：同名beam、shield、portal可能依赖二维人物/地面/镜头姿态，不可直接用于任意3D方向。技能图标仍用image_gen；实体、生物、界面、屏幕转场类也不能仅凭目录名作为世界场景精灵。
