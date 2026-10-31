# 荒野探险家 / Wilderness Explorer

**0.64.1（2026-10-31，用户实测修复包）**：修复军势面板缩放后背景/按钮边界失配，按实际可用区域统一重排尺寸、坐标和字体，底部收纳/现身按钮保持在面板内。管理操作允许骑乘状态，所有提交显示处理中/失败原因；召唤按钮显示冷却秒数，有乘客的坐骑仍受移除保护。仅编译打包，未运行测试，同房各方需升级。见[修复说明](docs/ABYSS-ARMY-0.64.md)。

**0.64.0（2026-10-31，用户实测候选）**：阿比斯军势上限七只，重复召唤猎犬或可骑乘、速度170%的夜魇战马；死亡蔓延回满全部己方契约生物。新增次位面收纳/现身，解除契约直接消失；修正手机重击蓄力与主机断界斩衔接。按用户要求只编译打包，未运行测试，联机各方需同版。见[使用与验收](docs/ABYSS-ARMY-0.64.md)。

**0.63.0（2026-10-31）**：阿比斯一技能切换虚空/死亡权柄；死亡二技能12秒赋刃、禁用重击、同目标两击转化并满血，三技能24格向外死亡领域；左侧冥界军势召唤猎犬、召回/解除契约。每人6只、世界24只，切回虚空和卸装保留，随世界保存。死亡大招暂共用虚夜4.2秒/CD69。模型和原有普攻重击动作保留，新252协议由主机裁定并保护全部玩家/契约友军。图标使用内置image_gen；[实现、数值与真机验收](docs/ABYSS-DEATH-AUTHORITY.md)。Android/真实多人仍待验证。

**0.62.0（2026-10-31）**：阿比斯大招/二技能/三技能冷却69/3/36秒；普攻改为宽厚黑紫虚空斩痕，蓄力重击释放25格断界斩与4秒残隙，独立于一技能按钮CD，主机验证蓄力与伤害。保留大招/三技能演出和共享星门。Release、原生与138项GPU合成检查通过，Android/真实多人待验证；客户端平A伤害旧限制保持。详见[战斗更新](docs/ABYSS-COMBAT-0.62.md)。

**0.61.1（2026-10-31）**：仅扩大三技能万象破碎的裂界和爆炸视觉，第一人称前方整体开裂、厚片分离再坍缩爆发；保持1.8秒、原伤害与冷却、联机权限、大招和星门。Release/原生回归与126项GPU合成检查通过，Android7723与真实多人待验证，见[范围扩大与对比](docs/ABYSS-WIDE-SHATTER.md)。

**0.61.0（2026-10-31）**：阿比斯「空间权柄」前三技能重制：断界斩100+60与四秒裂隙、全方向18格无界折跃及三秒双门、1.8秒万象破碎；右四战斗/左侧星门的五按钮镜像HUD，三张image_gen专属图标。独立审计251协议处理主机伤害、折跃确认和观察者同步；0.60.0虚夜4.2秒/180CD、永久星门和存档保持。Windows离线/合成GPU验证见[开发与限制](docs/ABYSS-SPACE-V2.md)和[测试记录](docs/TEST-RESULTS.md)；Android7723与真实多人仍待验证，各方须同版。只本地提交，不推送。

**0.60.0（2026-10-31）**：仅重制阿比斯虚夜大招视觉：原世界中黑洞先出现，0.7–3.1秒用原生场景深度让远山、水面、建筑和生物由远到近被侵蚀；3.7秒收缩、3.92秒扩张爆发，4.2秒退出。原伤害/免伤、249/250协议和存档保持。Windows原生GPU合成场景45项像素检查通过，提供连续帧预览；它不是游戏实拍，Android7723与真实联机仍待验证。技术、限制及验收见[虚夜领域](docs/VOID-HORIZON.md)。

**0.59.1（2026-10-31）**：修复“建立永久星门”无响应：打开/刷新列表不再吞掉建门请求；平视时自动在身前寻找安全空地，提前瞄准地面时按瞄准点放置。面板独立显示成功/失败与限流结果，客户端自动同步不覆盖建门回执。原存档和虚夜保持，Android触屏与真实联机待验证，见[共享星门](docs/SHARED-VOID-PORTALS.md)。

**0.59.0（2026-10-31）**：虚夜改为先入领域、形成黑洞、从黑洞向外扩张，4.2秒覆盖完成立即退场并触发128格已加载范围湮灭。取消固定攻击力、被动生物过滤及旧24目标限制；排除所有玩家，保持施法免伤和共享永久星门。Android与真实联机待验，见[虚夜湮灭](docs/VOID-HORIZON.md)。

**0.58.0（2026-10-31）**：多人共享永久星门。所有玩家都能建立、命名、删除和管理；星门随世界保存，每人独立选择目的地。支持随处施法建立永久出发门、操作已有门两种入口，新增普通客户端同步及传送握手。普通客户端普攻/瞬狩支持范围保持；Android与实际多人待验证，见[共享星门](docs/SHARED-VOID-PORTALS.md)。

**0.57.1（2026-10-31）**：虚夜从17秒缩至8秒，2秒建立完整领域、7.2秒终击；保留六次侵蚀及施法免伤。修复房主普攻偏离触摸方向、直接瞄准普通生物不命中、手机Hit+Interact无法打开星门及门下沿无法选中。离线原生回归与Release记录见[测试结果](docs/TEST-RESULTS.md)，Android和实际房间仍待验证。

**0.57.0（2026-10-31）**：统一昆古尼尔、波塞冬、阿比斯、雷神之锤及四圣兽技能入口到查看左侧弧形技能区，左右手镜像和动态避让；普通72、大招88，圆形命中。昆古尼尔image生图服务连续两次连接失败，保留原图，换图未完成。Android触屏与真实联机待验证。

**0.56.0（2026-10-30）**：阿比斯终焉坍缩重做为「虚夜」全景领域，展开后施法者只见地平线与远方巨大黑洞。连续撕裂面、吸积盘前后遮挡与暗核；17秒施法免伤，伤害/牵引无条件排除全部玩家。普通客户端新增主机裁定请求与快照，双方须同版；镰刀补齐持有移速+20%与近战频率+10%。Android、实机画面及真实联机待验证，见[虚夜实现与验收](docs/VOID-HORIZON.md)。

**0.55.5（main，2026-10-30）**：落地"神器默认属性"规范——新神器默认带持有移速 +20%、近战攻速 +10% 与专属物品栏背景。海神三叉戟补上陆地 +20% 移速与 +10% 攻速（水中倍率不变），雷神之锤普通形态也享有 +20% 移速与 +10% 攻速（觉醒形态原本已有）。规则见根 `AGENTS.md`，详见[海神V2](docs/POSEIDON.md)与[测试结果](docs/TEST-RESULTS.md)。

**0.55.4（main，2026-10-30）**：海神三叉戟强化。水中伤害两倍改三倍（一技能42、二技能132、普攻165）；水中移速 +200% 改 +500%（约 6 倍）；大招水龙卷改为环形持续伤害——半径 4.5–11.5 格的蓝色水龙卷环带内每秒 52 点（水中 156），离开即停，18 格潮涌只作演出不再全屏杀伤；二技能沧海怒涛加宽到 15 格、加高到 8 格，真正是一道巨浪扑过去。Android 7723 与真实联机待用户验收，详见[海神V2](docs/POSEIDON.md)与[测试结果](docs/TEST-RESULTS.md)。

**0.55.3（main，2026-10-30）**：按用户反馈修正海神三叉戟两处。大招改为全向水龙卷：判定只看目标与领域中心的水平距离，2.2 秒内 360° 扩散到 18 格（高 10 格）后整片覆盖，演出为闭合潮涌水墙 + 环绕水龙卷群；水中移速 +200% 改由 `WalkSpeed` 增量实现（原实现改的 `SwimSpeed` 玩家移动链路从不读取，因此从未生效）。Android 7723 与真实联机待用户验收，详见[海神V2](docs/POSEIDON.md)与[测试结果](docs/TEST-RESULTS.md)。

**0.55.2（main，2026-10-30）**：修正 0.55.1 未完全解决的 Android 星门存档错误。保存直接生成正确转义的 XML v1，完全移除 `XmlWriter` 调用；读取使用已核实的游戏原生 XML 解析链，移除可疑便捷方法/设置依赖。原有星门身份、名称、坐标与链接保持，不清空存档。Android 7723 进入、保存、退出与重进仍需实机复测，见[测试结果](docs/TEST-RESULTS.md)。

**0.55.1（已由 0.55.2 替代）**：替换 `XmlWriter.Create` 重载后，用户实机仍报 `WriteStartElement(string)` 缺失，修复不完整。

**0.55.0（main，2026-10-30）**：海神V2水盾潮环、方向巨浪与20秒海神领域；水中伤害/护盾翻倍、游速3倍。三张image_gen徽记围绕查看排布，配色改为深海蓝/海青/古金。完整构建已通过，Android与真实联机待用户验收，联机双方须同版。详见[海神V2](docs/POSEIDON.md)。

**0.54.0（main，2026-10-29）**：阿比斯改用 imagegen 四图圆形技能按钮，围绕原生查看控件排列并镜像避障；普攻/蓄力的范围调整为 4.5/5.5 格，增加移握、横扫和弧形刀痕。三连斩时序、模型与持握尺寸保留；Android 7723、实际手感与联机待验证。

**0.53.0（main，2026-10-29）**：虚影瞬狩增加短暂武器旋转、起终点裂隙、线框残影、猎杀弧光及原创音效，单人和本地主机游戏内体验、Android 及联机仍待验证。

**0.52.0（main，2026-10-29）**：终焉坍缩增加四段原创短音效和防重复触发规则，音频失败不影响伤害；游戏内音量、设备性能及联机仍待验证。

**0.51.0（main，2026-10-29）**：改进阿比斯终焉坍缩的分叉裂隙、旋转环和地面符纹，绘制与战斗状态分离；游戏内表现、Android 7723、普通客户端和完整音效仍待验证。详见 [技能状态](docs/ABYSS-V1-SKILLS.md)。

**0.50.0（main，2026-10-29）**：继续开发阿比斯星门，增加目标区块的临时加载、取消清理及到达后保留。此功能尚需游戏内和 Android 7723 测试；普通客户端新技能同步、完整视觉与音效仍未完成。

**0.49.0（main，2026-10-29）**：保留现有内容并合入阿比斯断界斩与四技能开发预览。远距离星门主动加载、普通客户端新技能同步及完整视觉演出尚未完成；Android 7723 和游戏内/联机待验证。见 [技能状态](docs/ABYSS-V1-SKILLS.md) 和 [测试记录](docs/ABYSS-V1-TEST-REPORT.md)。

**0.48.0（main，2026-10-29）**：强化四圣兽耐久、青龙修复和全套生命、白虎伤害与觉醒；安装包为 `荒野探险家.netmod` / `荒野探险家-0.48.0.netmod`。原生耐久分档及尚待实测的内容见 [四圣兽记录](docs/FOUR-SAINTS-V1-AUDIT.md)。

**0.47.0（main，2026-10-29）**：解决 `port/scnet-x25.01.17` 合并冲突，
阿比斯·终焉之镰升级 V0.2，正式使用紫曜巨镰 A3：三段残月大刃、双面凹腔、紫色金属长柄，
1 根骨骼 / 32 网格 / 9,260 三角形。保留三连斩、触屏长按蓄力和四个锁定技能槽，
新增专属紫曜槽位背景，归入 `SCIEPort.Artifacts`。四项技能仍未开放。
保留 main 既有全部功能，Builder Assistant 仍为 0.19.0；合并中已有的吞噬之眼和独立遗迹工程
一并保留，主线坦克的额外组件会触发吞噬之眼安全拒绝，不扩大回收白名单。
安装包为 `荒野探险家.netmod` / `荒野探险家-0.47.0.netmod`。
Android 7723、新模型性能与联机仍待验证；普通客户端阿比斯战斗不支持。
实现、构建证据和设备清单见 [阿比斯记录](docs/ABYSS-REAPER.md) 与 [测试结果](docs/TEST-RESULTS.md)。

**0.46.0（main）**：合并两条主线。其一，合入双人直升机移动端飞行控制 HUD——右下角独立
上升/下降按键，支持持续多点触摸总距、松手平滑回中、自动定高悬停、紧凑座舱遥测与落地滑橇锁；
仅本地授权驾驶位显示，复用既有双座驾驶权限与原生父 Body 一元位置同步，不直写实体坐标。HUD 为
纯程序化矢量控件，不依赖外部贴图。其二，合入四圣兽 V3 独立立体装甲、常驻骨骼附件、网格装备图标
与装备页人物预览修复。方块/衣物索引、GUID、网络包与存档均不变。离线构建已确认，Android 7723
触控、双机联机与装备页实显仍待验证；实现与真机清单见
[直升机 HUD V0.5 审计](docs/HELICOPTER-HUD-V0.5-AUDIT.md) 与
[整套 V3 审查](docs/FOUR-SAINTS-V3-FULL-ARMOR-AUDIT.md)。

## 0.44.1 主线合并（2026-10-28）

将四圣兽V3独立装甲、常驻骨骼附件、网格图标及装备页人物预览修复合入 main 0.44.0；保留全知之眼、波塞冬、独立分类及主题背景。合并衣物块 DrawBlock 与 GetCategory，不改变衣物203/200–203、既有属性技能、碰撞及协议。历史0.42/0.43四圣兽候选版本号与同时期主线神器版本号曾重叠，最终统一为0.44.1；与直升机 HUD 主线合并后统一升为 0.46.0。Android7723及联机由用户验收，仍待验证。

**0.44.0（main）**：全部 53 条物品配置改用独立 Tab：神器、载具、圣兽套装、
生态植物、食材、工业材料、工业部件、建筑装置、生物生成；内部键为 `SCIEPort.*`，
与原版/其它 Mod 同名标签隔离。五件现有神器均有主题背景，四圣兽分别为青玉绿、
冰蓝、银灰浅金、赤焰红；普通衣物和普通物品原色保留。
以后的安装包统一叫 `荒野探险家.netmod`，同时生成 `荒野探险家-0.44.0.netmod`；
包内 SCIEPort.dll/PackageName、物品 ID 与存档不改。规范已写入根/目录 AGENTS 与
[物品展示规范](docs/INVENTORY-STANDARD.md)。离线检查已确认；Android 触屏/背景实显仍待验证。

**0.43.0（main）**：新增海神三叉戟·波塞冬 V1：原创金蓝三叉戟、水下呼吸、
游泳速度 ×2.6、8 秒冷却潮涌冲锋、26 秒冷却沧海怒涛，以及潮汐音效、触屏徽记、
水幕/浪尖/水纹法阵。创造栏“武器”获取，无耐久，不可放置；巨浪不修改水方块或地形。
目标 Engine 模型导入与离线构建已确认；Windows 游戏内、Android 7723 与多人手感
待验证。操作、修改清单、限制与真机清单见 [POSEIDON.md](docs/POSEIDON.md)。

**0.42.0（main）**：已合入全知之眼（The All-Seeing Eye）V0.1，新增原创观星仪神器、
万象解析环境/方块页及独立本地玩家 HUD；保留 main 0.41.0 的既有功能与四圣兽调整。
创造栏“神器”获取，数据采集只读。模型真实引擎导入与离线检查已确认，Android 7723
与多人实际表现待验证。操作及真机清单见 [OMNISCIENT-EYE.md](docs/OMNISCIENT-EYE.md)。

0.43.1：修复装备页人物预览仍显示旧衣物网格。外层人物预览现在接入四件缓存立体装甲，并跟随原生人物旋转、骨骼动画和界面缩放；内层皮肤预览保留原生行为。世界渲染、四件几何、贴图、属性和协议未改。编译/ABI与包检查已确认，Android装备页显示和拖动旋转待用户验收。

0.43.0：四圣兽V3四件齐全的统一验收候选包。青龙冠、玄武龟蛇重甲、白虎虎首护腿、朱雀羽翼战靴均使用独立立体网格、持续骨骼挂接与独立装备图标；整套3292三角面，保留原衣物基础层、全部属性技能、协议与碰撞。目标引擎导入、男女骨骼矩阵/静态净空和Release检查已确认；游戏内及Android/联机统一交用户验收。见 [整套V3审查](docs/FOUR-SAINTS-V3-FULL-ARMOR-AUDIT.md)。

0.42.0：四圣兽V3第一阶段候选包，万里青龙新增独立680三角面DAE龙冠、常驻头骨附件及独立网格图标。冠体默认开启，保留原有衣物基础层和全部属性/协议。目标引擎导入、男女头骨基矩阵校验和Release编译已确认；游戏内多姿态、Android与联机由用户实测，均待验收。其余三件立体附件尚未实施。资源、入口和调试方法见 [V3青龙冠审查](docs/FOUR-SAINTS-V3-CROWN-AUDIT.md)。

> 游戏内模组显示名称为「荒野探险家」（英文 Wilderness Explorer，作者 zyan，v0.36.0 起）；`SCIE-Port` 仍作为仓库内的工作区目录名与内部标识保留，`PackageName` 等内部 ID、GUID、网络包与存档兼容逻辑不变。下方保留全部既有开发记录（版本历史自 v0.28 起向下罗列，v0.29–v0.35 的完整开发记录见 `docs/COMPATIBILITY.md` 与 `docs/` 下各审计文档）。`.netmod` 的品牌与打包规则见 [`docs/PACKAGING-RULES.md`](docs/PACKAGING-RULES.md)，自 v0.40.3 起由构建脚本在打包前后各校验一次。

Version 0.40.6 adds a fallback registration for the four Four Saints outfits. x25.01.17
merges every mod `.clo` into the vanilla clothing XML inside `ClothingBlock.Initialize`,
but `GetFiles` only logs and continues when a single file fails, so a dropped merge left
the outfits unregistered and invisible in both the clothing tab and the world. After the
blocks initialize, any of the indices 200-203 that the `.clo` did not deliver are created
from code; entries the `.clo` did provide are never overwritten. Each item is guarded on
its own and a texture that fails to load falls back to the first one that does, with the
exact path logged. Clothing data ids, layers, stats, block ids and save layout are
unchanged.

Version 0.40.5 fixes the reported Four Saints equip/display failure and restores native
throwing sights for Gungnir. x25.01.17 only equips clothing when the incoming piece's
`Layer` is strictly greater than the last item already worn in that slot; Xuanwu/Baihu
used layer 4 and Zhuque used layer 1, so normal torso, leg and foot clothing could reject
them while the usually-empty head slot still accepted Qinglong. All four divine pieces
are now outer layer 10, with IDs 203/200-203, textures, stats, packet 245 and saves
unchanged. Gungnir retains its reliable direct `PlayerInput.Aim` clock, but now calls the
same per-frame `ComponentAimingSights.ShowAimingSights` path as native throwable weapons.
Its two badges are icon-only: the skill-name plate/caption is removed while cooldown and
energy feedback remain. Release/package validation passed; Android/7723 equip rendering,
touch sights and multiplayer remain pending device confirmation.

Version 0.40.4 fixes the x25.01.17 sparse-clothing crash without changing the
published Four Saints values. Native clothing ends at index 37, while Four Saints use
200-203; the stock creative enumerator sorted null slots 38-199 before filtering them
and aborted block initialization. A block-203-compatible subclass now filters nulls
before the same stable display-order sort. IDs 203/200-203, textures, stats, packet 245
and saves are unchanged. The exact failure was reproduced before the fix and no longer
appears in an x25.01.17 Windows startup smoke test; Android/7723 world entry still needs
device confirmation.

Version 0.40.3 makes the `.netmod` branding contract enforceable instead of convention.
`Build-NetMod.ps1` now validates, before staging and again from the finished archive, that
the in-game name is 荒野探险家 / Wilderness Explorer, the author is zyan, `PackageName`
and `AssemblyName`/`RootNamespace` are still `SCIEPort`, the csproj and `modinfo.json`
versions match, `THIRD-PARTY-NOTICES.md` ships, and no CSV display name carries a port or
preview suffix. Block indices, clothing data, GUIDs and packet IDs are untouched; only the
description now opens with the bilingual brand name. The rules live in
`docs/PACKAGING-RULES.md` and `AGENTS.md` so future packaging follows the same contract.

Version 0.40.2 is a diagnostic build: natural fox/rabbit seeding is switched off
(`CreatureWorldSpawn.NaturalSpawnEnabled = false`) while the reported "world never
finishes loading" case is isolated. That spawner is the only mod code that runs inside
terrain generation and the feature is still documented as 待验证. Nothing else changed;
block IDs (903-948, 995-1000), clothing data 200-203, entity templates, packet layouts
and all gameplay values are untouched. Re-enabling the spawner is a one-line change once
a fresh world is confirmed to load with it disabled.

Version 0.40.1 fixes a crash that could stop a brand-new world from finishing its
loading screen. `Game.Random.Int(min, max)` is inclusive on both ends, and the natural
creature spawner rolled chunk cells with `Int(0, 16)`; the roll returned 16 roughly once
in seventeen attempts and read past the chunk arrays (`y + x * 256 + z * 256 * 16`), which
is the `IndexOutOfRangeException` seen in `CreatureWorldSpawn.FindSurface`. The same
misreading made the spawn chance roll `Int(0, 100)`, where a 100 is rejected by
`ChanceMet` and silently disables the spawn. Both ranges now come from
`EcologyRules.LastChunkCellIndex` (15) and `LastChanceRoll` (99), and every chunk probe is
clamped and bounds-checked. Spawning is also deferred: the chunk hook only validates and
queues, and entities are created from `SubsystemUpdate` once the chunk has left its
contents stage, so entity creation can never run inside terrain generation. The spawner
disables itself for the session after five unexpected failures instead of touching every
remaining chunk. Block/creature IDs, entity templates, biome rules and caps are unchanged.

Version 0.40.0 fixes three Gungnir issues in one runtime/presentation pass. Both skill
names are now engraved into a nameplate band inside each badge, so no device DPI,
safe area or wide hotbar can hide them. Long-press throwing is driven by the mod's own
`PlayerInput.Aim` clock instead of the engine aim callback: x25.01.17 re-arms that
callback only 1.4 s after a completed hold and latches `m_isAimBlocked` whenever a
behaviour consumes `InProgress`, which is what made the spear stop throwing after a
few uses. A flight-lease watchdog plus "recall instead of reject" launching remove
every phase lock, and a throw now needs only a ~0.35 s press. Eternal Judgment was
rebuilt as a genuine sky-falling giant spear: a vertical tear opens in the canopy, the
4.6x blade condenses 40 blocks overhead, plunges blade-first with afterimages and a
full-height trail, then stands buried with embers, three shockwaves and a dissolving
shaft. The 1.4 s damage tick, radius, damage values, block ID 948 and packet 244/245
layouts are unchanged.

Version 0.39.0 is the Four Saints V2 art pass. All four native 64x64 clothing
overlays are rebuilt as authored pixel armor with distinct Qinglong, Xuanwu,
Baihu and Zhuque materials. Existing package 245 state now drives a compact
bone-following four-glyph visual atlas; gameplay values and packet layout remain
unchanged. See `docs/FOUR-SAINTS-V2-ART-AUDIT.md` for engine limits and device tests.

Version 0.38.2 replaces Gungnir's green inventory/hotbar slot treatment with a
restrained obsidian center and antique-gold bevel. The existing breathing cycle
remains, but its RGB variation is reduced to 6/8 levels so the weapon model stays
dominant. Mjolnir colors and the shared slot save/restore lifecycle are unchanged.

Version 0.38.1 extends Xuanwu Armor with immunity to both verified direct cold
damage causes from `ComponentVitalStats.UpdateTemperature` (localization indices
25 and 26). It does not freeze body temperature, change weather, suppress heat,
or suppress the separate `ComponentFlu` disease/damage path. Clothing data IDs
200-203 and Baihu packet 245 remain byte-for-byte compatible.

Version 0.38.0 adds four independent native clothing items: Qinglong Crown,
Xuanwu Armor, Baihu Greaves and Zhuque Boots (clothing data 200-203 on native
clothing block 203). Their authority runtime provides max-health/regen and fall
immunity, final attack reduction/drowning immunity, movement/jump/attack-speed
bonuses plus a 5-second Baihu awakening, and fire/magma immunity plus pre-death
rebirth. Packet 245 carries authenticated Baihu requests and bounded snapshots;
the 76-unit touch HUD is positioned from the real Look/hotbar rectangles.
Android/7723 and two-device multiplayer remain pending device verification.

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

Version **0.42.0** on `main` integrates **全知之眼 / The All-Seeing Eye V0.1**
from feature commit `896469b` (original development build 0.14.0 on `port/scnet-x25.01.17`).
Find the artifact in creative inventory → **神器**. Use it to open **万象解析**:
environment and manually triggered block analysis (8 blocks); creature and diagnostic
pages are disabled extension entries. The panel toggles **全知视界**, which remains
visible after switching items. The HUD reads coordinates/height/chunk at 0.2 seconds
and column temperature/humidity game levels at 1 second. It has no terrain/entity
write operations, durability use, network packets or world-placement behavior.

The original opaque astrolabe model imports in the target Engine as **1 bone / 25 meshes /
784 triangles**. Release/build checks are confirmed; Windows in-game and Android 7723
rendering, touch operation and multiplayer behavior remain **待验证**. Detailed design,
commands and the device test checklist: [OMNISCIENT-EYE.md](docs/OMNISCIENT-EYE.md).

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

## 海神三叉戟 V2（0.55.5）

水御潮环15秒CD、刷新5点水盾/扩散击退14伤害；沧海怒涛11秒CD/44伤害、15格宽8格高的巨浪；海神领域60秒CD、20秒蓝色世界/全向水龙卷/环带内每秒52点持续伤害，领域内前两技能CD为7/5秒。全部伤害水中三倍（42/132/165），有效护盾水中仍为两倍。持有属性：陆地移速+20%、近战攻速+10%；游泳时移速+500%（约6倍）。海神领域以自身为中心、半径4.5–11.5格的水龙卷环带持续伤害，2.2秒潮涌只作演出，环带内目标每秒掉血并按秒被拉向中心。三个image_gen技能徽记围绕“查看”排布。联机双方需同版；Android及真实联机待验收。详见[海神V2](docs/POSEIDON.md)。
