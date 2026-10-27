# 测试结果
# 0.38.1 四圣兽 V1.1 玄武严寒免疫（2026-10-27）

- x25.01.17 实际路径：`ComponentVitalStats.UpdateTemperature` 在体温≤0时以 `fName,25` 调用 `ComponentHealth.Injure(1f,...)`，体温<3时每10秒以 `fName,26` 调用 `Injure(0.05f,...)`；两者均进入现有 `OnCreatureInjure` 权威入口。过热为 `fName,35`，流感为独立 `ComponentFlu.fName,4`，均未加入免疫。
- 实现：玄武 ID 201 的现有溺水判断扩为三个精确相等原因；没有模糊文本匹配，没有修改 Temperature/Wetness/天气/季节，也没有改变衣物 200～203 或包 245 格式。
- 四圣兽离线套件扩为 25 条断言，增加严寒双路径、卸装装备门、非目标原因、青龙/白虎/朱雀常量回归；完整构建结果见本节最终包记录。
- 完整构建：环境 ABI、全部既有离线套件、17 XDB、net472 Release（0 warning / 0 error）和 107 条目包通过。包 3,482,940 bytes，SHA-256 `D1E1F8E6393740C9DE46F72172650A89CDF9AF74054C8A28BD90EB181325FED3`；modinfo/AssemblyVersion `0.38.1`/`0.38.1.0`。
- Android 7723 雪地穿脱、湿身降温、流感保留及主客机权威行为仍为待验证。

# 0.38.0 四圣兽神装 V1（2026-10-27）

- ABI 取证：目标 `ComponentHealth` 为 1.0=10 点、原生 `Heal` 与生命条均硬限 1.0；衣物块 203 的数据号为 8 位，原版最高 37。本模块用衣物数据 200～203 和包 245。
- 四圣兽离线套件：玄武减伤边界、白虎 1.2/2.0 攻频与一次 1.5 倍伤害、妙尔尼尔乘法组合、+1 格跳高公式、1280×720/1920×1080/1.35 UI 缩放布局全部通过。
- 完整构建：`scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` exit 0；全部既有套件、17 XDB、net472 Release 与新增衣物/纹理/包内容通过。
- 包：3,482,760 bytes / 107 entries，SHA-256 `56F9690086BD2B9988B41C0F68221CDF32233ED4CC6BA2AF8BB49AD32AC7FA05`；modinfo/AssemblyVersion `0.38.0`/`0.38.0.0`。
- Android 7723、真机触控、伤害路径和双机联机均为待验证；没有把离线结果标记为真机成功。详见 [四圣兽 V1 审查](FOUR-SAINTS-V1-AUDIT.md)。

## 0.37.0 双神器操作与强化（2026-10-27）

- ABI 取证：x25.01.17 `ComponentPlayer` 明确在 `AimState.InProgress` 收到 `true` 时取消瞄准；原生投掷物返回模式已逐分支比对。
- Gungnir：150 项通过；新增 8/50 秒冷却、0.4 秒二段间隔和长按不消费输入断言。既有 1280×720、1920×1080、1.25×/1.5× UI 布局均无 Look/快捷栏碰撞。
- Stormhammer：671 项通过；新增移动 ×1.20、攻击频率 ×1.10、0.66→0.60 秒门限及时间戳调整断言。
- 完整构建：环境/ABI、资源/模型、全部既有离线套件、16 XDB、net472 Release 和打包均通过。
- 包：3,469,440 bytes / 100 entries，SHA-256 `A99251BCC465BED727EE8D4E53AA4DAA076AED5FE47953088DE58A05A412D494`；modinfo/AssemblyVersion `0.37.0`/`0.37.0.0`。
- 未启动 Android 或双客户端；真机触控、数字颜色、属性手感和联机权威结果均为待验证。

## 0.35.0 昆古尼尔 V5.2（2026-10-27）

- 环境/资源：固定 x25.01.17 哈希/API/net472 检查通过；真实 Engine 导入 Gungnir 1 bone / 41 meshes / 1176 triangles，透明图集/UV/音频/CSV 通过。
- Gungnir：146 项通过，覆盖九秒生命周期、7–9 秒平滑恢复、减少闪光、雷暴+神王天空并存、到期/断线/死亡清理、三档预算及可靠投枪三态。
- Stormhammer：单独运行 667 项通过；其伤害、技能和雷暴状态未修改。
- 完整回归：构建脚本内全部既有套件通过；16 XDB 通过；net472 Release 0 warning / 0 error。
- 打包：3,468,275 bytes / 100 entries，SHA-256 `F82C8F4309BD412FB6A0866B35ED72346ECABE293FFCEA2BD12CAE1A95B20A94`；modinfo `0.35.0`，AssemblyVersion `0.35.0.0`。
- Windows 游戏、Android 7723、白天/夜晚/雨天观感、普通客户端投枪与双神器并发仍为待验证。

## 0.34.0 主线合并复验（2026-10-27）

- 合并范围：保留昆古尼尔 V5.1/V5.1b、妙尔尼尔 V6/展示反馈，以及该功能分支历史中的便携堡垒美术更新；唯一文本冲突 `modinfo.json` 已合并为共同描述。
- 规则测试：Gungnir 130 项、Stormhammer 667 项、Portable Fortress 319 项通过；构建脚本中的其余既有离线套件全部通过。
- 环境/构建：固定 `E:\SCNET\SCNETx25.01.17` 哈希与 ABI 检查通过；16 XDB `DATABASE VALIDATION PASSED`；net472 Release 0 warning / 0 error。
- 合并包：`SCIEPort.netmod` 3,465,578 bytes / 100 entries，SHA-256 `40C79967AE3FEEFF3785F4036BC934F53030BA88A8FB006E582AA944A9C70969`；modinfo `0.34.0`，AssemblyVersion `0.34.0.0`。
- Windows 游戏运行、Android 7723 和双机联机仍为待验证；本次复验没有启动游戏或连接设备。

## 0.34.0（2026-10-26）

- 15 组离线套件全绿（Stormhammer 667）；net472 Release 0 error 0 warning；15 XDB PASS。
- 包：3,021,714 B / 92 条目，SHA-256 `C6DDD1462E0D8B596D149EF05430A976365199F05B85C89427C5367A63526502`，modinfo 0.34.0。
- 构建期间修了两个守卫自身的问题：V3.2 战斗常量表仍要求 12f（V6 已改 8f）；新守卫块引用了后文才定义的 `$awakenedBehavior`（改为块内自读）。
- 真机观感待验证。
## 0.33.0 雷神之锤·妙尔尼尔 V6（2026-10-26）

- 环境：已确认。`Test-Environment.ps1` exit 0：三程序集 SHA-256 与 x25.01.17 一致，API 1.42 / SC 2.4.10.6 / net472。
- 离线规则：已确认。`tests/SCIEPort.Stormhammer.Tests` **667 断言**通过（V5 为 580）。新增覆盖重雷层：数量、时间排序、窗口边界、距离带 8–18、锥角 ±34°、`Great` 标记、同 cast id 逐位复现、不同 cast id 改变节奏、零数量与空窗口返回空、天幕落雷不得被标记为 `Great`。
- 编译：已确认。net472 Release **0 警告 / 0 错误**。
- 模型契约：已确认。`Models/StormhammerAwakened.dae` 条目已从 `Build-NetMod.ps1` 删除（该文件不再有加载点）；`ThunderSpear.dae` 契约保留。
- 数据库：已确认。`DATABASE VALIDATION PASSED`，两个 Stormhammer XDB 均通过父级/内部 GUID 校验。
- 构建守卫新增：`JudgmentCooldown = 8f`、`MainStrikeTime = 0.28f`、`BuildGreatBoltSchedule`（规则层与视觉层）存在、重雷层由 `state.MainSpawned` 门控、觉醒块必须加载 `Models/Stormhammer`、`DriveCorona` / `GetScene` 存在、HUD 不得含技能名与描边、CSV 中文名正确。
- 打包：已确认。`artifacts/SCIEPort.netmod` **3,020,616 字节 / 92 条目**，SHA-256 `A5CCED971D76F30B3132A66AF8913DD2127B6AA62A15FE19B786E2AEA45D0142`，modinfo `0.33.0`；包内已不含 `Models/StormhammerAwakened.dae`。
- Windows 游戏运行：待验证。未启动游戏。
- Android 7723：待验证。未真机运行：锤体光环、电弧密度、低画质帧率、0.28 s 落雷手感、重雷负载、HUD 精简后的可读性。
- 联机：待验证。未做双设备测试。
## 0.32.0 便携堡垒美术重构与入口修复（2026-10-26）

- 环境：已确认。`Test-Environment.ps1` exit 0：三程序集 SHA-256 与 x25.01.17 一致，API 1.42 / SC 2.4.10.6 / net472，260 个原生方块常量、最大 ID 263。
- 资产生成：已确认。`python scripts/New-PortableFortressArt.py` → 387 部件 / 11 网格 / 13470 三角面；图集 1024×1024（16 区）、`FortressGlass.png` 与 `FortressGlow.png` 各 64×64（含真实半透明像素）。
- 模型导入：已确认（目标程序集）。`Test-PortableFortressModel.ps1 -GameDir E:\SCNET\SCNETx25.01.17` 打印 `PASS`；真实 `Engine.Media.ModelData.Load(Stream)` 得 1 骨骼 / 11 网格 / 13470 三角面；包围盒 X `-5.5..5.5`、Y `0..9`、Z `-6.5..6.5`。
- 离线规则：已确认。`tests/SCIEPort.PortableFortress.Tests` **319 断言**通过，其中入口相关新增：地板代理不得越过 `DoorInnerZ`、最后一级踏面与 `DeckTop` 齐平、玻璃代理存在且厚度 ≤ 0.09、入口条带目标高度为 0.36 且限门宽、抬高后不再返回目标。
- 编译：已确认。net472 Release 0 error / 0 warning。
- 构建守卫新增：图集尺寸/不透明度/颜色数、玻璃与暖光贴图透明度、`StairRise=0.435`、`DoorInnerZ=6.20`、`FortressZone.Glazing`、`EntrySillMaxZ=RearFaceZ`、`MaxRiseSpeed=4.5`、`DoorHalfClearanceX`、`BlendState.AlphaBlend`、`DepthStencilState.DepthRead`、诊断开关存在、`EmissionColor` 存在。
- Windows 游戏运行：待验证。未启动游戏。
- Android 7723：待验证。未真机运行：入口无需跳跃、外观材质、玻璃可读性、夜间可读性、楼梯与竖井手感、帧耗时、存读档。
- 联机：待验证。未做双设备测试。
- 预览：`docs/portable-fortress/previews/` 共 7 张，由生成器软件渲染产出（非游戏截图）。
- 打包：已确认。`artifacts/SCIEPort.netmod` **3,047,028 字节 / 93 条目**，SHA-256 `45C82E53CA318EECF3909B6315AA4024161D2AA54CB0F9EA648733DE280C19A0`，modinfo `0.32.0`；包内含 `PortableFortress.csv`、`PortableFortress.xdb`、`PortableFortress.dae`、`FortressCore.dae`、`PortableFortress.png`、`FortressGlass.png`、`FortressGlow.png`、`FortressCore.png`。


## 0.34.0 昆古尼尔 V5.1 / V5.1b（2026-10-27）

- 环境/ABI：固定 `E:\SCNET\SCNETx25.01.17` 哈希、版本 2.4.0.0、API 1.42、SC 2.4.10.6、net472 与保留 ID 检查通过。
- HUD/视觉规则：Gungnir 130 项通过；1280×720、1920×1080、1.25×/1.5× UI 与左手镜像均无 Look/快捷栏碰撞。新增低/中/高拖尾、螺旋、符文、环预算和提示生命周期断言。
- 资源：脚本可复现；真实 Engine 导入 1 bone / 41 meshes / 1176 triangles；透明技能图、8 字形透明符文图集、UV/音频/CSV 检查通过。
- 完整回归：`Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` exit 0；全部既有规则套件、16 XDB 通过；net472 0 warning / 0 error。
- 打包：3,094,805 bytes / 99 entries，SHA-256 `CE23FA8C6933E1953D60DF7BD3009BFF157E6A76389E76533A9E7620B141B447`；modinfo `0.34.0`，AssemblyVersion `0.34.0.0`。
- Android 7723、真机多点触控/截图/帧率、主机与普通客户端演出仍为待验证；详见 `docs/GUNGNIR-V5.1-AUDIT.md`。

## 0.33.0 昆古尼尔 HUD / 符文演出重构（2026-10-27）

- ABI/原生布局：`Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'` exit 0；固定 Windows x25.01.17、API 1.42、SC 2.4.10.6、net472。只读取证真实 `GameWidget.xml` 与 `ComponentGui/Widget/ComponentInput`，确认 Look 160×160 逻辑容器、左右手镜像、GlobalBounds/ScreenToWidget 以及 ViewWidget 攻击输入链。
- HUD/规则：Gungnir **105** 项通过。日志坐标：1280×720 → 神佑 `(962,609.3,72,72)`、裁决 `(1022,513.3,88,88)`；1920×1080 → `(1602,969.3,72,72)` / `(1662,873.3,88,88)`；1.25× UI（1024×576 逻辑）→ `(706,422,72,72)` / `(766,326,88,88)`；1.5× UI（1280×720 逻辑）同首组。四组 Look/快捷栏重叠均 false，左手镜像断言通过。此为离线逻辑检查，不是 Android 截图。
- 资源：`New-GungnirAssets.py` 可复现；`Test-GungnirAssets.ps1` 用真实 Engine 导入 1 bone / 41 meshes / 1176 triangles，并通过透明 512×256 技能图、透明 1024×256 八字形图集、UV/CSV/音频存在性检查。
- 完整回归：`Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` exit 0；Kelly、ATV、越野车、水上载具、直升机、坦克、生态、作物、激活、堡垒与 Gungnir 测试全绿；16 XDB `DATABASE VALIDATION PASSED`；net472 0 warning / 0 error。
- 打包：`SCIEPort.netmod` **3,091,265 bytes / 99 entries**，SHA-256 `B451A23802425F83B68B53C7B1E0DE09C6A69FE965B55A8874FB9C7F86295830`；modinfo `0.33.0`，AssemblyVersion `0.33.0.0`。
- Windows 游戏内 / Android 7723 / 双机联机：待验证。本轮未启动游戏、未连接设备，不将离线结果写成真机兼容。截图、触控、性能、清理与联机清单见 `docs/GUNGNIR-HUD-VFX-0.33-AUDIT.md`。

## 0.32.0 合并候选（苍穹之怒 V5.0 + 昆古尼尔 V4，2026-10-27）

- 环境/ABI：已确认。执行 `scripts/Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'`，程序集 `2.4.0.0`、API `1.42`、SC `2.4.10.6`、目标框架 `.NET Framework 4.7.2` 与保留 ID 检查通过。
- 规则测试：已确认。昆古尼尔 **88/88**、苍穹之怒 V5 **580/580**；完整构建中 Kelly、ATV、越野车、水上载具、直升机、坦克、生态、作物、载具激活与移动堡垒等既有离线套件全部通过。
- Release / 数据库 / 包：已确认。执行 `scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` exit 0；net472 编译通过，16 份 XDB 验证通过；`.netmod` **3,009,161 字节 / 98 条目**，SHA-256 `12830CC3B0D43D77582B96DA720E727703DE627595B5E43BCF3B66760C9CF5B8`；modinfo `0.32.0`，DLL AssemblyVersion `0.32.0.0`。
- Android 7723 / 联机：待验证。本轮未执行真机触屏、帧率或双机同步测试，不据此标记兼容。

## 0.31.0 苍穹之怒 V5.0「雷神降世」（2026-10-26）

- 状态机：已确认。`ActivateThunderGod` 在 `StartUltimate` 内授予 12 s 状态并按实体 id 覆盖（不累加）；`UpdateThunderGod` 每帧校验 `IsAddedToProject / Health>0 / IsEquipped`，到期或失效即广播 `ThunderGodEnd`；`Dispose` 清空 `m_thunderGod / m_pendingJudgment` 与视觉状态。
- 伤害通道：已确认。`DealElementalPower(base, multiplier)` 与 `DealAttackPower(base, multiplier = 1)` 为唯一入口；普攻雷电与雷霆万钧传 `ThunderGodPowerMultiplier`，物理 / 反伤 / 主雷 / 余震传 `1f`。`DiagnosticDamage` 改为输出 `base / x / final / health before->after`。
- 冷却：已确认。仅刷新雷霆万钧（`SetCooldown(id, ThunderousJudgment, 0f)`）并广播 `Accepted(Value=0)`；大招自身 55 s 不变。
- 视觉：已确认（源码）。超载电弧间隔 0.42×、芯 0.13 / 辉光 0.36、双条跨身电弧；0.42 s 足迹雷纹；锤头 `IsOverchargedNear` 2.5 格检测 → 白蓝高亮 ×1.45；首次雷霆万钧 `Step=1` 触发 14 条 / 芯 0.70 / 辉光 1.95 / 额外 10.5 格符文；主雷芯 2.45 / 辉光 5.6 / 分叉 7 / 寿命 1.05 s + 两圈纯视觉冲击波环。
- 天幕：已确认（580 断言）。五段密度 1.15–11.60 s，高画质 145 道（27 近 + 118 远），中/低 ×0.62/×0.34；断言覆盖总数、排序、窗口边界、高潮 > 屏息×4、长尾跨过 10.5 s、确定性、`DueStrikes` 跨阶段计数、null/空 phases 容错。
- 天地异变：已确认（源码）。`StormReleaseTime = 12.0f` + 1.6 s 淡出尾；`GetStormBlend` 仅 `LocalCaster`；`GetFlash` 受 `ReducedFlashes` 压制。
- 新增构建守卫：已确认。6 条 V5 守卫（12 s / ×2 / 只刷雷霆万钧 CD / 物理 x1 / 反伤 x1 / 主雷与余震不得引用倍率 / 天幕与超载与 HUD 存在）。
- 全量回归：已确认。Kelly 11 / ATV 35 / Offroad 20 / Watercraft 16 / Helicopter 41 / Tank / Ecology 47 / Corn 26 / Bamboo 25 / Flax 29 / Sunflower 33 / Apple 49 / VehicleActivation 22 / PortableFortress 291 / **Skywrath V5 580**。
- 编译/数据库：已确认。net472 Release **0 warning / 0 error**；15 个 XDB `DATABASE VALIDATION PASSED`。
- 打包：已确认。`artifacts/SCIEPort.netmod` **2,650,795 字节 / 91 条目**，SHA-256 `B3C5FFBC508F343A800ACF4E9A4E2AF5E3DCECCDD0175420E07AF9390593F3F5`；modinfo `0.31.0`。
- Windows 游戏运行：待验证。未启动游戏。
- Android 7723：待验证。强势期手感、超载密度、HUD 计时、低画质帧率均未执行。
- 双机联机：待验证。旁观者天空、主雷同步、两端伤害一致性均未执行。
- 伤害实测：未验证。仅静态日志格式与离线数值表已确认，未在真机观察 `health before->after`。

## 0.30.0 苍穹之怒 V4.0（2026-10-26）

- 审计：已确认。定位旧版"蓝色光团"根因为 `StormhammerParticleSystem.CreateArc`（18–44 个 0.035–0.075 相机朝向粒子）与 `EmitSkyBolt` 同源粒子弧；并逐项取证 `SubsystemDrawing`/`IDrawable`/`TexturedBatch3D.QueueQuad`/`BlendState.Additive`/`DepthStencilState.DepthRead`/`RasterizerState.CullNone`/`SubsystemSky.CalculateFog`/`ComponentBody.ApplyShaking`/`SubsystemAudio.PlaySound`。
- 几何：已确认。新增 `LightningRules`（相干值噪声路径、端点锚定、xorshift32 确定性、寿命/宽度/伤害/天幕调度）与 `LightningScene`（48 条池化、芯+辉光双层带、主雷交叉面、角宽下限、雾融合、符文、爆闪、每帧四边形预算）。
- 离线规则：已确认。`tests/SCIEPort.Stormhammer.Tests` **277 断言**通过：路径端点/连续性/位移上限/同 seed 复现/异 seed 变化、寿命曲线、宽度收细、两张伤害表边界、天幕数量与分带范围与排序与 `DueStrikes` 跳过语义。
- 资源：已确认。`LightningBeam.png`(128×16)、`LightningRune.png`(256×256)、`Strike.wav`(1.65 s)、`Rumble.wav`(3.30 s)，均可由 `scripts/New-StormhammerV4Assets.py` 重建；构建脚本校验贴图尺寸与 RIFF/WAVE 头。
- 新增构建守卫：已确认。拦截 `UltimateTarget` 与 `EmitSkyBolt` 回潮，强制 `QueueRibbon`/`LightningRune`/`BuildStormSchedule`/`UpdatePendingJudgment`/`LightningRules.TribulationDamage` 存在。
- 全量回归：已确认。Kelly 11 / ATV 35 / Offroad 20 / Watercraft 16 / Helicopter 41 / Tank / Ecology 47 / Corn 26 / Bamboo 25 / Flax 29 / Sunflower 33 / Apple 49 / VehicleActivation 22 / PortableFortress 291 / **Skywrath V4 277** 全部 exit 0。
- 编译/数据库：已确认。net472 Release **0 warning / 0 error**；15 个 XDB `DATABASE VALIDATION PASSED`。
- 打包：已确认。`artifacts/SCIEPort.netmod` **2,648,230 字节 / 91 条目**，SHA-256 `D2EE8300C12BC21DC69A5EBC0D5516B2EC8B0ADE146D738CC18B1A21CEDA3BFA`；modinfo `0.30.0`。
- Windows 游戏运行：待验证。未启动游戏，未观察实际渲染。
- Android 7723：待验证。主雷宽度/亮度、天幕密度与帧率、爆闪舒适度、镜头抖动、触屏 HUD 均未真机执行。
- 双机联机：待验证。主雷同步、旁观者天空不受染色、天幕不外泄、余震不重复结算均未实测。

## 0.29.0 便携堡垒（2026-10-26）

- 环境：已确认。`Test-Environment.ps1` exit 0：三程序集 SHA-256 与 x25.01.17 公开包一致，API 1.42 / SC 2.4.10.6 / net472，260 个原生方块常量最大 ID 263。
- 模型：已确认（目标导入）。`scripts/New-PortableFortressModel.ps1` 重新生成 `Assets/Models/PortableFortress.dae`（11 网格 / 1248 三角面 / 104 部件）；`scripts/Test-PortableFortressModel.ps1` 打印 `PASS`，真实 `Engine.Media.ModelData.Load(Stream)` 得到 1 骨骼 / 11 网格，包围盒 11 × 13 × 9。
- 资产：已确认。`scripts/New-PortableFortressAssets.ps1` 生成 `FortressCore.dae`（14 部件）与 `PortableFortress.png` / `FortressCore.png`。
- 离线规则：已确认。`tests/SCIEPort.PortableFortress.Tests` **291 断言**通过，覆盖：代理数量与尺寸上限、代理不越出外壳、地板顶面恒为 0.36、门洞/观察窗/楼梯井/舱口/屋顶通道不被封死、四向旋转群律、143 格占地唯一、锚点七格偏移、预检七种拒绝、体素重叠与安全边距、楼梯与竖井目标高度、抬升速率与跳跃豁免、回收门控与事务限额。
- 全量回归：已确认。Kelly 11 / ATV 35 / Offroad 20 / Watercraft 16 / Helicopter 41 / Tank / Ecology 47 / Corn 26 / Bamboo 25 / Flax 29 / Sunflower 33 / Apple 49 / VehicleActivation 22 / **PortableFortress 291** 全部 exit 0。
- 编译/数据库：已确认。net472 Release **0 warning/0 error**；15 个 XDB `DATABASE VALIDATION PASSED`。首轮校验曾报两处真实缺陷并已修正：三个目录未复用原版 GUID；`SimpleModel` 的 `InheritanceParent` 误写为 `...ecb5a2b7baf4`（正确为 `...ecb5a2a7baf4`）。
- 新增构建守卫：已确认。构建脚本校验三个方块行的分类/碰撞/可放置标志、类名在全仓 CSV 中唯一、944–946 编号未漂移、`FloorTop = 0.36`、`MaxSegmentHalfDiagonal = 4`、地形容量 4 格、`WorkType.Client` 拒绝、预检先于事务创建。
- 打包：已确认。`artifacts/SCIEPort.netmod` 为 **2,323,919 字节 / 87 条目**，SHA-256 `89FCCE29A77224F98D915D3D645DF6BEDBA3D80903964296DF008CA54C4159CB`；包内含 `PortableFortress.csv`、`PortableFortress.xdb`、`PortableFortress.dae`、`FortressCore.dae`、`PortableFortress.png`、`FortressCore.png`；modinfo Version=`0.29.0`。
- Windows 游戏运行：待验证。未启动游戏。
- Android 7723：待验证。展开、进门、走楼梯、上屋顶、存读档、帧耗时均未真机执行。
- 联机：待验证。未做双设备测试。

## 0.26.0 双人直升机 V0.4（2026-10-25）

- 前置环境：直接运行 `Test-Environment.ps1` 首先因机器遗留 `SCNET_GAME_DIR=E:\...`（该盘不存在）失败；显式使用 `-GameDir 'D:\SC'` 后发现 PowerShell 7 的 byte-array Assembly load 无法解析同目录 Engine 依赖，脚本改为对已完成哈希校验的绝对路径使用 `Assembly.LoadFrom`。修正后环境检查 exit 0。
- ABI：已确认。固定三程序集哈希、API 1.42、SC 2.4.10.6、net472、260 个原生方块常量继续通过；新增反射确认 `SubsystemAudio.CreateSound/CalculateListenerDistance/CalculateVolume` 及 `Sound` 的状态、音量、音调、循环、Stop/Dispose 生命周期。
- 规则：已确认。`SCIEPort.AirVehicles.Tests` 从 28 增至 **41 断言**，新增四状态、正负/多圈角回绕、最短 yaw 差、前后俯仰符号、侧倾限幅、阻尼和 rotor blur 阈值；exit 0。
- 全量回归：已确认。Kelly 11 / ATV 35 / Offroad 20 / Watercraft 16 / Helicopter 41 / Tank / Ecology 47 / Corn 26 / Bamboo 25 / Flax 29 / Sunflower 33 / Apple 49 / VehicleActivation 22 全绿。
- Release/数据库：已确认。命令为 `$env:PATH='C:\Users\85196\AppData\Local\Microsoft\dotnet;'+$env:PATH; $env:DOTNET_ROLL_FORWARD='Major'; .\SCIE-Port\scripts\Build-NetMod.ps1 -GameDir 'D:\SC' -Configuration Release`；使用本机 9.0.300 SDK（测试目标 net8 以 Major roll-forward 运行），net472 主工程 **0 warning/0 error**，12 个 XDB `DATABASE VALIDATION PASSED`。
- 模型：已确认。`Helicopter.dae` 为 758 triangles；目标 `Engine.Media.ModelData.Load` 实际导入 **6 bones/5 meshes**，骨骼和枢轴名全部通过构建守卫。96×16 PNG 为全不透明十二色。静态正交/斜视诊断图用于检查轮廓与部件位置，但不计作游戏渲染通过。
- 音频：已确认（文件/目标解码）。`SCIEPortHelicopterLoop.wav` SHA-256 `842D8FF3D950813E85A6F5CD2E3C0639EA69070CA01766AD47184BEFD309B8EC`；真实目标 `SoundData.Load(Stream)` 解码为 1 channel / 22050 Hz / 22050 samples。Android OpenAL 播放未执行。
- 打包：已确认。`artifacts/SCIEPort.netmod` 为 **520,065 字节 / 64 条目**，SHA-256 `23060B72D8D959265E32B8F3A9A86C73F6977CE4201575FDD7CE1276D5177DD1`；包内含新增 WAV，modinfo=`0.26.0`，DLL AssemblyVersion/FileVersion=`0.26.0.0`。
- Windows 游戏运行：待验证。未启动游戏，不能声称创造栏、世界渲染、声音、飞行或保存重载通过。
- Android 7723：待验证。模型/贴图/旋翼/姿态/触屏飞行/声音生命周期、昼夜和帧率都未真机执行。
- 两台真实客户端：待验证。位置、座位、运动、乘客权限、远端旋翼/姿态/声音、上下机、断线重连均未执行。

## 0.25.0 双人直升机 V0.1（2026-10-25）

- 审计：已确认。先完成 `docs/HELICOPTER-V0.1-AUDIT.md`，逐项取证 Block+CSV+XDB+Spawn 注册、Body 驱动/碰撞/保存、单 Mount 双座、`SetBoneTransform` 动态部件、原生父 Body 网络路径及其非严格服务器权威边界，再进入实现。
- 独立实现：已确认（源码/编译）。新增 `Features/AirVehicles`，未复制坦克/越野车接地推进；ID 941、实体 `SCIEPortHelicopter`、新 GUID、一个 Mount、双座管理、可配置飞行 Drive、Local/Server 自动降落与深水副作用均通过源码和 XDB 守卫。
- 离线规则：已确认。`tests/SCIEPort.AirVehicles.Tests` **28 断言**通过，覆盖平滑积分、旋翼加减速、显式重力/悬停、上升/下降、自动降落、水平前后限速、高度限制和座舱包围盒。
- 模型/贴图：已确认（文件/目标导入）。`Helicopter.dae` 为 `Y_UP`、恰好三个命名骨骼和 17 个 clean-room cuboid；目标 x25.01.17 `Engine.Media.ModelData.Load` 实际结果 **3 bones/17 meshes**。`Helicopter.png` 为 48×8、全不透明、六个调色板颜色。资源可由 `scripts/New-HelicopterAssets.ps1` 重建。
- 环境/全工程构建：已确认。运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\Dev\Survivalcraft2\SCIE-Port\scripts\Build-WithPinnedSdk.ps1' -GameDir 'D:\SC' -Configuration Release`，其先执行 `Test-Environment.ps1`；目标程序集哈希保持兼容记录值，260 个原生方块常量最大 ID 263，ID 941 未占用。
- 全部测试/数据库：已确认。Kelly 11 / ATV 35 / Offroad 20 / Watercraft 16 / **Helicopter 28** / Tank / Ecology 47 / Corn 26 / Bamboo 25 / Flax 29 / Sunflower 33 / Apple 49 / VehicleActivation 22 全绿；net472 Release **0 warning/0 error**，12 个 XDB `DATABASE VALIDATION PASSED`。
- 打包：已确认。最终 `artifacts/SCIEPort.netmod` 为 **506,514 字节 / 63 条目**，SHA-256 `C424E021AD2DA65E255FD796EA77B47E0E2E7C7F5BEE5856CDF6833BB35F1736`，含 Helicopter CSV/XDB/DAE/PNG；包内 modinfo Version=`0.25.0`，DLL AssemblyVersion/FileVersion=`0.25.0.0`。
- Windows 游戏运行：待验证。未启动游戏验证创造栏、生成、驾驶和保存。
- Android 7723 单机：待验证。创造模式生成、触屏起飞/升降/悬停/移动/降落、双座、乘客不可控制、安全上下机、碰撞、退出重进均须在可丢弃新世界实测。
- 两台真实 Android 客户端：待验证。须用相同最终 SHA-256 包测试房主/客户端互换 Driver/Passenger、第三人拒绝、位置/座位/运动、跨区块、驾驶员离开自动降落、断线重连。未通过前不得标记联机已实现。
- 严格服务器权威：不支持。V0.1 沿用原生驾驶员父 Body 状态源。

## 0.22.3 车厢不可见修复（2026-10-24）

- 根因复现：已确认。固定 `D:\SC` 的 `Engine.Media.ModelData.Load` 直接读取授权上游
  `Minecart.dae`，在 `Engine.Media.Collada.LoadPolygons` 抛出
  `ArgumentOutOfRangeException`。源 polylist 的 `VERTEX/NORMAL/TEXCOORD` 全为
  `offset=0`，且 6540 个顶点引用只给出 6540 个共享索引，不符合目标加载器要求的交错输入。
- 派生模型：已确认。打包脚本保持忽略区上游文件字节不变，只在 staging 生成兼容派生：输入
  offset 固定为 `0,1,2`，每个共享索引按三输入展开，最终 19620 个索引。派生 DAE 为
  492,888 字节，SHA-256
  `526F18CE199D6682D4FD8DA254FFA279761CDB6E51A2D7A1A93BF21AA237CD76`。
- 目标引擎导入：已确认。构建守卫用 x25.01.17 的真实 `ModelData.Load` 导入包内派生模型，结果
  为 3 bones/1 mesh，mesh 名 `obj1`，低于七骨骼世界渲染预算；导入异常或布局回退会直接中止打包。
- 物品路径：已确认（源码/编译）。`RailVehicleBlock.Initialize` 改为枚举 `model.Meshes` 并逐个
  烘焙其父骨骼绝对变换，不再依赖单一 `FindMesh`；火车和车厢共用该安全路径。
- 环境与全量回归：已确认。根目录及 SCIE-Port 的 `Test-Environment.ps1 -GameDir D:\SC`
  通过固定三程序集哈希、API 1.42、SC 2.4.10.6 与 net472 检查；Kelly 11、ATV 35、越野车 20、
  快艇 16、Tank、Ecology 47、Corn 26、Bamboo 25、Flax 29、Sunflower 33、Apple 49 全部通过。
- Release / 数据库 / 包：已确认。在隔离的干净 worktree 中仅带入车厢修复执行
  `Build-NetMod.ps1 -GameDir D:\SC -Configuration Release`，exit 0，net472 0 warning/0 error，
  11 份 XDB 验证通过，59 个包条目；产物 429,800 字节，SHA-256
  `75978D899701EF3B7670B2EB31206C1031B418A0D44D766AFE0A16E8D7147770`。DLL
  Assembly/FileVersion 为 `0.22.3.0`，构建时 ProductVersion 为
  `0.22.3+2963a91e684062416779e6cd88fe54602b8acf79`。
- Windows 游戏运行：待验证。未启动游戏。
- Android 7723：待验证。需删除旧包、冷启动后分别确认创造栏车厢图标与生成后的世界实体可见，
  再测试日照/阴影/室内、上车驾驶、保存重进、区块重载以及房主/普通客户端观察一致性。

## 0.22.2 坦克武器与耐久修复（2026-10-24）

- 目标取证：已确认。固定 `D:\SC` x25.01.17 的 `Content.scpak` 中原版 Damage 模板声明 `Hitpoints` 为 0..1、默认 1，并提供 `AttackResilience`；环境脚本反射确认 `ComponentDamage.Hitpoints`、`AttackResilience`、`Damage(float)` 与 `Save(...)`。
- 发射顺序：已确认（源码/编译）。主驾驶更新先完成推进/抓地，再处理开火；成功创建炮弹后立刻施加 `RecoilSpeed=1.25`。普通客户端不创建炮弹，只在发送服务器验证请求时预测同一后坐。
- 炮弹生命周期：已确认（源码/编译）。只有独立首次命中回调调用爆炸；取消所有飞行距离/发射车辆包络门槛，近距与远距共用路径。`ProjectileRemoved` 只删除状态，生命周期/区块卸载不会爆炸或重复爆炸。
- 炮管与耐久规则：已确认。纯规则新增 `-5°/+25°` 限位与 `1.25` 后坐断言；XDB/打包守卫要求 Damage 父 GUID、`Hitpoints=1`、`AttackResilience=600`，并禁止 `OnFire`。
- 全量离线回归：已确认。Kelly 11、ATV 35、越野车 20、快艇 16、Tank、Ecology 47、Corn 26、Bamboo 25、Flax 29、Sunflower 33、Apple 49 全部通过。
- Release / 数据库 / 包：已确认。执行 `Build-NetMod.ps1 -GameDir D:\SC -Configuration Release`，exit 0，net472 0 warning/0 error，11 份 XDB 验证通过，59 个包条目；产物 427,650 字节，SHA-256 `94B1FFF3D9BBC9140FC2F2E7AF7D748B0331087B9454A52B481FD0BF24C3FD49`。DLL Assembly/FileVersion 为 `0.22.2.0`，构建时 ProductVersion 为 `0.22.2+79d231d5ec93fb15b20837d5fa307c6372aba0cd`。
- Windows 游戏运行：待验证。未启动游戏。
- Android 7723：待验证。必须验证发射当帧后坐而非等命中、炮管五向限位/视觉弹道一致、贴墙与远距都只爆一次、非命中消失不爆、耐久保存与归零报废、不可点燃，以及房主/普通客户端开火一致性。

## 0.22.1 双座输入与座位稳定化（2026-10-24）

- 目标环境：已确认。`Test-Environment.ps1 -GameDir D:\SC` 通过三程序集固定哈希、API 1.42、net472，并新增确认 `PlayerInput` 动作字段、公开 `m_playerInput`、`LookOrder`、可写父体偏移、`SubsystemUpdate` 注册接口以及 `-10 < -5 < 0` 调度顺序。
- 双座规则：已确认。ATV 规则由 32 增至 35 条，新增“稳定挂载才维护座位、动画中不抢写、脱离后不拉回”三项；ATV 35、越野车 20、快艇 16 及其余 Kelly 11、Tank、Ecology 47、Corn 26、Bamboo 25、Flax 29、Sunflower 33、Apple 49 全部通过。
- Release / 数据库 / 包：已确认。执行 `Build-NetMod.ps1 -GameDir D:\SC -Configuration Release`，exit 0，net472 0 warning/0 error，11 份 XDB 验证通过，59 个包条目；产物 427,897 字节，SHA-256 `2A3A7FE7534E9A7E07D2D0395B339383600FA5B3C4354B4A4390EC60A430F919`。DLL Assembly/FileVersion 为 `0.22.1.0`，构建时 ProductVersion 为 `0.22.1+fdddd79898c4646a177ce647d67017510ecd569f`。
- 输入隔离：已确认（源码/编译）。驾驶 Move 在原生 Player 更新前缓存；骑手个人移动/跳跃清零，水平观察转给各自 LookOrder；Aim/Dig/Hit/Interact 与物品栏/下车输入保持原样。
- 联机架构：已确认（源码/编译）。保留 v0.22 驾驶员首子 Body、普通客户端乘客父车回声抑制及本机驾驶员原生父体同步；未访问私有 `NetNode.PendingPackages`。
- Windows 游戏运行：待验证。未启动游戏。
- Android 7723：待验证。必须验证双人持续乘坐和前进、双方自由转向视角、乘客射箭/挖掘/放置/交互、驾驶角色互换、第三人拒绝、下车及断线重连。

## 0.22.0 全载具联机稳定化（2026-10-23）

- 目标 ABI：已确认。`Test-Environment.ps1 -GameDir E:\SCNET\SCNETx25.01.17` 复核三程序集固定哈希、net472、API 1.42、原生包 ID 0–38、`Client.PlayerData`、公开 `ComponentBody.m_childBodies`，并确认保留包 ID 240/241 无原生冲突。
- 驱动覆盖：已确认（源码守卫/编译）。ATV、双人快艇、双人越野车、飞艇、火车、车厢和坦克均要求本机 `PlayerData.IsMainPlayer` 才运行驾驶输入，不再保留 Local-only 或 Client 直接返回的驱动门。
- 双座回声保护：已确认（源码/ABI）。驾驶员被稳定移到 `ChildBodies[0]`；座位组件在 `UpdateOrder.CreatureModels` 运行，普通客户端若为乘客则清除父 Body 的 `SendPosition/SendRotation/SendVelocity`，避免插值状态回传。
- 坦克远程主炮：已确认（代码/规则）。新增包 ID 241；服务端验证包发送者、Rider 实体、Mount、ParentBody、16 位回绕序号、冷却与浸水门。新增 4 条序号规则断言（递增、重复、过期、回绕）；炮弹仍只由 Local/Server 的原生 `FireProjectile` 创建。
- 全量离线回归：已确认。Kelly 11、ATV 32、越野车 20、快艇 16、Tank（含新增序号断言）、Ecology 47、Corn 26、Bamboo 25、Flax 29、Sunflower 33、Apple 49 全部通过。
- Release / 数据库 / 包：已确认。提交 `daced50` 后再次执行 `Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17 -Configuration Release`，exit 0，net472 0 warning/0 error，11 份 XDB 合并验证通过，59 个包条目；产物 427,108 字节，SHA-256 `25F698A9A2DAF718E09700B1B3ABCE92E88906788174331EEBE2A2FA0F429F39`。DLL Assembly/FileVersion 均为 `0.22.0.0`，ProductVersion 为 `0.22.0+daced50baf63fd2699ed2ce27f44237e62cafce8`。
- Windows 游戏运行：待验证。本机未启动游戏。
- Android 7723：待验证。单机回归、房主驾驶、普通客户端驾驶、双座两种角色互换、断线重连、跨区块及坦克炮塔/爆炸一致性均未实测。
- 多人兼容结论：待验证。源码已覆盖全部现有载具，但在双 Android 实测通过前不得标记“联机正常”。移动同步继承原生驾驶员客户端信任边界；严格服务器权威为不支持。

日期：2026-10-19
目标：`Survivalcraft NET 2.4 Net_x25.01.17`
本机仓库目录：`E:\Survivalcraft2\SCIE-Port`
本机游戏程序集目录：`E:\SCNET\SCNETx25.01.17`

## 结果矩阵

| 项目 | 状态 | 证据/说明 |
|---|---|---|
| 上游 SHA 固定 | 已确认 | `git rev-parse HEAD` = `4864d956793b6e670413019f1f6b1cc5c8bb0970`；工作树干净 |
| SCIE 许可文件检查 | 已确认 | 固定树没有 LICENSE/COPYING；用户声明已获明确许可，车辆资源及公开发布留档边界见第三方声明 |
| x25 Windows 程序集指纹 | 已确认 | 三项 SHA-256 与 `Test-Environment.ps1` 固定值一致 |
| 目标 ABI 检查 | 已确认 | 版本 2.4.0.0、API 1.42、SC 2.4.10.6、net472；ModLoader/CSV/ID 及 Boat/Mount/Rider/SpawnCreature 签名检查通过 |
| 原 SCIE 工程原样构建 | 不支持 | 旧 csproj 硬编码 `D:\Survivalcraft`；缺引用时产生 1378 错误，不能作为目标构建方式 |
| 旧源码直接引用 x25 编译 | 不支持 | target probe 暴露 56 个第一层兼容错误：PluginLoader、几何、UpdateOrder、交互、配方、类型冲突等 |
| SCIE-Port Release 编译 | 已确认 | `dotnet build`：0 warning，0 error |
| 0.3.0/0.3.1 世界加载 `Database not loaded` 根因 | 已确认（机理） | 反编译 `ModsManager.CombineDataBase`：合并按 `LocalName + Guid` 匹配。0.3.1 的 `Watercraft.xdb` 三个原版文件夹用了自定义 GUID（Entities `3ea62aa2…`、Gameplay `fa264e34…`、BlockBehaviors `36e1b3bf…`），与游戏真实原版文件夹 GUID 不一致，导致合并时追加“同名不同 GUID”的游离 `<Folder>`，蒸汽船内容被放进非标准容器而非并入原版 `Entities`。**旧的“根节点造成嵌套 DatabaseObjects”是误诊**：能正常运行的 Builder Assistant `Speedboat.xdb` 恰恰以 `<DatabaseObjects>` 为根且使用正确原版文件夹 GUID。 |
| 0.3.2 修复 | 已确认（编译+静态） | 仅把三个文件夹 GUID 改回原版值（Entities `5fcdf35f…`、Gameplay `d3d4b692…`、BlockBehaviors `00c97f0f…`）；保留 `<SCIEPortWatercraft>` 容器根、全部模板、GUID、InheritanceParent、ProjectTemplate。Release 0 warning / 0 error。 |
| 真实合并验证 harness | 已确认（本机 Windows 程序集） | 反射调用 `D:\SCNET\Survivalcraft.exe` 的 `ModsManager.CombineDataBase`，对合成原版 `<DatabaseObjects>` 分别合并修复前/后 xdb：修复后每种原版文件夹各 1 个、`SCIEPortSteamBoat` 并入真实 `Entities(5fcdf35f…)`；修复前各 2 个（含 rogue），蒸汽船被隔离在游离文件夹。见 `outputs/merge-evidence.txt`。 |
| 完整反序列化端到端 | 已确认（本机 Windows 程序集真实代码路径） | 用游戏自身的 `SurvivalCraftModEntity.GetDecipherStream` 解出 `Content.scpak`（明文即标准 ZIP，head `50 4b 03 04`，987 个条目），从中取到真实原版 `Assets/Database.xml`（463,610 字节，含 `<DatabaseObjectTypes>`，10 个类型定义），再调用游戏真实的 `Game.DatabaseManager.LoadDataBaseFromXml`（正是加载界面 `b__16_8` 里被 try/catch 吞成 `Warning(e.Message)` 的那一步）跑端到端。结果：**0.3.1(BROKEN)** 首条真异常为 `System.InvalidOperationException: Another database object with name "Entities" is already nested in database object "Root".`（栈：`DatabaseObject.set_NestingParent` ← `XmlDatabaseSerializer.LoadDatabase:77` ← `DatabaseManager.LoadDataBaseFromXml:42`）——即 rogue 同名 `Entities` 文件夹让两个同名 Folder 同时挂到 Root，反序列化器直接抛异常，`m_gameDatabase` 保持 null，进世界时 `get_GameDatabase()` 才补刀 `Database not loaded.`。**0.3.2(FIXED)** 与**阳性对照 Builder Assistant Speedboat(手机已知能跑)** 均 `LoadDataBaseFromXml: SUCCESS`。对照通过同时反证 harness 忠实还原了手机加载路径。见 `outputs/MergeHarness-v2-evidence.txt`。注：本机为桌面 .NET Framework 构建；Android 同源代码，最终仍以手机实测为准。 |
| `Test-Database.ps1` 校验脚本 | 已确认 | 新增脚本并接入 `Build-NetMod.ps1`：禁止“同名不同 GUID 的原版文件夹”“单文件内同名不同 GUID 重复文件夹”“悬空/未知 InheritanceParent”。对修复后 xdb PASS(0)，对 0.3.1 rogue 版 FAIL(1) 并精确点出三处 GUID。 |
| `.netmod` 内容校验 | 已确认 | 0.3.3 包含 DLL、modinfo、两块 CSV、修复后的 Watercraft XDB、**朝向修正后的**蒸汽船模型（Y_UP）/授权纹理及第三方声明；打包级校验强制模型 `up_axis=Y_UP` 且顶点数组保持 98 顶点（294 float）+ `Cylinder` 节点 |
| `.netmod` 大小 | 已确认 | 0.3.3：20,975 字节（较 0.3.2 变化源于打包模型由上游 Z_UP 原文件换成项目内修正后的 Y_UP 资产，且 zip 时间戳非确定性 ±若干字节） |
| `.netmod` SHA-256 | 已确认 | 0.3.3：`B86B9B152BE738697FB2AE9A4A901208FD0EBD577C9F93BA20B5344CDDCDBCCA`（20,975 字节）。历史：0.3.2（世界加载修复）`E0C3568E…` / 首次构建 `AC22833A…`；0.3.1 旧值 `78E44A1C…`（进入世界报 `Database not loaded`）。 |
| `.netmod` 内容校验 | 已确认 | 0.5.1 包含 DLL、modinfo、CSV、Watercraft XDB、四种载具/铁轨代码、三份 Y_UP 派生模型、车厢模型、纹理及第三方声明 |
| `.netmod` 大小 | 已确认 | 0.5.1：136,966 字节；DAE 等文本资源经 ZIP 压缩 |
| `.netmod` SHA-256 | 已确认 | 0.5.1：`68DD8F38C7962953C655BC84A3CECD542F7E2063E21E22CF70D06E036A1070A6` |
| 0.6.0 Kelly 源码迁移 | 已确认 | 三个实现文件已移至 `src/SCIEPort/Features/KellyPlants`，命名空间及 XDB 类名改为 `SCIEPort.Features.KellyPlants`；Builder Assistant 不再包含或注册 Kelly 功能 |
| 0.6.0 Kelly 资源 | 已确认 | 14 条 CSV、`KellyPlants.xdb`、`KellyFruits.dae` 与授权 WebP 源文件均位于 SCIE-Port；模型 SHA-256 `C35D3B5DDD7D42464F9DA55C88A5A87AEE832FDB1E91B8864B6F1E4E4A6B40A9`，WebP 源文件 SHA-256 `E4C99B07E7F4D2FEB8420D889C3CC0559DC835B951D1AC56CA5FDD87320CE0` |
| 0.7.1 Kelly 创造栏崩溃日志 | 已确认（缺陷） | 用户 Android 日志在 `KellyFruitBlock.DrawBlock` → `KellyPlantTextures.Atlas` 明确报 `未能找到资源[Textures/KellyExpansion][Engine.Graphics.Texture2D]`；0.7.0 包只有 WebP |
| 0.7.1 Kelly PNG 修复 | 已确认（文件/打包级） | WebP 以 512×512 RGBA、无缩放方式转换为 PNG；构建包必须含 PNG 且拒绝 WebP。Android 翻页复测待验证 |
| 0.7.2 资源引用审计 | 已确认（静态/打包） | 所有 Mod 自有 `ContentManager.Get`、Watercraft XDB `ModelName`/`TextureOverride` 均可映射至包内 DAE 或 PNG；唯一原版依赖为 `Textures/Creatures/Jaguar`，已从目标 `Content.scpak` 确认存在。包内不存在非 PNG 的 `Assets/Textures/*` |
| 0.7.2 飞艇黑色根因 | 已确认（代码/ABI） | XDB 中原先没有 `TextureOverride`；`ComponentAirshipDrive.Load` 虽写入运行时白贴图，却可能早于 `ComponentSimpleModel.Load`，后者按空值重设为 null。物品图标走 `DrawMeshBlock` 的独立白贴图，故不受影响 |
| 0.7.2 载具世界颜色修复 | 已确认（编译/静态） | 飞艇 XDB 改为包内 `Textures/Airship.png`；蒸汽船、飞艇、火车、车厢统一设置白色 Diffuse 与 `0.12` 低强度 Emission，保留原贴图颜色并避免低地形光压成纯黑 |
| 0.7.2 Release/包检查 | 已确认 | `Test-Environment.ps1` 与 `Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17 -Configuration Release` 通过；net472、程序集 `0.7.2.0`、0 warning、0 error，XDB 与资源守卫通过 |
| 0.7.2 `.netmod` | 已确认 | 361,213 字节；SHA-256 `A1E1F4A683120049250117168FB5AC5CF465B32E8E515943C35ED764B34F2FB2`；包含 `Airship.png` 与 `KellyExpansion.png`，不包含 Kelly WebP 或游戏 DLL |
| 0.7.2 Android 运行 | 待验证 | 未在本机或设备启动游戏；需验证植物第三页及后续翻页不退出，以及飞艇/火车/车厢/蒸汽船在昼夜和室内外均非纯黑 |
| 0.8.1 ATV 源码审计 | 已确认 | SteamBoat 仅复用 Mount/Rider/Body/Spawn 实体模式；Train/Carriage 仅复用单人触屏读取与实体物品部署模式。船浮力/水面驱动和轨道居中均未迁入 ATV。真实 `ComponentBody` 反编译确认负责碰撞、重力、存档 Velocity/ParentBody 和 `MaxSmoothRiseHeight` 平滑上台阶 |
| 0.8.1 ATV ID/XDB/资源 | 已确认 | ID 923 未被 260 个目标原版 Block 常量占用；`LandVehicles.xdb` 的三原版 Folder GUID、模板继承、实体和 Project 成员均通过 `Test-Database.ps1`。项目创建的 Y_UP `ATV.dae` 含车身与四个车轮节点；包内 `ATV.png` 为项目白色基础纹理 |
| 0.8.1 ATV 离线规则测试 | 已确认 | `tests/SCIEPort.LandVehicles.Tests` 共 13 项通过：接地/浸水门控、前进、后退、反向刹车、双向限速、松油滑行、正反向转向和 0.35 格台阶上限 |
| 0.8.1 Release/包检查 | 已确认 | `Test-Environment.ps1 -GameDir E:\SCNET\SCNETx25.01.17`、Kelly 11 项规则测试、ATV 13 项规则测试、net472 Release（0 warning/0 error）、三份 XDB 和包资源守卫全部通过 |
| 0.8.1 `.netmod` | 已确认 | 366,804 字节；SHA-256 `A88675F576BBE4638F9C13E2F7FB5D3B70C700780EBFF240AEE38CB244C65577`；包含 `LandVehicles.csv`、`LandVehicles.xdb`、`ATV.dae`、`ATV.png`，不包含游戏 DLL、PDB 或上游 DLL |
| 0.8.1 ATV Windows/Android/多人运行 | 待验证 | 本机没有启动游戏，也没有部署设备。需要验证生成、骑乘、第三人称、地形碰撞、0.35 格台阶、刹车、存档重载；本版本不支持多人自定义物理/输入同步 |
| 0.8.2 ATV 根因审计 | 已确认 | 目标 `ComponentBody` 反编译确认接地 `6*dt` 原生阻力与 v0.1 自定义阻力叠加；源码确认离地调用 `7*dt` 水平归零、横向抓地固定每帧 0.35、物品重复反向面、共享单 cube geometry 和 1×1 白纹理。Android 共享实例行为仍标待验证 |
| 0.8.2 ATV 规则测试 | 已确认 | `tests/SCIEPort.LandVehicles.Tests` 23 项通过：地形/实体支撑隔离、浸水门控、0.35 秒离地状态、前后驱动、反向刹车、松油不过零、双向限速、高低速转向和 1.05 格 smooth-rise clearance |
| 0.8.2 ATV 模型/纹理守卫 | 已确认 | 九个独立 Y_UP geometry/node、identity 矩阵、每件 24 position floats/12 triangles/法线/UV；32×8 PNG 恰好四种 Alpha=255 色，模型边界完整落在 `1.7,1.15,2.1` Body 内 |
| 0.8.2 Release/包检查 | 已确认 | `Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17 -Configuration Release` 通过；固定程序集哈希/ABI、Kelly 11 项、ATV 23 项、net472 Release 0 warning/0 error、三份 XDB 与全部包守卫通过 |
| 0.8.2 `.netmod` | 已确认 | 368,782 字节；SHA-256 `BFB90D40D17BE7E500BB6FB44688629D5CD7C1287846BC7B6C26DE79BE132DC9`；内置 `SCIEPort.dll` 版本 `0.8.2.0`，不含游戏 DLL/PDB/上游 DLL |
| 0.8.2 Windows 游戏运行 | 待验证 | 按用户此前要求未启动本机游戏；物品/世界渲染、实际速度、相机、骑乘、碰撞和旧实体恢复均未运行验证 |
| 0.8.2 Android 7723 | 待验证 | 尚未安装到手机；模型、触屏手感、一格越障、狭窄道路、净空和保存重载必须按 `ATV-V0.2-AUDIT.md` 验收 |
| 0.8.2 多人 | 不支持 | 驱动仍只在 `WorkType.Local` 执行，没有服务端权威输入或状态纠偏 |
| 0.8.2 Android 世界模型 | 已确认（缺陷） | 用户实测创造栏图标可见、生成到世界后无模型；目标 `ModLoader.GetMaxInstancesCount()` 返回 7，0.8.2 DAE 经目标 `Engine.Media.ModelData.Load` 实际导入为 10 bones/9 meshes，超过世界实例模型的默认变换预算；物品栏路径因预先烘焙网格而不受影响 |
| 0.8.3 ATV 单骨骼模型 | 已确认（目标 Engine 导入） | 九个独立 geometry 统一挂到一个 identity `ATV` 节点；目标 x25.01.17 `Engine.Media.ModelData.Load` 实际返回 1 bone/9 meshes。打包脚本新增同一真实导入断言，防止模型骨骼预算回归 |
| 0.8.3 Release/包检查 | 已确认 | 固定程序集 ABI、Kelly 11 项、ATV 23 项、net472 Release 0 warning/0 error、三份 XDB、单骨骼导入和全部资源守卫通过 |
| 0.8.3 `.netmod` | 已确认 | 2026-10-20 文档回归构建为 368,658 字节；SHA-256 `11B261B33D4CF30BA649F0E599AA14170A44A39C4716A37A503952E4E1907A53`；内置 `SCIEPort.dll` 版本 `0.8.3.0`，不含游戏 DLL/PDB/上游 DLL。ZIP 时间戳使重打包哈希可变化，功能内容由包内守卫校验 |
| 0.8.3 Android 世界渲染 | 已确认 | 2026-10-20 用户在 Android 7723 客户端确认单骨骼修复包测试通过；ATV 物品栏和生成后的世界模型均可见。该结论仅覆盖模型显示，不自动覆盖驾驶手感、一格越障、保存重载或多人同步 |
| 0.6.0 Kelly 原名守卫 | 已确认 | 14 个名称按 CSV 顺序拼接的 UTF-8 SHA-256 为 `5b868dfafcb2fd858f71f73d0997c16779a2cbf7f5696848c1df341651cb3656`；无中英文括号，构建脚本会拒绝改名或追加括号移植标注 |
| 0.6.0 原版 ID 扫描 | 已确认 | `Test-Environment.ps1 -GameDir D:\SC` 通过；目标程序集读出 260 个原生方块常量，903–916 与 995–1000 均未冲突 |
| 0.6.0 Release 编译 | 已确认 | 使用已安装 .NET SDK 8.0.400 的 MSBuild 直接构建 net472，`TreatWarningsAsErrors=true`，成功生成 `SCIEPort.dll`，无错误或警告 |
| 0.6.0 XDB 静态校验 | 已确认 | `KellyPlants.xdb` 与 `Watercraft.xdb` 同时通过 `Test-Database.ps1`；原版文件夹 GUID、内部继承与已知原版父级均可解析 |
| 0.6.0 `.netmod` 完整打包 | 待验证 | 本机缺少被 Git 忽略的 `upstream/SCIE` 固定快照；自动获取固定提交时 GitHub 443 不可达，因此既有 SCIE 三个授权资源无法重新取入，本轮未生成新的完整 `.netmod`，不得伪报包哈希 |
| 0.6.0 Android Kelly 运行 | 待验证 | 尚未在 7723 客户端验证原名显示、贴图/模型、播种成长、生态生成、雨后蘑菇、存读档与性能 |
| 0.7.0 工业家具来源 | 已确认 | 固定 SCIE commit `4864d956...` 的 Gear、Wheel、GearMould、WheelMould、Piston、Cylinder 六个模型；源文件与派生文件 SHA-256 已记录在 `THIRD-PARTY-NOTICES.md` |
| 0.7.0 工业家具 ID | 已确认 | 真实 x25.01.17 程序集反射读出 260 个原生方块常量，917–922 未占用；与本工程 903–916、995–1000 无内部冲突，外部 Mod 冲突仍待安装检查 |
| 0.7.0 工业家具模型转换 | 已确认 | Gear、Wheel、两种 Mould、Piston 的 positions/normals 已用 `Convert-DaeToYUp.ps1` 从 Z_UP 转为 Y_UP；Cylinder 原生 Y_UP；构建逐个校验节点与 position float 数 |
| 0.7.0 Release 编译 | 已确认 | `Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17`：net472、程序集版本 0.7.0.0、0 warning、0 error；真实 x25 程序集哈希与 ABI 检查通过 |
| 0.7.0 XDB 校验 | 已确认 | `KellyPlants.xdb` 与 `Watercraft.xdb` 均通过 `Test-Database.ps1`；家具不新增 XDB，不改变已确认文件夹 GUID |
| 0.7.0 `.netmod` | 已确认 | 339,693 字节；SHA-256 `520FAA575C7100F4942C1EDB1652978A0DA445631790D54B010517F03B2EF03B`；包内含六件家具 CSV/模型，不含游戏 DLL、旧 Mod DLL、PDB 或 Jint |
| 0.7.0 Windows 家具运行 | 待验证 | 尚未启动游戏验证创造栏、模型比例/颜色、四向放置、碰撞、拆除和保存重载 |
| 0.7.0 Android/7723 家具运行 | 待验证 | 尚未部署手机；Windows 编译与包检查不能替代真机渲染和存档验证 |
| 0.7.0 多人家具同步 | 待验证 | 静态方块理论上走原生地形同步，但尚未分别测试主机和普通客户端，不能继承单机结论 |
| 蒸汽船物品/组件编译 | 已确认 | ID 999；独立实体、原生 Boat/Mount/Body/Damage 和本地推进组件均以 Release 0 warning/0 error 编译 |
| SCIE 蒸汽船模型 | 已确认 | 上游授权源模型（保持不动、仅溯源）：`upstream/SCIE/IndustrialMod/Models/SteamBoat.dae`，25,178 字节，SHA-256 `E3C2F54513D10486FA14927418E6ABB76CB7AC270B947A57C338A2423911C4F8`，`up_axis=Z_UP`。**实际打包的模型**（项目内朝向修正资产）：`src/SCIEPort/Assets/Models/SteamBoat.dae`，25,042 字节，`up_axis=Y_UP`，仍含 `Cylinder` 节点与 98 顶点（294 float）。 |
| 蒸汽船“竖着”朝向修复 | 已确认（文件/打包级）/ Android 待实测 | 现象：手机进世界能加载，但船竖直立着。根因：SCIE 的 `SteamBoat.dae` 是 Blender 导出的 `Z_UP`（把高度放在 Z 轴），而 SCNET 的 `SimpleModel` 按游戏约定直接读原始坐标、把 Y 当上、Z 当前——于是 Z_UP 模型的高度轴被当成“前后”，船就竖起来；对照能正常横放的 Builder Assistant `Speedboat.dae` 是手写的 `Y_UP`。修复：对 `Cylinder-mesh-positions-array`（98 顶点）与 `Cylinder-mesh-normals-array`（135 法线）逐三元组烘焙绕 X 轴 −90° 旋转 `(x,y,z)->(x,z,-y)`（右手系不翻转），并把 `<up_axis>` 改为 `Y_UP`；不改 `SimpleModel` 参数、不动 `Body` 的 `BoxSize`、不动模板/GUID。结果包围盒由（宽 X±1.06 / 高 Z0.06..1.56 / 长 Y−2.74..2.35）变为（宽 X±1.06 / 高 Y−0.13..1.56 / 长 Z−2.35..2.74），与 `BoxSize=2,0.5,4.5`（Z 为船长）一致。**边界**：本机只能在文件与打包级验证几何已旋转、`up_axis=Y_UP`、顶点数未丢，且已确认随包模型确为 Y_UP；手机里船是否真正平放仍须实机确认，不提前宣称已修复。 |
| SCIE 蒸汽船纹理 | 已确认 | 64×64 PNG，6,036 字节；SHA-256 `BB0CC5F9886E30BA9DE12D79F1C7724CBF9C6341DCE7606C65906441ABE8F6A4` |
| 飞艇物品/实体/飞行组件编译 | 已确认 | ID 998；独立实体、Mount/Body/Damage、触屏输入和本地限速飞行均为 Release 0 warning/0 error |
| 0.5.0 Android 飞艇缺陷 | 已确认 | 用户实测飞艇竖直且无法骑乘；源模型 Z_UP，且 9 格宽物理体让玩家无法进入原生 2.5 格座位搜索范围 |
| 0.5.1 SCIE 飞艇派生模型 | 已确认 | Y_UP、926 顶点，范围 X `-4.628..4.628`、Y `0..11.162`、Z `-15.925..16.054`；145,775 字节，SHA-256 `E1A4DD77D02334607A88349472471B711FE1B1DEE5EE75DF510DB38DCAC3664A` |
| 0.5.1 飞艇骑乘参数 | 已确认（静态/编译） | Body `3,2,6`、Mount `0,1,-0.8`、Dismount `1.75,0.5,0`，生成点为命中点上方 0.1 格；Android 实际上船待验证 |
| 铁轨/火车/车厢编译 | 已确认 | ID 997/996/995；轨道几何、邻接形状、实体生成、骑乘及本地沿轨组件为 Release 0 warning/0 error |
| SCIE 火车模型/纹理 | 已确认 | 0.5.1 派生 `Train.dae` 为 Y_UP、220 顶点、49,781 字节，SHA-256 `9269DC2A0047EDC7A68D93F6C88236B81BA26292DA74723520B4C5244185A29D`，`Cylinder` 节点通过；`Train.png` SHA-256 `413E44943BA96517FB0B1712EB0ACC157CE60E14BE7B96C7D0B3FB37C3A5CD57` |
| SCIE 车厢模型 | 已确认 | `Minecart.dae` 432,139 字节，SHA-256 `CDB55F28AA29BAB7067DF8D68217953BB70EB8D3544BC10A337C2BDD639D88F5`，`obj1` 节点检查通过 |
| Watercraft XDB 静态结构 | 已确认 | 自定义根 `SCIEPortWatercraft`；109 个 XML 节点、108 个 GUID、无重复 GUID |
| Windows 游戏加载 | 待验证 | 本次没有启动游戏进程 |
| Windows 创造栏显示材料 | 待验证 | 必须在新建可丢弃单人世界确认 |
| Windows 蒸汽船生成/骑乘/驾驶 | 待验证 | XDB、内置模型路径、触屏输入和实体运行行为必须在游戏中确认 |
| 世界加载（进入世界） | 已确认（桌面真实反序列化 + 手机实测） | 桌面 harness 已用真实 `DatabaseManager.LoadDataBaseFromXml` 证明：修复后首异常 `Another database object with name "Entities" is already nested in database object "Root".` 消失、加载成功，且阳性对照同样成功。手机实机反馈“经过测试可以了”——能进世界，不再报 `Database not loaded`，仅剩“船竖着”的外观问题（见“蒸汽船朝向修复”行）。0.3.3 未改 XDB（只换模型），故该世界加载结论直接继承。 |
| 0.5.1 Android 四种载具 | 待验证 | 蒸汽船、修复后飞艇、修复后火车、车厢的姿态、生成、骑乘、驾驶和保存恢复均需实机复测 |
| 0.5.1 铁轨运行 | 待验证 | 直轨、四种弯轨、四种坡轨、自动形状更新及沿轨转向未启动游戏验证 |
| Windows 保存/重载 | 待验证 | 仅创造物品也需验证存档不报错 |
| Android 7723 实测 | 待验证 | 尚未部署到手机，Windows 指纹不能替代 Android 证据 |
| 多人主机/客户端 | 不支持 | 四种载具的自定义推进/飞行/轨道物理仅启用 `WorkType.Local`；尚无服务器权威输入同步 |
| 火车蒸汽燃料、车厢编组/货箱 | 不支持 | 依赖尚未迁移的工业机器、库存、父体同步和存档链；0.5.1 未以占位实现冒充完成 |

## 执行命令

```powershell
cd E:\Survivalcraft2\SCIE-Port
.\scripts\Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'
.\scripts\Test-Database.ps1                 # 单独运行数据库静态校验
.\scripts\Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'   # 构建时会自动再跑一次 Test-Database.ps1
```

以上三条命令在修复后的 0.3.2 代码树上都应成功。`Build-NetMod.ps1` 在打包前会自动调用 `Test-Database.ps1`；一旦出现“同名不同 GUID 的原版文件夹”或悬空 `InheritanceParent`，构建即失败，防止本类缺陷再次进入 `.netmod`。

## 包清单

```text
Assets/Blocks/SCIEPort.csv
Assets/Blocks/Watercraft.csv
Assets/Blocks/KellyPlants.csv
Assets/Blocks/IndustrialFurniture.csv
Assets/Database/Watercraft.xdb
Assets/Database/KellyPlants.xdb
Assets/Models/Airship.dae
Assets/Models/KellyFruits.dae
Assets/Models/Cylinder.dae
Assets/Models/Gear.dae
Assets/Models/GearMould.dae
Assets/Models/Minecart.dae
Assets/Models/Piston.dae
Assets/Models/SteamBoat.dae
Assets/Models/Train.dae
Assets/Models/Wheel.dae
Assets/Models/WheelMould.dae
Assets/Textures/SteamBoat.png
Assets/Textures/Train.png
Assets/Textures/KellyExpansion.png
modinfo.json
SCIEPort.dll
THIRD-PARTY-NOTICES.md
```

包内未发现：`Survivalcraft.exe`、`Engine.dll`、`EntitySystem.dll`、`Jint.dll`、`System.Reflection.TypeExtensions.dll`、SCIE 音频/旧 XDB/配方/翻译。

## 下一轮运行测试步骤

1. 备份并使用新建单人世界。
2. 把 `artifacts/SCIEPort.netmod` 放入该精确客户端的 `NetMods`。
3. 启动后保存完整加载日志，确认 `SCIEPort` 被发现且无 CSV/Block 初始化警告。
4. 进入创造模式，搜索并拿取 `SCIE Port Test Alloy`；检查图标、名称、描述、堆叠、丢弃、拾取。
5. 搜索 `SCIE 蒸汽船（原型）`，对水面使用；确认只生成一艘实体且物品减少。
6. 验证上船、触屏前进/后退/转向、松手减速、下船、碰撞损坏及附近最多 4 艘限制。
7. 保存退出并重进，确认玩家库存、船实体和位置可恢复且日志无实体模板错误。
8. 搜索 `SCIE 飞艇（授权移植预览）`，在开阔地生成；验证乘坐、触屏前后推进、视角控制升降、转向、下船、浸水损坏和附近 2 艘限制。
9. 保存退出并重进，确认飞艇位置和状态可恢复。
10. 搜索并铺设 `SCIE 铁轨（授权移植预览）`：分别验证直线、四向弯道和一格高坡道是否自动成形。
11. 把 `SCIE 火车（授权移植预览）` 放到轨道上，验证非轨道拒绝生成、乘坐、触屏前后驾驶、弯道和坡道。
12. 对 `SCIE 车厢（授权移植预览）` 重复生成和独立低速试乘；当前不应期待自动编组或货箱。
13. 保存退出并重进，确认铁轨 data、两种轨道实体位置/方向恢复且日志无模板错误。
14. Android 上重复 2–13，并记录 APK/程序集指纹、设备、Android 版本和日志。
15. 在 Furniture 分类逐一拿取钢齿轮、钢轮、齿轮模具、车轮模具、工业活塞、钢瓶；每件按四个朝向放置，核对不竖直、不沉地、不越格、碰撞可接受。
16. 逐件拆除并确认掉落原物品；保存退出并重进，核对类型与朝向 data 均保持。家具仅为静态装饰，不应出现机器 UI、库存或配方行为。
17. 搜索 `SCIE 越野摩托`，仅在新的可丢弃单人创造世界对实体顶面/地面使用：确认一次消耗一件、附近超过 4 辆时拒绝生成，且不会改变任何地形方块。
18. 靠近驾驶座使用原生上车操作：确认可上车/下车，第一和第三人称都跟随载具，不出现玩家被碰撞体卡住或无限下落。
19. 在平地分别验证前进、松手滑行、后退、行驶中反向输入先刹车再倒车、左右转向；确认最大前进和倒退速度受限，进入深水会停驱动并下车。
20. 对半格坡、完整一格方块、两格墙、实体墙和低顶棚一格台阶重复低速和全速测试：一格应由原生 smooth rise 连续抬升；两格、其它实体和净空不足必须发生原生碰撞，绝不穿透、飞越、瞬移或改写方块。
21. 撞墙、从一格高落下、跨区块边缘后保存退出并重进：确认 ATV 位置/朝向/速度可恢复，日志没有 `SCIEPortATV`、`LandVehicles`、模板、模型或 XDB 异常。多人主机和普通客户端不在本阶段测试范围，不能把单人结果外推为联机兼容。

## Android (SCNET x25.01.17 / 7723) 安装、卸载旧包、隔离测试步骤

这一步是判断 `Database not loaded` 是否真正解决的唯一权威证据；本机 Windows 合并验证不能替代它。

1. 记录当前包（用于回滚定位）：在手机上删除旧版前先确认 NetMods 目录里存在的是 0.3.1 的 `SCIEPort.netmod`。0.3.1 与 0.3.2 的 `PackageName` 都是 `SCIEPort`，同名会互相遮蔽，**必须先彻底卸载旧包**。
2. 卸载旧包：进入 Survivalcraft 的 Mod/NetMods 管理界面删除 `SCIEPort`，或直接用文件管理器删掉 `NetMods` 内的旧 `SCIEPort.netmod`（`PackageName=SCIEPort`，文件名可能是 `SCIEPort.netmod` 或安装器改写过名字，以 `modinfo.json` 里 `PackageName` 为准）。确认目录内不再残留任何 `SCIEPort` 相关文件。
3. 确认真实 NetMods 路径：Survivalcraft 在 Android 上读取应用私有目录下的 `NetMods`（典型为 `Android/data/<包名>/files/NetMods`，部分版本在 `.../Documents/NetMods`）。以游戏内“已加载 Mod 列表”能显示的路径为准；放错目录不会报错，只会“看不到 Mod”。
4. 部署 0.3.3：把 `artifacts/SCIEPort.netmod`（或交付的 `SCIEPort-0.3.3.netmod`，内容相同）拷进上述 `NetMods`，确保**目录里只有这一个 `SCIEPort`**。核对 SHA-256 = `B86B9B152BE738697FB2AE9A4A901208FD0EBD577C9F93BA20B5344CDDCDBCCA`（20,975 字节），与本次构建一致。0.3.1/0.3.2/0.3.3 的 `PackageName` 都是 `SCIEPort`，同名互相遮蔽，务必先按步骤 2 彻底卸载旧版再放入 0.3.3。
5. 冷启动游戏：完全退出进程后重开（避免旧 DLL/DB 缓存残留），进入 Mod 管理确认 `SCIE Industrial Era Port (Audit Build) 0.3.3` 已启用。
6. 隔离世界测试：新建一个可丢弃的**单人创造世界**（不要用会污染主存档的世界）。本轮重点：①世界仍能正常进入、无 `Database not loaded`（0.3.3 的 XDB 与已实测通过的 0.3.2 完全相同，只换了模型，理论上不会回归）；②生成蒸汽船后**船是否平放在水面**、不再竖着——这是本次朝向修复要验证的核心。
7. 抓加载期 WARNING：真实首异常只以 `Warning(e.Message)`（只有文字、无堆栈）出现在**读条/加载阶段**，晚一点才会在进世界时报 `Database not loaded`（那只是下游症状）。已在本机复现出 0.3.1 的确切首异常原文是 `Another database object with name "Entities" is already nested in database object "Root".`。请在日志里翻到**第一次进 `GameLoading` 之前/期间的 WARNING 行**：如果看到的仍是这条（或同类的“…is already nested…”“…already present…”），说明手机加载的还是旧包；如果 0.3.3 确实已加载却换了**别的** WARNING 文字（例如某个具体模板/父级 GUID），把那条原文+设备/Android 版本/完整日志回传，据此继续定位，不宣称已修复。
8. 若船已平放：继续蒸汽船功能实测（搜索/拿取、放置、上船、驾驶、下船、存档重载），全部通过后才可把“蒸汽船朝向修复”行从“Android 待实测”改为“已确认”。若仍竖着或躺倒方向不对（如侧翻/上下颠倒），记录实际朝向并把 `src/SCIEPort/Assets/Models/SteamBoat.dae` 与 `modinfo.json` 版本回传，据旋转方向继续调整，不提前宣称已修复。

只有以上证据完成后，才能把相应运行项目从“待验证”改为“已确认”。

## 0.8.1 T-34 风格坦克第一阶段（2026-10-19）

### 已确认的构建/静态证据

| 检查 | 结果 |
|---|---|
| 独立分支/工程 | `codex/t34-tank` 的独立工作树；只改 `SCIE-Port`，未改 Builder Assistant、Watercraft、Rail 或 Kelly 源码 |
| 目标程序集 | `E:\SCNET\SCNETx25.01.17`；`Test-Environment.ps1` 校验已记录的 `Survivalcraft.exe`、`Engine.dll`、`EntitySystem.dll` SHA-256、2.4.0.0、API 1.42、SC 2.4.10.6 与 net472 |
| 真实 ABI 守卫 | `ComponentBody` 地面物理属性、Mount/Rider/原生下车、`ComponentPlayer → ComponentInput → PlayerInput.Move`（`Move` 为目标程序集公开字段）、`ComponentCreatureModel.EyeRotation`、生成接口与模型贴图接口均通过反射检查 |
| 纯规则测试 | `dotnet run --project tests/SCIEPort.Tanks.Tests/SCIEPort.Tanks.Tests.csproj --configuration Release --nologo`：通过；覆盖前进、倒车、松油、反向制动、两方向限速和 0.3 格台阶上限 |
| 完整构建 | `scripts/Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17 -Configuration Release`：通过；Kelly 11 条既有规则测试、Tank 规则测试、net472 Release、全部 XDB 验证和包清单检查均通过，构建输出无 warning/error |
| 数据库/资源 | `Tanks.xdb` 的原版文件夹 GUID、父模板、`SCIEPortT34Tank` 的 Body/Mount/Spawn/Damage/TankDrive、`T34Tank` Y_UP、三个各自独立的 `TankHull`/`TankTurret`/`TankGun` geometry/node、法线、UV、包内 PNG 均由构建脚本验证 |
| 产物 | `artifacts/SCIEPort.netmod`，366,736 字节，SHA-256 `6A62471E9BCE317B1CC9E38B6F185D2A946BF1B3FC3DC2180262D4A2E13FBD2C`；内置 `SCIEPort.dll` 版本 `0.8.1.0`；不含游戏 DLL/PDB/旧 Mod DLL |

### 未进行的运行测试

| 环境 | 状态 | 说明 |
|---|---|---|
| Windows 游戏运行 | 待验证 | 本次未启动游戏；没有生成、骑乘、第三人称、碰撞、坡度、离水、退出保存或重载的运行结论 |
| Android 7723 | 待验证 | 本次未安装/启动手机端；Windows 编译、数据库合并和资源包检查不能证明 Android 兼容 |
| 多人联机 | 不支持 | 本阶段仅在 `WorkType.Local` 改写驱动速度；没有服务器权威、复制、重连或纠偏实现 |

### Android 7723 单人创造世界测试清单

1. 备份现有包，在 `NetMods` 中只保留本轮 `SCIEPort.netmod`；冷启动游戏，确认加载信息显示 `0.8.1`。
2. 新建可丢弃的单人创造世界，搜索 `T-34 坦克`，确认图标、名称和拿在手上的模型不黑屏、不缺资源。
3. 对平坦固体地面使用物品，确认实体生成在地面上方、物品扣除、不会写入或覆盖地形。
4. 靠近车体并上车；分别验证前进、倒车、左右转向、松手减速和下车，且离地/落水时不继续获得地面推力。
5. 对 0.3 格以内小台阶、1 格墙、斜坡边缘、建筑墙和悬崖逐项测试：只应由原生 Body 碰撞/重力处理，不得穿墙、穿地或飞行。
6. 保存退出并重进，确认实体模板、位置、车体方向及日志正常；本阶段 `TurretYaw`/`GunPitch` 只是持久化预留字段，尚无操控/动画。
7. 若失败，回传首次 `Warning/Error` 原文、完整日志、设备型号、Android 版本、7723 客户端版本/文件指纹与复现步骤；不要仅凭后续的“Database not loaded”判断根因。

## 0.9.0 坦克 v0.2 稳定化（2026-10-20）

- 环境检查：已确认。`E:\SCNET\SCNETx25.01.17`，`Survivalcraft.exe` 2.4.0.0，API 1.42，SC 2.4.10.6，net472。
- 规则测试：已确认。坦克推进、倒车、滑行、反向制动、限速、转向及接地许可测试通过。
- 模型导入：已确认。目标 `Engine.Media.ModelData.Load` 实际结果为 3 骨骼、20 网格；Y_UP、层级、法线、UV、三角形和 64×8 八色不透明 PNG 均通过构建守卫。
- 数据库：已确认。`Tanks.xdb` 的 Body/Mount/Spawn/Damage/TankDrive 继承与内部 GUID 校验通过；ID 924 保持不变。
- Release 编译：已确认。0 warning、0 error。
- `.netmod`：已确认从 `main` 重新生成并完成包内容检查。路径 `artifacts/SCIEPort.netmod`，SHA-256 `5B2625728D76D39B4DA060FB36F7259D16B2AB2A0F4B9DF4E7CF1ADA676E0A67`。
- Windows 游戏运行：待验证；本轮未启动游戏。
- Android 7723 实机：待验证；不得依据 Windows 编译结果标记兼容。
- 多人联机：不支持；本阶段驱动只在 `WorkType.Local` 运行。

详细根因、参数和实机清单见 `docs/TANK-V0.2-AUDIT.md`。

## 0.9.1 坦克 v0.3 主炮、尺寸与越障（2026-10-20）

- Android 基础坦克：已确认。用户反馈“坦克测试通过”，同时明确旧包无法开炮、无法跨越完整一格、占地太小。
- 目标 ABI：已确认。`PlayerInput.Hit`、`SubsystemProjectiles.FireProjectile`、`BulletBlock.Index=214`、`ProjectileRemoved`、`SubsystemExplosions.AddExplosion` 与 `ComponentModel.SetBoneTransform` 均由固定 x25.01.17 程序集守卫确认。
- 规则测试：已确认。新增冷却、输入射线、深水禁射、弹速、爆炸压力及 1.05 格 smooth-rise 检查，全部通过。
- 模型：已确认（离线）。坐标放大 35%，目标 Engine 仍实际导入 3 骨骼/20 网格；世界 Body、座位与下车偏移同步扩大。
- Release 编译与数据库/包校验：已确认，0 warning、0 error。
- `.netmod`：`artifacts/SCIEPort.netmod`，SHA-256 `4ECB3BFDD070CEBC444CC4B09C8B5F1E2BCE67699C725FB145523B35CE5CAEEB`。
- Android 新功能：待验证。攻击触屏开炮、炮塔朝向、炮弹命中爆炸、后坐、一格越障和尺寸满意度必须重新实测。
- 多人联机：不支持。主炮和驾驶当前只在 `WorkType.Local` 执行。

## 0.9.2 坦克 v0.3.1 炮塔与炮口安全修复（2026-10-20）

- Android v0.3：已确认缺陷。用户实测炮塔不转，第一次开炮会炸到坦克。
- 根因：已确认。目标程序集 `FireProjectileNet` 会依据传入 owner 的玩家包围盒重写起点；骑手位于坦克内，导致车外炮口被拉回车体碰撞箱。
- 修复：炮塔持续读取活动摄像机方向；发射使用限制后的炮管方向；炮口从车体 AABB 求出口并外移 0.75 格；弹丸创建后恢复伤害 owner；1.5 格内不解锁爆炸。
- 规则测试：已确认。新增 AABB 前向/侧向出口、炮口净距和引信距离边界测试并通过。
- Release 构建：已确认。目标 x25.01.17 环境检查、Kelly 11 项、ATV 23 项、Tank 规则测试、net472 编译、XDB 与包内容校验均通过。
- `.netmod`：`artifacts/SCIEPort.netmod`，SHA-256 `79495495B2BFB4AA2CFBDF2777F22F73A8280BB5D0F07C110A1D668830CDC5A3`。
- Windows 游戏运行：待验证，本轮未启动游戏。
- Android v0.3.1：待验证，必须复测炮塔转向、第一炮安全、贴近障碍开炮、远距离命中爆炸。
- 多人联机：不支持。

## 0.10.0 ATV 双座越野摩托 v0.3（2026-10-20）

- 技术审计：已确认。目标 `ComponentMountPackage` 无座位编号，同实体双 Mount 不可寻址；原生 Body 碰撞只排除直接父子，故放弃会让乘客碰撞祖父车体的多层锚点方案。详见 `ATV-V0.3-DUAL-SEAT-AUDIT.md`。
- 座位架构：已确认（代码/ABI）。ATV 保持 ID 923、`SCIEPortATV` 和实体 GUID；两名骑手直接挂载 ATV Body，主机/Local 分配驾驶员与乘客，乘客不被 Drive 读取。临时玩家 ID 不写存档，角色由保存的原生座位偏移恢复。
- 联机包：已确认（ABI/编译）。自定义 `IPackage` ID 240 只由服务器下发离散座位事件；目标原生包实际占 ID 0–38。服务器忽略客户端伪造的同类包，普通客户端不改车辆速度。
- 模型与贴图：已确认（静态/目标导入）。项目脚本生成 Y_UP 单骨骼、12 meshes 的双座越野摩托和 32×8 四色不透明 PNG；目标 `Engine.Media.ModelData.Load` 导入为 1 bone / 12 meshes，模型范围落在 `1.1,1.25,2.6` Body 内。
- 规则测试：已确认。`SCIEPort.LandVehicles.Tests` 共 32 项通过，其中新增 9 项覆盖驾驶优先、乘客分配、满员拒绝、重新占座、驾驶权限和固定独立偏移。
- Release/包检查：已确认。`Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17 -Configuration Release` 通过；固定程序集/包 ID ABI、Kelly 11 项、ATV 32 项、Tank 规则、net472 Release 0 warning/0 error、XDB、模型和包内容守卫全部通过。
- `.netmod`：已确认。`artifacts/SCIEPort.netmod`，381,492 字节，SHA-256 `3818392DD24BEECF834080C56CAD73DC0E147BB29F70B18231630BE04C8AB3AA`，版本 0.10.0。
- Windows 游戏运行：待验证。遵照用户要求，本机未启动游戏。
- Android 7723 单人：待验证。新模型显示、驾驶、一格越障、上下车及保存重载尚未实测。
- Android 多人：待验证。目标仅为主机驾驶员 + 普通客户端乘客；双人移动、自由观察、上下车、第三人拒绝、死亡/断线及重载需双机测试。
- 普通客户端驾驶：不支持。原生骑乘 `BodyUpdate` 会同步父车辆状态，必须另做服务器输入、预测和纠偏协议后才能安全开放。

## 0.11.0 双人快艇（2026-10-20）

- 目标环境检查：已确认。`scripts/Test-Environment.ps1` 对 `E:\SCNET\SCNETx25.01.17` 验证
  `Survivalcraft.exe` 2.4.0.0、API 1.42、SC 2.4.10.6、net472 及所需载具 ABI。
- Release 编译：已确认。`scripts/Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17
  -Configuration Release` 完成，0 warning、0 error。
- 规则测试：已确认。Kelly 11 项、ATV 32 项、快艇 16 项及坦克规则全部通过。快艇覆盖模型
  边界、座位间距、上下船位置、水面门控、推进限速和首人驾驶/次人乘客/第三人拒绝。
- 数据库：已确认（静态）。四份 XDB 验证通过；快艇保留 ID 999、实体名/GUID与唯一原生 Mount，
  含共享双座管理器且不含会接受所有骑手输入的原生 Boat。
- 模型：已确认（静态/目标导入）。Y_UP、`Cylinder`、98 顶点和 2.3×1.75×5.6 边界守卫通过；
  目标 `Engine.Media.ModelData.Load` 实际导入为 1 bone/1 mesh。派生模型 SHA-256
  `E399E94FCEE8ABBEB8BCEFA04369E8A57C8ED31B72A6A5A9B38C3D71D5B1EBCF`。
- `.netmod`：已确认（打包级）。`artifacts/SCIEPort.netmod`，382,932 字节，SHA-256
  `25442AD084930955B6E8458EE5DD7860066D71A94C53C7D1A723C6E9A73EB852`，内置版本 0.11.0；
  包清单、PNG 限制和禁止游戏 DLL 规则通过。
- Windows 游戏运行：待验证。遵照用户要求，本机未启动游戏。
- Android 7723 单人：待验证。生成、显示、驾驶、两座上下车、沉没及保存重载尚未实测。
- Android 双人联机：待验证。支持目标为主机驾驶员 + 普通客户端乘客；普通客户端驾驶员不支持。

详细设计和十项 Android 验收步骤见 `docs/SPEEDBOAT-V0.4-DUAL-SEAT-AUDIT.md`。

## 0.12.0 双人越野车 v0.1（2026-10-20）

- 目标环境：已确认。`Test-Environment.ps1` 验证固定 `E:\SCNET\SCNETx25.01.17` 的
  `Survivalcraft.exe` 2.4.0.0、API 1.42、SC 2.4.10.6、net472 和所需载具 ABI；ID 925
  未被 260 个原生方块常量占用。
- Release 编译：已确认。`scripts/Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17
  -Configuration Release` 通过，0 warning、0 error。
- 回归与规则：已确认。Kelly 11 项、ATV 32 项、双人越野车 20 项、快艇 16 项和坦克规则
  全部通过。新测试覆盖尺寸、座位、地面/实体/深水门控、加速、倒车、制动、滑行、限速、
  转向和驾驶权限。
- 数据库：已确认（静态）。五份 XDB 验证通过；`SCIEPortOffroadCar` 含唯一原生 Mount、共享
  双座管理器、独立 Drive、SimpleModel、Spawn、Body 和 Damage，且 `AutoDespawn=False`。
- 模型与贴图：已确认（静态/目标导入）。项目原创 `OffroadCar.dae` 为 Y_UP、单 identity node、
  24 个独立 cuboid geometry；目标 Engine 实际导入为 1 bone/24 meshes。可见范围完整落入
  2.4×1.55×4.0 Body；40×8 PNG 含五种不透明颜色。
- `.netmod`：已确认（打包级）。`artifacts/SCIEPort.netmod`，390,558 字节，SHA-256
  `D4090670D2B5F7390A8BCD990A7EF9FC268D36974CDE33A0C46A648C27BF29A2`，版本 0.12.0；
  包资源映射、模型导入、PNG、XDB、重复类名和禁带游戏 DLL 检查全部通过。
- Windows 游戏运行：待验证。遵照用户要求，本机未启动游戏。
- Android 7723：待验证。物品栏、世界模型、驾驶、一格台阶、碰撞、双座、下车和保存重载待实测。
- Android 多人：待验证。支持目标为主机驾驶员 + 普通客户端副驾驶；普通客户端驾驶员不支持。

尺寸依据、原创资源说明和 Android 验收步骤见 `docs/OFFROAD-CAR-V0.1-AUDIT.md`。

## 0.12.1 坦克点击自爆二次修复（2026-10-20）

- Android 反馈：已确认缺陷。用户确认安装含 v0.3.1 炮口修复的后续移植包后，坦克点击开炮仍会自爆。
- 源码根因：已确认。既有引信只要求弹丸从炮口移动 1.5 格；42 格/秒弹丸可在一个物理步内越过该阈值，而爆点仍位于压力 18 能覆盖坦克的近地/近墙区域。
- 修复：已完成源码。保留 AABB 外炮口和创建后恢复 owner；爆炸格中心到坦克当前 AABB 还必须至少为 8 格，否则炮弹只消失且记录一次安全抑制日志。
- 纯规则测试：已确认。Kelly 11 项、ATV 32 项、双人越野车 20 项、快艇 16 项及 Tank 测试均通过；Tank 新增车旁已解锁弹、远处未解锁弹、8 格边界与点到 AABB 最近距离断言。
- `git diff --check`：已确认，通过。
- 环境检查：已确认。Windows PowerShell 下 `Test-Environment.ps1 -GameDir D:\SC` 验证三份固定哈希、程序集 2.4.0.0、API 1.42、SC 2.4.10.6、net472、260 个原生方块常量及载具 ABI。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir D:\SC -Configuration Release` 完成全部规则、net472 编译、五份 XDB、模型/PNG/清单与禁带 DLL 守卫；`artifacts/SCIEPort.netmod` 为 390,766 字节，内置程序集版本 0.12.1.0，SHA-256 `228E3DFC3F15439C97023794684161423F3DC4336F231942A6F43B9825C4D63F`。
- Windows 游戏运行：待验证。
- Android 7723：待验证。需卸载旧同名包并冷启动后复测第一炮、连续点击、近地/贴墙发射，以及 8 格外命中爆炸。
- 多人联机：不支持，结论不变。

## 0.12.2 坦克炮塔、无限耐久与破坏性主炮（2026-10-21）

- 固定内容包取证：已确认。只读解包 `E:\SCNET\SCNETx25.01.17\Content.scpak`（SHA-256
  `13A86223C791D23093D9DF15D9E424F8ADF5BEB0FE1BE8DFB7AC62ECC07E6C34`），`Assets/BlocksData.txt`
  记录 ID 107 `LargeGunpowderKegBlock.DefaultExplosionPressure=2160`；小/中型桶分别为 60/360。
- 炮塔规则：已确认（源码/规则测试）。活动摄像机连续给出目标角，炮塔以 1.2 rad/s、炮管以
  0.65 rad/s 逐步追随；点击只触发开火，炮弹方向由当前保存的 `TurretYaw/GunPitch` 计算。
  新测试覆盖转速上限、跨 ±π 最短转向、俯仰速率及无过冲。
- 无限耐久：已确认（XDB/打包守卫）。`SCIEPortT34Tank` 不再包含 `ComponentDamage`；完整构建会在
  Damage 被重新加入时失败。此结论只表示原生生物/爆炸耐久不可扣减，不包括强制移除实体或乘员免伤。
- 爆炸：已确认（源码/固定内容包/编译）。主炮压力由 18 提升为 2160，继续复用原生
  `SubsystemExplosions.AddExplosion`，因此具备原生方块破坏链；发射载具安全包络由 8 扩至 12 格。
- 自动测试：已确认。Kelly 11 项、ATV 32 项、双人越野车 20 项、快艇 16 项、Tank 规则全部通过；
  `git diff --check` 通过。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17
  -Configuration Release` 完成 ABI、net472、五份 XDB、模型/PNG、包清单和禁带 DLL 校验，0 warning、
  0 error。`artifacts/SCIEPort.netmod` 为 390,936 字节，SHA-256
  `F23737C1570216553446B14A43047F7D1108D34A50D43D24AF887FD6A5AB2924`，版本 0.12.2。
- Windows 游戏运行：待验证。遵照用户要求，本机未启动游戏。
- Android 7723：待验证。必须在可丢弃世界验证炮塔可见转动、炮弹沿炮管、方块破坏、12 格近爆
  抑制、狼连续攻击不消失、乘员是否受爆炸波及，以及保存重载。
- 多人联机：不支持。驾驶、瞄准和开火仍仅在 `WorkType.Local` 执行。

## 0.12.3 大型黑色炮弹与首次命中爆炸（2026-10-21）

- 原生机理：已确认。目标 `SubsystemProjectiles.Update` 在碰撞后仍会按命中面把速度乘以
  `-0.3/0.3` 形成反弹；`ProjectileStoppedAction.Disappear` 只在速度低于停止阈值后执行，不能保证命中即爆。
- 炮弹外观：已确认（源码/编译）。新增内部 ID 926 `TankShellBlock`，复用目标运行时
  `Models/Snowball`/`Snowball` 网格，不复制或打包游戏模型；纯黑绘制，使用原生 0.3 投射物尺寸的
  3.5 倍比例，并从创造栏枚举中隐藏。
- 命中处理：已确认（代码/ABI/XDB）。新增 `SCIEPortTankShellBehavior`，在首次 terrain/body 碰撞
  回调中按命中格触发已有引信、安全包络和压力 2160 爆炸，随后从所有权表移除并标记 projectile 删除；
  `ProjectileRemoved` 不会重复爆炸。
- 数据守卫：已确认。ID 926 未与 260 个原生方块常量或本工程 ID 冲突；完整构建要求行为已挂入
  Project，并固定 `DisintegratesOnHit=TRUE`、`IsStickable=FALSE`、`ProjectileDamping=1`。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17
  -Configuration Release` 完成 Kelly 11、ATV 32、越野车 20、快艇 16、Tank 规则、net472、五份 XDB、
  模型/PNG/清单及禁带 DLL 校验，0 warning、0 error。`artifacts/SCIEPort.netmod` 为 392,066 字节，
  SHA-256 `946D2E35BF197B9A4234CFCCD7C6FF8698CA8A655DC6242FA5BA30C215215F2D`，版本 0.12.3。
- Windows 游戏运行：待验证。遵照用户要求，本机未启动游戏。
- Android 7723：待验证。需验证黑色球形炮弹尺寸和可见性，并分别命中石头、木材、玻璃、斜面、
  实体及水面，确认首次碰撞爆炸且无可见反弹、无重复爆炸。
- 多人联机：不支持，结论不变。

## 0.13.0 首个狐狸生物移植验证（2026-10-21）

- 上游基线：已确认。`SurvivalcraftTemplateModForAPI` 固定为
  `a8a9f09aea2ede595b2d677f10907772a2a716c3`；`AdvancedGltfFoxModForSC`
  固定为 `82c1ea541751045804968a71216f9153e2dd400e`。两个仓库 LICENSE 均为 MIT。
- 素材许可：已确认/部分待验证。模板 `Fox.gltf` 自述为 CC BY 4.0
  第三方模型；高级 `Fox2.glb` 没有内嵌原始出处。本轮均未复制或打包，包内
  `Fox.dae`/`Fox.png` 为项目脚本从零生成。
- 目标 ABI：已确认。`Test-Environment.ps1` 对固定 SHA-256 的
  x25.01.17 程序集反射确认 `ComponentCreature`、
  `ComponentFourLeggedModel.SetModel`、`ComponentCreature.ConstantSpawn` 和
  `SubsystemCreatureSpawn.SpawnCreature(string, Vector3, bool)`；260 个原生
  Block.Index 中未占用 927。
- XDB：已确认（静态）。`Test-Database.ps1` 扫描六个 XDB 通过；
  `Creatures.xdb` 使用真实原版 Entities/BlockBehaviors GUID，实体继承目标
  Wolf GUID，生成行为正确挂入 Project，没有声明 `CreatureEggData`。
- 模型/贴图：已确认（离线）。目标 `Engine.Media.ModelData.Load` 实际导入
  `Fox.dae` 为 6 bones/13 meshes，必需骨骼均存在；`Fox.png` 为 32×8、四种
  全不透明颜色，包内引用与 UV 结构检查通过。
- 自动回归：已确认。Kelly 11、ATV 32、越野车 20、快艇 16、Tank 规则测试
  全部通过；`git diff --check` 待提交前复核。
- Release / `.netmod`：已确认。执行
  `Build-NetMod.ps1 -Configuration Release` 完成环境、net472、XDB、目标模型
  导入、PNG、包清单和禁带 DLL 校验，0 error。最终产物 398,264 字节，SHA-256
  `0440DFFF7B960B346D87C4DB3ED3F2E72575D8AA921C465CDC44E9A68995D47D`，版本 0.13.0。
- Windows 游戏运行：待验证。遵照用户要求，本机未启动游戏。
- Android 7723：待验证。需验证创造栏图标、生成、模型比例、UV、四足动画、
  AI、碰撞、死亡和退出重进恢复；不得因编译或离线导入通过标记为兼容。
- 自然生成：不支持。第一阶段没有添加任何自然生成注册或地形 Hook。
- 多人联机：不支持。普通 Client 不会在本地创建狐狸，但尚未实现请求、服务端
  确认、实体复制和断线状态专项协议。

## 0.14.0 玉米作物垂直切片（2026-10-21）

- 自动回归：已确认。离线规则测试全绿——Kelly 11、ATV 32、越野车 20、快艇 16、
  Tank 规则、Ecology 25，新增 **Crops 26**（尺寸掩码往返、掩码外位保留 0x40、
  成熟阈、生长间隔干 8/水利 6/水肥 4/低温 12/高温 3、放置需顶面土壤、收获 1 棒
  + 2 种、权限闸 `!isClient`）。Crops.Tests 与 Ecology.Tests 均已接入
  `Build-NetMod.ps1`（修正兔子阶段遗留的 Ecology.Tests 未接线空档）。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 0
  compile warning/error（`TreatWarningsAsErrors` 开启），DATABASE VALIDATION
  PASSED，44 条目（新增 `Assets/Blocks/Crops.csv`、`Assets/Database/Crops.xdb`、
  `Assets/Textures/Corn.png`），产物 406,085 字节，SHA-256
  `77376DC4506B0891F0F0B524C2DD1C2FD7D2F1FAC3B146F8932EBA903C12DAD2`。
- DLL 版本反射：已确认。`[AssemblyName]`/`FileVersion` = `0.14.0.0`，
  `ProductVersion` = `0.14.0+c325d213e5bd133b52c0f54db3d5466e3103ea9d`。
- 图集取证：已确认（原生资产，非自证）+ 待验证（自绘渲染）。方块面网格 slot→
  像素公式（每行 16 格 / 32×32，容量 256）已从游戏自带 `Blocks.png`（512×512
  RGBA）对 5 个原生方块语义颜色取样确认；自绘 `Corn.png` 结构断言（尺寸/格式/
  各 slot 不透明像素/留空 slot）通过，但实际加载采样待真机验证。
- Windows 游戏运行：待验证。遵照要求本机未启动游戏；种植、逐阶段生长轮询、
  成熟收获掉落、存读档均未运行。
- Android 7723：待验证。玉米三块渲染、贴图、alpha 测试、创造栏图标、生长动画、
  收获与退出重进恢复均需真机新世界验证，不得因编译或离线测试通过标记为兼容。
- 多人联机：待验证。轮询行为已在 `WorkType.Client` 直接返回，仅权威端推进，
  但真实主机/客户端同步与生长一致性未实测。

## 0.15.0 竹子作物垂直切片（2026-10-21）

- 自动回归：已确认。离线规则测试全绿——Kelly 11、ATV 32、越野车 20、快艇 16、
  Tank 规则、Ecology 25、Crops 26，新增 **Bamboo 25**（基段位往返/幂等、掩码外
  位 0x40 与基位共存、上段判定、`CanExtend` 上下界、间隔干暖 3/水利 2/低温 6/
  水寒 5、放置需顶面有效基座、砍伐每段掉 1、权限闸 `!isClient`）。
  Bamboo.Tests 已接入 `Build-NetMod.ps1`。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 0
  compile warning/error（`TreatWarningsAsErrors`），DATABASE VALIDATION PASSED，
  45 条目（新增 `Assets/Blocks/Bamboo.csv`、`Assets/Database/Bamboo.xdb`、
  `Assets/Textures/Bamboo.png`），产物 SHA-256
  `CDABF44485DEA3BEC017BA80FE490C3820E60C7FA618BF45B67CFCD239BA40EA`。
- DLL 版本反射：已确认。`[AssemblyName]`/`FileVersion` = `0.15.0.0`，
  `ProductVersion` = `0.15.0+758712cc...`（玉米 commit c325d21 之后）。
- 实心立方体 API 取证：已确认。反射 x25.01.17 实测
  `GenerateCubeVertices(...,TerrainGeometrySubset[])` + `DrawCubeBlock(...,Texture2D)`
  重载存在，`SoilBlock/SandBlock/DirtBlock` 均有公开静态 `Index`。
- 自绘图集：结构已确认（打包断言 512×512 RGBA、slot 0..2 有像素、slot 2 全 1024
  不透明、slot 5 全透明）；真机采样/alpha/图标 待验证。
- Windows 游戏运行：待验证。本机未启动游戏；种植、逐段堆叠、砍伐塌落、建材放置、
  存读档均未运行。
- Android 7723：待验证。两方块渲染、生长、塌落、建材碰撞与退出重进均需真机新世界
  验证，不得因编译或离线测试通过标记为兼容。
- 多人联机：待验证。轮询已在 `WorkType.Client` 直接返回，仅权威端堆叠，真实同步
  一致性未实测。

## 0.16.0 亚麻作物垂直切片（2026-10-21）

- 自动回归：已确认。离线规则测试全绿——Kelly 11、ATV 32、越野车 20、快艇 16、
  Tank 规则、Ecology 25、Crops 26、Bamboo 25，新增 **Flax 29**（尺寸掩码往返、
  掩码外位 0x40 保留、成熟阈、间隔干 7/水利 5/水肥 3/低温 13/高温 6/理想 2、
  放置需顶面土壤、收获 2 纤维 + 1 种子、权限闸 `!isClient`）。Flax.Tests 已接入
  `Build-NetMod.ps1`。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 0
  compile warning/error（`TreatWarningsAsErrors`），DATABASE VALIDATION PASSED，
  47 条目（新增 `Assets/Blocks/Flax.csv`、`Assets/Database/Flax.xdb`、
  `Assets/Textures/Flax.png`），产物 SHA-256
  `6A9C19EDE0692B7DED34DEF2E0AF7E0F83FE1F70FF88FA409170FC66F3CF798B`。
- DLL 版本反射：已确认。`[AssemblyName]`/`FileVersion` = `0.16.0.0`，
  `ProductVersion` = `0.16.0+13b7579f...`（竹子 commit 之后）。
- 自绘图集：结构已确认（打包断言 512×512 RGBA、slot 0..8 有像素、slot 12 全透明；
  生成器自检阶段 0..7 不透明像素单调增长）；真机采样/蓝花渲染/图标 待验证。
- Windows 游戏运行：待验证。本机未启动游戏；种植、逐阶段生长、成熟收获（纤维 +
  可重种种子）、掉落、存读档均未运行。
- Android 7723：待验证。两方块渲染、贴图、种植交互、生长轮询、收获与退出重进恢复
  均需真机新世界验证，不得因编译或离线测试通过标记为兼容。
- 多人联机：待验证。轮询已在 `WorkType.Client` 直接返回，仅权威端推进，真实同步
  一致性未实测。

至此第一批资源（玉米、竹子、亚麻 + 前置的狐狸/兔子）基础功能全部落地并单独提交，
均处于「静态/离线已确认、真机待验证」状态，等待统一联网/真机验证阶段。

## 0.17.0 自然生成 spawner 垂直切片（2026-10-21）

- 自动回归：已确认。离线规则测试全绿——Kelly 11、ATV 32、越野车 20、快艇 16、
  Tank 规则、Crops 26、Bamboo 25、Flax 29，Ecology 由 25 增至 **37**（新增 12 条：
  几率单调/上下界、`FoxSpawnChancePercent<RabbitSpawnChancePercent`、每块预算到限/越限）。
  本阶段实测发现 `ChanceMet(-1,100)` 因 `chance>=100` 短路先于 roll 越界守卫而误判命中，
  已将 roll 越界守卫置于短路之前并补断言，重跑 Ecology 37/37 通过。
- 静态 ABI 取证（先取证再动手）：已确认。反射 x25.01.17 实测
  `TerrainChunk.GetTemperatureFast/GetHumidityFast→Int32`、`Coords.X/Y`、`Origin.X/Y`、
  `GetTopHeightFast/GetCellValueFast`、`SubsystemGameInfo.WorldSeed→Int32`、
  `SubsystemCreatureSpawn.SpawnCreature(string,Vector3,bool)` 均存在；`Game.Random(seed).Int`
  沿用 Kelly 已验证用法；实体类型与狐/兔生成器一致全限定 `GameEntitySystem.Entity`，
  `Random` 全限定 `Game.Random` 规避 Engine/Game 二义。
- 生成器接线：已确认（代码）。复用已验证的 `OnTerrainContentsGenerated(TerrainChunk)`
  块生成钩子，未新增地形写入路径；`CreatureWorldSpawn` 不注册独立 XDB/CSV/GUID，
  改由狐/兔 `Subsystem...SpawnBlockBehavior.Load` 各调 `SetProject(Project)` 缓存子系统。
  加载器内植物散布与生物生成各自 `try/catch` 并 `Log.Error` 记录（不吞异常）。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 0 compile
  warning/error（`TreatWarningsAsErrors`），DATABASE VALIDATION PASSED，51 条目（本阶段
  无新增包内文件，复用 `Assets/Database/Creatures.xdb`、`Creatures.csv`），产物
  SHA-256 `9F12EF87A85C6304B1103EBCB0F1F1C7AB0B2BEF6CF4DF094EAD744A03153B90`
  （413,808 字节）。
- DLL 版本反射：已确认。`[AssemblyName]`/`FileVersion` = `0.17.0.0`，
  `ProductVersion` = `0.17.0+719fa2b7...`（亚麻 commit 之后、本阶段提交前工作树）。
- 生成器决策（离线纯逻辑）：已确认。种子确定性 `GetChunkSeed(WorldSeed, Coords.X/Y)`、
  权限闸 `!isClient`、每块预算 2、狐上限 3/兔上限 6、栖息度（温度/湿度/自然地面/树冠）
  全部委托 `EcologyRules`。
- 实体生成激活（生成期 `SpawnCreature`）：**待验证**。静态 ABI 与纯规则已确认，但在块生成
  回调内造实体能否在 Windows/Android 7723 稳定成功、不崩、不阻塞生成、不产生未同步实体
  尚未观测；按要求不提前宣称已修复。
- 已知缺口：生物死亡回收（存活计数递减）未接线，计数只增不减并钳到上限——最坏是过早停止
  生成（保守安全）而非过度繁殖；死亡事件接线留待后续阶段。
- Windows 游戏运行：待验证。本机未启动游戏；自然生成时机、生成实体存活、存读档、卸载均未运行。
- Android 7723：待验证。低端机性能、上限表现、与原生生物群系共存、退出重进恢复均需真机新世界
  验证，不得因编译或离线测试通过标记为兼容。
- 多人联机：待验证。已拒普通客户端生成，主机权威生成后向客户端的实体同步、房主/加入者一致性
  未实测。
- 生态互动（狐追捕兔）：待验证（未接线）。`IsEligiblePredation` 纯谓词已测，实际追捕仍委托
  原生 Wolf 衍生 AI，本阶段不接线，留作独立阶段。


## 0.18.0 生态互动：狐追捕兔（数据式委托原生 Wolf AI，2026-10-22）

- 自动回归：已确认。离线规则测试全绿——Kelly 11、ATV 32、越野车 20、快艇 16、
  Tank 规则、Crops 26、Bamboo 25、Flax 29，Ecology 由 37 增至 **47**（新增 10 条
  捕食接线断言，核心不变量「狐 `AutoChaseMask` 必须覆盖兔 `Category`」「狐
  `ChaseNonPlayerProbability` 必须高于狼基线 0.03」，另含 `IsCategoryChased`
  对 `"0"`/null/空/大小写/逐段 trim 的边界）。
- 捕食机制取证（先取证再动手）：已确认。反射 x25.01.17 确认原生
  `Game.ComponentChaseBehavior` 拥有 `FindTarget()`/`ScoreTarget(ComponentCreature)`/
  `IsTargetInAttackRange`，字段 `m_autoChaseMask : Game.CreatureCategory`；
  `Game.CreatureCategory` 为 `[Flags]`。再用 `Game.SurvivalCraftModEntity.GetDecipherStream`
  解出 `Content.scpak` 内真实 `Assets/Database.xml`（463,610 字节，987 条目）实测：基类
  `ChaseBehavior`(`31894441-…`) 默认 `AutoChaseMask="LandOther, Bird"`、
  `ChaseNonPlayerProbability=0`；`Wolf`(`49a9845c-…`，狐继承父) 只覆写
  `ChaseNonPlayerProbability=0.03`；`Alpaca`(`8e62c6e4-…`，兔继承父) `Category="LandOther"`。
  据此狐本就把 LandOther 列为可追对象、兔本就在其猎物集合内。
- 不臆造 API：已确认。`Database.xml` 基类 `ChaseBehavior` 参数集不含 `IsAttack`
  （`m_isAttack` 为运行时字段而非可序列化模板参数），本阶段未在模板里写 `IsAttack`；
  咬击伤害来自狐继承的狼 `Miner.AttackPower=1` 与追捕进入攻击距离后的原生结算。
- 数据式接线（委托原生 AI）：已确认（代码/数据）。仅在 `SCIEPortFox` `EntityTemplate`
  增加一个 `ChaseBehavior` `MemberComponentTemplate` 覆盖：`AutoChaseMask="LandOther"`
  （锁定陆生猎物，不再顺带含基类默认 Bird）与 `ChaseNonPlayerProbability="0.35"`
  （狼基线 0.03 过低几乎不主动猎非玩家动物，提高使捕食可观察）。不写任何 C# 转向/
  寻路/攻击逻辑，全部交给原生 `ComponentChaseBehavior`；匹配方式与工作区既有覆盖一致
  （`Name` 关联继承组件、`Parameter` 按 `Name` 覆写字段、各用唯一 Guid）。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 0 compile
  warning/error（`TreatWarningsAsErrors`），DATABASE VALIDATION PASSED，51 条目（本阶段
  无新增包内文件，改的是既有 `Assets/Database/Creatures.xdb`），产物
  SHA-256 `C063A77AE2E9F204154800D88001089350339F58A4CE7E7FEE8D496A098A61C8`
  （414,365 字节）。
- 打包校验：已确认。`Build-NetMod.ps1` 对 `SCIEPortFox` 断言存在 `ChaseBehavior` 覆盖、
  其 `AutoChaseMask` 逗号集合含 `LandOther`、`ChaseNonPlayerProbability` 可解析为 float
  且 > 0.03；缺失或过小即抛错。
- DLL 版本反射：已确认。`[AssemblyName]`/`FileVersion` = `0.18.0.0`，
  `ProductVersion` = `0.18.0+c5ba1de...`（自然生成 spawner commit 之后、本阶段提交前工作树）。
- 实机捕食激活：**待验证**。静态/数据 ABI 与纯规则已确认，但狐在真机是否确实发现、追捕
  并杀死兔、0.35 概率的手感与频率、与羊群互助/逃跑行为的交互、退出重进一致性，均未在
  Windows 客户端或 Android 7723 观测；按要求不提前宣称已修复。
- Windows 游戏运行：待验证。本机存在可运行的 `Survivalcraft.exe`（x25.01.17），但本阶段
  未启动游戏实测捕食行为。
- Android 7723：待验证。低端机上捕食 AI 的开销、可见性与稳定性需真机新世界验证，不得因
  编译或离线测试通过标记为兼容。
- 多人联机：待验证。追捕/攻击由实体权威端（Local 或主机/服务端）仿真，普通客户端不本地
  生成实体；权威端捕食结果向客户端的实体状态同步、房主与加入者观感一致性未实测。

至此自然生态与资源扩展的「基础功能 + 自然生成 + 生态互动（狐→兔）」均落地并单独提交，
处于「静态/数据 ABI 与离线测试已确认、实机行为待验证」状态，等待统一的 Windows +
Android 7723 联网/真机验证阶段。


## 0.19.0 批次2首片：向日葵作物（2026-10-22）

- 自动回归：已确认。离线规则测试全绿——新增 `SCIEPort.Sunflower.Tests` **33** 条
  （DATA 位往返/钳制保外位、成熟阈值、生长间隔含“冷减速>暖加速”温季签名、光照阈值
  10>亚麻 8 且 ≤15、顶面土壤放置、收获 4 食籽+1 可重种籽、权限闸）。既有套件随全链
  构建一并通过：Kelly 11、ATV 32、越野车 20、快艇 16、Tank 规则、Crops 26、Bamboo 25、
  Flax 29、Ecology 47。向日葵测工程已接入 `Build-NetMod.ps1`。
- 复用与取证（先取证再动手）：已确认。镜像亚麻切片（Rules/Blocks/Subsystem/CSV/XDB/
  atlas/Build 校验）；枚举 `Index = 9xx` 确认作物带止于 935、936/937 空闲；从真实
  `Database.xml` 检索确认原版无 sunflower/chicken/bee/apple（批次2皆 clean-room）；
  以 `KellyFruitBlock`（实为 `Block` 子类、可食性纯由 CSV `DefaultNutritionalValue` 驱动、
  已证 Android 可跑）为对照给种子设 Food/nutr 0.12/rotPeriod 160，不臆造食性 ABI。
- XDB/CSV：已确认。Sunflower.xdb 行为模板 `SCIEPortSunflowerBehavior` 继承原生 pollable 基
  `fefb9590-…`、`Class` 绑 `SCIEPort.Features.Sunflower.SubsystemSunflowerBlockBehavior`、
  Project 成员挂该行为 Guid；三个新 Guid 由 `randomUUID()` 生成全局唯一，仅复用允许重复的
  容器根。Sunflower.csv 由 Node 按列名从亚麻模板派生（禁手抄 CJK），UTF-8 无 BOM、CRLF；
  植株 Crops/可放/slot0、瓜子 Food/可放/slot8/nutr>0。DATABASE VALIDATION PASSED。
- 自绘图集：结构已确认。`New-SunflowerAssets.ps1` 产 `Sunflower.png` 512×512
  Format32bppArgb，slot 0..7 生长阶段 + slot 8 瓜子，其余透明；打包逐格断言 0..8 非空、
  slot 12 全透明（自检 slot 计数 0..8 递增 28→118，slot12=0）。真机渲染/开花/图标 待验证。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 exit 0
  （`TreatWarningsAsErrors` → 0 warning / 0 error），54 条目（新增
  `Assets/Blocks/Sunflower.csv`、`Assets/Database/Sunflower.xdb`、`Assets/Textures/Sunflower.png`），
  产物 417,793 字节，SHA-256
  `C08B52F2ECB9E8E31C6A5C1E1EA7A9B79ABDD6E4BAE69B4A36606763EB58D8C6`（磁盘复核一致）。
- DLL 版本反射：已确认。`[AssemblyName]`/`FileVersion` = `0.19.0.0`，
  `ProductVersion` = `0.19.0+5b793cf...`（捕食 commit 之后、本阶段提交前工作树）；二进制内
  确含五个向日葵符号（`GetTypes` 因 net472 DLL 引用 Engine/Game 无法在本机 PS 加载而抛
  ReflectionTypeLoadException，属探测方式局限，非构建问题；改用符号串扫描确认已编译进 DLL）。
- 已知缺口：批次2其余三项（鸡=新实体、苹果=树+果实新结构、蜂=飞行/群体风险最高）尚未开始；
  向日葵种子目前仅提供营养值，尚未接入“喂鸡/蜂”等跨切片互动（留待对应切片落地）。
- Windows 游戏运行：待验证。本机未启动游戏；种植、逐阶段生长、成熟收获、吃籽回饥饿、
  存读档、卸载均未运行。
- Android 7723：待验证。两方块渲染、贴图、种植交互、生长轮询、收获与退出重进恢复均需真机
  新世界验证，不得因编译或离线测试通过标记为兼容。
- 多人联机：待验证。轮询已在 `WorkType.Client` 直接返回，仅权威端推进，真实同步一致性未实测。

## 0.20.0 批次2第二片：鸡生物（复用生物切片管线，手动生成器阶段，2026-10-22）

- 自动回归：已确认（既有套件不变、全链绿）。本阶段为**生物切片**，与已验证的狐/兔同路径——
  不新增 net8 单测工程（狐/兔先例即无独立每生物测，靠 `Build-NetMod.ps1` 的**目标引擎离线导入
  `ModelData.Load`** 逐骨骼/网格校验 + XDB/CSV/打包断言兜底）。既有套件随全链构建一并通过且
  计数不变：Kelly 11、ATV 32、越野车 20、快艇 16、Tank 规则、Crops 26、Bamboo 25、Flax 29、
  Sunflower 33、Ecology 47。
- 复用与取证（先取证再动手）：已确认。逐字镜像狐/兔生物切片七件套（EntityTemplate 覆写 7 组件 /
  clean-room COLLADA 六骨骼 / 32×8 四色 PNG / SpawnBlock / SubsystemBlockBehavior / CSV 行 /
  构建校验）。反射前已从真实 `Database.xml` 检索确认原版无 chicken/hen/rooster（clean-room）。
  枚举 `Index = 9xx` 确认作物带止于 937、938 空闲、995+ 载具保留；鸡取 938、DisplayOrder 911。
  鸡继承 Alpaca（`8e62c6e4-…`）得 `Category=LandOther`，数据层天然落入狐 `AutoChaseMask`
  捕食目标集（仅记录结论，追捕仍待真机）。
- 四足 rig 适配：已确认。鸡仅两腿，但保留原生强制六骨骼完整性（Body/Head/Leg1-4）：前二骨骼挂
  可见腿、后二骨骼挂靠后短腿，读作鸟 stance。`Build-NetMod.ps1` 用目标 `Engine.Media.ModelData.Load`
  实测导入 `Chicken.dae` 为 **6 bones / 10 meshes** 并逐骨骼名校验、Y_UP 断言；`New-ChickenAssets.ps1`
  产 `Chicken.png` 32×8 四色不透明，Build 断言每像素 A=255、恰 4 种 ARGB（白/红/橙/暗）。
- XDB/CSV：已确认。`Creatures.xdb` 加 `SCIEPortChicken` 实体（覆写 7 组件、DisplayName「SCIE 鸡」、
  ModelName/TextureOverride 指向 Chicken、AutoDespawn False、不注册原生 CreatureEggData）+ 行为
  `SCIEPortChickenSpawnBehavior`（继基 `fefb9590-…`、Class 绑 `SCIEPort.Features.Creatures.
  SubsystemChickenSpawnBlockBehavior`）+ Project 成员；实体/行为/成员各用 `randomUUID()` 全局唯一，
  仅复用允许重复容器根。`Creatures.csv` 鸡行由 Node 按列名从**已验证兔行**派生（`鸡` 字直接取自
  xdb 已提交 DisplayName，禁手抄 CJK），UTF-8 无 BOM、CRLF。DATABASE VALIDATION PASSED。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 exit 0
  （`TreatWarningsAsErrors` + `GenerateDocumentationFile` → 0 warning / 0 error），54 条目（新增
  `Assets/Models/Chicken.dae`、`Assets/Textures/Chicken.png`），产物 421,326 字节，SHA-256
  `246CFD35A066D90BAE2116E293FAD4A320D9540BD45E668C991A450F434EEDC9`（磁盘复核一致）。
- DLL 版本反射：已确认。`[AssemblyName]`/`FileVersion` = `0.20.0.0`，`ProductVersion` =
  `0.20.0+2151279...`（SourceRevisionId 取构建时 HEAD，即向日葵 commit 之后、本阶段提交前工作树）；
  符号串扫描确认 `ChickenSpawnBlock`/`SubsystemChickenSpawnBlockBehavior`/`SCIEPort.Features.Creatures`
  已编译进 DLL（`GetTypes` 因 net472 DLL 引用 Engine/Game 无法在本机 PS 加载而抛
  ReflectionTypeLoadException，属探测方式局限、非构建问题）。
- 已知缺口：本阶段鸡仅**创造栏手动生成器**，`Load` 刻意不调 `CreatureWorldSpawn.SetProject`，
  不接自然生成；不接线吃籽/下蛋等跨切片互动（避免臆造 ABI）。批次2剩余两项（苹果=树+果实新结构、
  蜂=飞行/群体风险最高）尚未开始。
- Windows 游戏运行：待验证。本机未启动游戏；图标方块渲染、使用生成、行走/群居/逃跑 AI、碰撞、
  死亡与存读档均未运行。
- Android 7723：待验证。图标、模型、UV、程序化步态、碰撞、生成与退出重进恢复均需真机新世界验证，
  不得因编译或离线导入校验通过标记为兼容。
- 多人联机：待验证。生成已在 `WorkType.Client` 直接返回，仅权威端生成实体，真实同步一致性未实测。

## 0.21.0 批次2第三片：苹果结果实植物（复用作物管线 + 野果结果位）

- 架构选型：已确认。用户选定「复用结果实植物范式（低风险）」。苹果 = 成熟前按已验证亚麻/
  向日葵的 DATA 低位分阶段生长，成熟后按已审计 `WildBerryPlantBlock` 的 Dedicated 结果位
  （DATA bit 3、`GetFruiting/SetFruiting`、季节日驱动、破坏结果丛掉果）承载可摘果实。完整
  sapling→树干体需未反编译的私有 `Game.SubsystemSaplingBlockBehavior`/Saplings ParameterSet，
  本阶段明确不支持。反射前已从真实 `Database.xml` 检索确认原版无 apple（clean-room）；枚举
  `Index` 确认作物带止 937、938 鸡、939/940 空闲、995+ 载具保留，苹果植株取 939、果实取 940。
- 温带作物签名：已确认（非向日葵换名）。基础间隔 10（木本慢于一年生 8）、水/氮各 −2、冷（≤2）+6
  硬冻停摆、普通温暖**不加速**（对照向日葵暖 ≥12 −2）；`MinLightToGrow=10` 全日照；仅秋季结果季
  且成熟方结果（`ShouldBearFruit = 成熟 && 结果季`）。
- 纯逻辑离线测试：已确认。`tests/SCIEPort.Apple.Tests`（net8，无游戏依赖，`dotnet run` exit 0）
  **49 断言**：size 0..7 往返、越界钳制且保结果位、结果位往返、结果位与 size 独立、成熟阈值、
  生长间隔（10/8/6/16 + 温带「热==温和、冷>温和」与「冷减速>暖加速」对照 + 下限 1）、光照
  （==10 且 >8、≤15）、结果季门、放置（顶面&&土壤）、收获（2 果 + 1 重种株）、权限闸 `!isClient`。
- 自绘图集：结构已确认。`New-AppleAssets.ps1` 产 `Textures/Apple.png` 512×512 Format32bppArgb、
  16×16 格 32px；slot 0..7 生长（绿芽→幼苗→木质丛→树冠）、slot 8 结果态缀红果、slot 9 单枚苹果，
  其余透明。Build 逐格像素断言 0..9 非空（自检不透明计数 slot0=30→slot7=408、slot8=425、slot9=153、
  slot12=0）、slot12 全透明。真机渲染/图标待验证。
- XDB/CSV：已确认。`Apple.xdb` 加行为 `SCIEPortAppleBehavior`（继原生 pollable 基 `fefb9590-…`、
  `Class` 绑 `SCIEPort.Features.Apple.SubsystemAppleBlockBehavior`）+ Project 成员；行为/Class/成员
  各用 `randomUUID()` 全局唯一，仅复用允许重复容器根（`00c97f0f-…`/`85023bf8-…`）。曾修一处手抄
  遗留——Project 成员 `InheritanceParent` 初指废弃 Guid，已全量替换为 `d7c6e2aa-…` 并复核。
  `Apple.csv` 初生成缺 79 列表头且为 LF，已 Node 从已验证 `Sunflower.csv` 取纯 ASCII 表头前置两数据行、
  改 UTF-8 无 BOM、CRLF（CR=行=3、每行 79 列）：植株 `ApplePlantBlock;苹果树;Crops;…;339;…slot0`、
  果实 `AppleBlock;苹果;Food;…;340;…nutr 0.13;…rot 160;…slot9`。DATABASE VALIDATION PASSED。
- Release / `.netmod`：已确认。`Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 exit 0
  （`TreatWarningsAsErrors` + `GenerateDocumentationFile` → 0 warning / 0 error），`.netmod` zip 实测
  **59 条目**（requiredEntry 校验清单 57 项，另有 modinfo/SCIEPort.dll/THIRD-PARTY-NOTICES 及未逐项校验的既有资产；含新增
  `Assets/Blocks/Apple.csv`、`Assets/Database/Apple.xdb`、`Assets/Textures/Apple.png`），产物 425,632
  字节，SHA-256 `30C62468FF6D58EE3734BABD6AB3AEEF57AED013658F040E82923E4DD37DE038`（磁盘复核一致）。
- DLL 版本反射：已确认。`[AssemblyName]`/`FileVersion` = `0.21.0.0`，`ProductVersion` =
  `0.21.0+8eac0a4…`（SourceRevisionId 取构建时 HEAD）。按「命名空间串 + 裸类型名分别匹配」的
  latin1 符号扫描确认 `SCIEPort.Features.Apple` 与 `AppleRules/ApplePlantBlock/AppleBlock/
  SubsystemAppleBlockBehavior/AppleTextures/ApplePlacement/GetAppleGrowthInterval/ShouldBearFruit/
  GetAppleYield/MinLightToGrow` 均已编译进 DLL（连写全名探测为假阴，因 net472 元数据将 namespace 与
  name 分列堆存储；同名 sunflower sanity 通过证探测法有效）。
- 已知缺口：本阶段苹果仅「可种植/生长/结果/摘取的静态数据切片」，未接跨切片互动（如鸡啄食苹果/籽、
  蜂授粉对结果的加成——蜂本身尚未开始）。批次2剩余一项（蜂=飞行/群体，风险最高）尚未开始。
- Windows 游戏运行：待验证。本机未启动游戏；种植、逐阶段生长、成熟转结果季、摘果/掉果、吃苹果回饥饿、
  存读档、卸载均未运行。
- Android 7723：待验证。两方块渲染、贴图、种植交互、生长/结果轮询、季节判定、收获与退出重进恢复均需
  真机新世界验证，不得因编译或离线测试通过标记为兼容。
- 多人联机：待验证。轮询已在 `WorkType.Client` 直接返回、仅权威端推进生长/结果，真实同步一致性未实测。

## 0.21.1 联机权限加固

- 权威模型取证：已确认。反射真实 `Survivalcraft.exe` 得 `Game.NetWork.WorkType` =
  `Local=0` / `Server=1` / `Client=2`，权威端为 `Local||Server`；目录含 `LiteNetLib.dll`+`MessagePack.dll`
  证实真 client/server 架构。据此把全部「改世界/生成实体」路径统一到「Client 让位」。
- 对称权限闸补齐：已确认（12 处，Release 0 warning / 0 error）。5 作物 `OnNeighborBlockChanged` 自毁
  （Apple/Sunflower/Corn/Flax/Bamboo）补 `WorkType.Client → return`；6 载具生成 `OnUse`（ATV/越野/蒸汽船/
  飞艇/轨道车/坦克）补 `WorkType.Client → return true`（镜像生物生成器）；铁轨 `OnBlockAdded` 自动连接
  `ChangeCell` 补 `Client → return`。各文件加 `using Game.NetWork;`。DATABASE VALIDATION PASSED。
- 载具联机现状：分类记录。坦克/轨道车/飞艇控制 `WorkType != Local` 硬锁单人，联机**不支持**（非未测）；
  ATV/越野/蒸汽船/双人座为联机感知代码，**待验证**。上游 Kelly 植物 neighbor 反应沿用原逻辑，本次**未**加固，
  列为同类待评估项。
- 部署约束：同一 `SCIEPort.netmod`（0.21.1）须主机与每个客户端同版本安装，否则未知 ID 缺失端报错。
- 构建 / DLL：已确认。`Build-NetMod.ps1 -GameDir 'D:\SCNET'` 全程 exit 0，59 条目，`FileVersion`=`0.21.1.0`、
  `ProductVersion`=`0.21.1+f3e4ffc`；产物 425,885 字节，SHA-256
  `0930726234B4D38092E9CA6E73C3191DE3BE35FF52DB004F731BF26E4F8C0717`（磁盘复核一致）。
- 运行时联机同步：待验证。静态审计只证明「代码仅权威端写世界」，无法证明同步包实际行为；作物 DATA/实体生成
  回同步、`OnUse`/`OnNeighborBlockChanged` 触发端、载具物理跨端一致均需主机+客户端两台实测（见 COMPATIBILITY
  0.21.1 三档实测清单）。Windows/Android 7723 联机均未开测。

## 0.23.0 荒野探索者 X1 摩托外观 V2

- 生成可复现：已确认。`scripts/New-AtvAssets.ps1 -ProjectRoot <repo>` 从纯参数化几何核（Frustum/Tube/Loft/
  Torus/ArcPlate/BoxA，内嵌 C#5）确定性产出 `src/SCIEPort/Assets/Models/ATV.dae` 与
  `src/SCIEPort/Assets/Textures/ATV.png`；不再堆叠小立方体模拟圆。旧 12 部件资产备份于
  `artifacts/backups/atv-pre-x1/`（gitignore，仅本地）。
- 部件与面数：已确认。13 个独立 geometry：FrontWheel 600 / RearWheel 600 / FrontFork 172 / Handlebar 220 /
  Headlight 60 / Frame 348 / Engine 252 / FuelTank 60 / Seat 60 / SidePanels 24 / Exhaust 136 / Swingarm 660 /
  Fenders 160，合计 **3352 三角面**（移动端预算 6000，构建断言强制）。
- 目标导入：已确认。`Build-NetMod.ps1` 反射目标 `Engine.Media.ModelData.Load` 实测 **1 bone / 13 meshes**，
  低于 SCNET 七骨骼实例着色器上限；单 identity `ATV` 场景节点、Y_UP、变换烘焙均通过断言。
- 包围盒：已确认。X [-0.371265, 0.371265]、Y [0.003218, 1.187]、Z [-1.286782, 1.286397]，完全落入
  `1.1,1.25,2.6` ComponentBody；碰撞/骑乘/联机/存档代码零改动，Block 923 与 `SCIEPortATV` GUID 不变。
- 贴图：已确认。64×64 不透明材质图集、33 色（断言上限 96），16 个带 1-texel 内缩的区域（橙/碳黑/银/铬/
  橡胶/座椅/灯等）；UV 全部位于 [0,1] 且逐区域隔离，构建断言逐 geometry 校验 accessor 步长、浮点个数、
  索引范围与 triangles count 一致性。
- 离线验收预览：已确认。`scripts/Render-AtvPreviews.ps1` 输出 7 视角 ×（白模/彩色）共 14 张于
  `artifacts/previews/atv-x1/`；目检 left/front/front45/rear45/top 均能识别为结构化越野摩托，排气管仅右侧、
  双座连续、油箱前宽后窄。
- 构建 / 打包：已确认。`Test-Environment.ps1 -GameDir D:\SC` 通过；`Build-WithPinnedSdk.ps1 -GameDir D:\SC
  -Configuration Release`（内部设 DOTNET_ROOT 指向 SDK 9.0.300 后转调 `Build-NetMod.ps1`）全程 exit 0，
  DATABASE VALIDATION PASSED，产物 **496,443 字节**，SHA-256
  `79D88624587E6AAACEB2E1BA1743A0220D7B5E4A4964F33358AE2D14B973F0A8`（提交后重建，DLL 内嵌本分支 HEAD）；
  包内 modinfo 版本 0.23.0。
- 上游资产恢复：环境备注。本工作区 `upstream/SCIE/`（gitignore 的授权第三方资产）曾被清空导致打包在
  「Missing authorized upstream asset」中断；用 `scripts/Restore-UpstreamFromPackage.ps1` 从用户自有上一版
  `SCIEPort.netmod` 还原 SteamBoat.png/Train.png/Cylinder.dae（逐字节同源）与 Minecart.dae（已归一化形态，
  构建对其 pass-through，包内结果与原版推导一致）。该目录不入库，正式环境仍应使用原始授权文件。
- Windows 游戏运行：待验证。本机未启动游戏；新外观在物品栏/世界/骑乘态的显示与旧结论不互相继承。
- Android 7723：待验证。13 部件新模型与 64×64 图集的物品栏图标、世界实体渲染、双人乘坐视觉、不同角度
  辨识度均需真机新世界验证；编译与离线预览通过不得标记为兼容。
- 多人联机：待验证。本次仅改视觉资产，未触碰座位/权威/同步代码；双人同乘显示一致性仍需双机实测。

## 0.23.1 世界绿色偏色修复

- 用户报告：缺陷复现描述。用户反馈「贴图正常，在世界里是绿色的」：物品栏图标与离线预览配色正确，
  但世界实体整车发绿。
- 机理取证：已确认。横向对比六类载具驱动组件的 `ComponentModel.DiffuseColor/EmissionColor`：越野/
  轨道/飞艇/蒸汽船均为 `Vector3.One` + 中性低自发光，坦克为近白 `(0.85,0.9,0.8)`；**仅 ATV** 沿袭旧版
  草绿 `(0.38,0.74,0.16)` + 绿自发光 `(0.04,0.06,0.02)`。世界实例渲染把绑定贴图乘以 DiffuseColor：
  橙 (0.90,0.49,0.13)×绿 → (0.34,0.36,0.02) 橄榄绿、银 (0.78)×绿 → (0.30,0.58,0.13) 中绿，与用户所见
  一致；物品栏图标烘焙进 `BlockMesh` 不乘该色，故「图标正常、世界发绿」。旧四色贴图时代该绿 tint 与
  素材配套，X1 橙/银图集下变为缺陷。
- 修复：已确认。`ComponentAtvDrive` 改 `DiffuseColor = Vector3.One`、`EmissionColor =
  (0.045,0.045,0.05,0)`（与其他载具同基线，保留夜间不纯黑保底）；Release 0 warning / 0 error。
- 构建 / 打包：已确认。`Build-WithPinnedSdk.ps1 -GameDir D:\SC -Configuration Release` 全程 exit 0，
  DATABASE VALIDATION PASSED；产物 **496,553 字节**，SHA-256
  `9AFE494D3F5E1206BAD68DDCE383E8B03DFDC011ADEC75ABB0F66EEA0F0808EB`；包内 modinfo 版本 0.23.1。
- Android 7723：待验证。需真机复验世界配色与图集一致（橙/碳黑/银可读）、夜间低光不纯黑；物品栏图标
  本就不受影响，无需复验。

## 0.24.0 载具激活密码闸（统一激活码 123456，主机校验按会话授权）

- 需求：功能落地。首次对任意 SCIE 载具「生成/驾驶/乘坐/开火」前弹触屏密码框，输入统一码 `123456`
  成功后本会话解锁全部七载具（ATV/蒸汽快艇/越野/飞艇/火车/车厢/T-34）；失败或取消**拒绝操作且不消耗物品**；
  主机与客户端都需授权、按真实玩家会话隔离、由主机校验，并覆盖已在场载具与坦克开火入口防绕过。
- UI ABI 取证：已确认（真实反射）。反射 x25.01.17 `Engine.dll` 得
  `Engine.Input.Keyboard.ShowKeyboard(String,String,String,Boolean,Action<string>,Action)`（public static、
  `passwordMode` 遮蔽）与 `Engine.Time.RealTime`（`Engine.Time` 上的 public static double 属性，作弹窗节流时基）。
  `Test-Environment.ps1` 初版用参数 `FullName` 连写串比对签名，因泛型 `Action<string>` 的 `FullName` 是
  程序集限定形式 `System.Action`1[[System.String, mscorlib, …]]` 而误判「未找到」；改为结构化逐参类型比对
  （`[Action[string]]` 等）后通过。
- 网络 ABI 取证：已确认（真实反射/编译）。原生 `IPackage` ID 连续占用 0..38，SCIE-Port 已用 240/241，
  **242** 空闲→新 `VehicleActivationPackage`；保留段断言扩为 `@(240,241,242)`。反射确认 `PackageManager.RegisterPackage`
  (public static)、`Client.GUID`、`NetNode.Server`、`NetNode.QueuePackage(IPackage)`。`PackageStreamWriter : BinaryWriter`、
  `PackageStreamReader : BinaryReader`，`Write(string)`/`ReadString()` 来自基类；环境脚本在派生类型枚举 `GetMethods`
  会因某未加载 `OpenTK` 重载解析抛异常，故改为断言基类继承+基类方法存在。
- 入口覆盖：已确认（代码/构建守卫）。六处生成 `OnUse` 在 `WorkType.Client` 早退后、`RemoveActiveTool` 前插
  `VehicleActivationService.EnsureActivated()` 拒绝闸；六处驾驶组件在主玩家判定内插同一闸（覆盖已在场载具）；
  坦克开火在 `bool wantsFire = TankRules.CanFire` 前置闸；`TankControlPackage` 服务端在 `RiderId` 校验后加
  `VehicleActivationService.IsGranted(From.GUID)` 门。`Build-NetMod.ps1` 对每处源码断言闸存在且顺序正确。
- 授权模型：已确认（代码/规则）。本地会话键 `Guid.Empty`；客户端绝不自我授权，码经 242 包发主机，主机校验、
  按 `From.GUID` 记账、单播（`To=From`）回执；客户端仅在主机肯定回执后置本地会话为已授权。弹窗包 try/catch，
  异常保持拒绝态不崩溃，节流 6.0 秒防每帧弹。
- 离线测试：已确认（net8 无游戏依赖）。`tests/SCIEPort.VehicleActivation.Tests` **22 断言**全绿（码校验/trim/null/
  空白/反序、节流首次/>/==/</零经过、间隔=6.0、授予/可操作/会话键/常量码）。打包前全工程 12 个离线套件全绿：
  Kelly 11 / ATV 35 / Offroad 20 / Watercraft 16 / Tank / Ecology 47 / Corn 26 / Bamboo 25 / Flax 29 / Sunflower 33 /
  Apple 49 / VehicleActivation 22。
- 编译修复：已确认。首编译报 `VehicleActivationService.cs` 缺 `Log`（Engine 命名空间）、`CommonLib`/`WorkType`/
  `VehicleActivationPackage`（Game.NetWork 命名空间）——服务类误只 `using Game;`；改 `using Engine;`+`using Game.NetWork;`
  后编译通过。
- 构建 / 打包：已确认。`Test-Environment.ps1 -GameDir D:\SC` 通过（含新载具激活 ABI 探测）；
  `Build-WithPinnedSdk.ps1 -GameDir D:\SC -Configuration Release` 全程 exit 0（`TreatWarningsAsErrors`+
  `GenerateDocumentationFile`→0 warning/0 error），`DATABASE VALIDATION PASSED`；产物 **498,017 字节**，SHA-256
  `C40A9C5EF5FE46C8578E4C079E7A461DEB4FBC6F9F96925885AE95D7E4D69213`；包内 modinfo Version=`0.24.0`、
  `SCIEPort.dll` AssemblyVersion=`0.24.0.0`（磁盘复核一致）。
- Windows 游戏运行：待验证。本机未启动游戏；弹窗实际出现与遮蔽、正确/错误码放行/拒绝、取消、已在场载具拦截、
  坦克开火拦截均未运行。
- Android 7723：待验证。目标真机 `Keyboard.ShowKeyboard` 弹窗可达性、密码遮蔽渲染、触屏输入与节流手感均未观测；
  编译/反射签名匹配不得标记兼容；弹窗失败按拒绝回退（安全），可用性待实测。
- 多人联机授权：待验证。主机校验+按 `Client.GUID` 隔离+单播回执为代码/ABI 结论；真实主机+普通客户端两台的
  授权隔离、越权拒绝、未授权远程开火丢弃需双机实测。
## Stormhammer 正式资源与技能（合并前构建 0.23.0，2026-10-26）

- 环境/ABI：已确认。`Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'` 识别程序集版本 2.4.0.0、API 1.42、SC 2.4.10.6、目标 .NET Framework 4.7.2，原生 block 260 个/最大 263；自定义 ID 941 未与原生或 SCIE-Port 已用 ID 冲突。
- 资产生成：已确认。`python SCIE-Port/scripts/New-StormhammerAssets.py` 输出 30 meshes / 1368 triangles，并重建 DAE、1024² diffuse、1024² glow mask、256² effects atlas、三段 44.1 kHz PCM16 WAV 和两张真实几何/UV 软件预览。
- 目标模型导入：已确认。构建内直接调用目标 `Engine.Media.ModelData.Load(Stream)`，得到 1 bone / 30 meshes；逐 geometry 验证 triangles/positions/UV，三角数必须为 1368，六挂点齐全。
- 贴图/音频校验：已确认。三张 PNG 尺寸锁定，主图集 8px 抽样必须 ≥350 色；三 WAV 文件均 >4096 bytes 且 RIFF/WAVE 签名正确。主贴图、glow、effects 均由真实 `ContentManager.Get<Texture2D>` 路径加载。
- 自动回归：已确认。Kelly 11、ATV 32、越野车 20、快艇 16、Tank、Ecology 47、Corn 26、Bamboo 25、Flax 29、Sunflower 33、Apple 49 全绿；DATABASE VALIDATION PASSED。
- Release：已确认。`Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` exit 0，0 warning / 0 error；DLL/FileVersion `0.23.0.0`，ProductVersion `0.23.0+fdddd798...`。
- `.netmod`：已确认。68 个包条目（包含 Stormhammer DAE、3 PNG、3 WAV、CSV、XDB、DLL），781,455 bytes，SHA-256 `0B2BD38528E5226802DB354D377DA92DF89130E60383EC61F4EB4E48AE799FB0`。
- 安全：已确认（代码）。召雷与链电不调用地形、爆炸或天气雷击 API；Client 不写伤害/耐久；所有粒子有固定数量和生命周期，GlowPoint 停止绘制后两帧移除。
- Windows 游戏运行：待验证。本轮没有启动客户端，软件预览不是游戏截图。
- Android 7723：待验证。库存辨识、第一/第三人称手部贴合、掉落比例、光照、音频、10 分钟性能与触屏蓄力均需真机验证。
- 多人联机：待验证。未实现技能专用请求/VFX 包；客户端召雷明确拒绝，主机执行后的生命同步和远端特效可见性需双设备验证。
# 2026-10-26 — 苍穹之怒 V2.0（合并前构建 0.24.0）

- 环境：`SCIE-Port/scripts/Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'`，exit 0；程序集 2.4.0.0、API 1.42、SC 2.4.10.6、net472，原生块常量 260 个。
- 构建：`SCIE-Port/scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release`，exit 0；Release 0 warning/0 error；全部既有离线规则测试通过；DATABASE VALIDATION PASSED。
- 资源：目标 Engine 导入觉醒锤 1 bone/39 meshes/1604 tris，雷矛 1 bone/5 meshes/108 tris；四张觉醒 PNG 尺寸、UV 基数、挂点和技能图标三槽通过。
- 产物：`SCIE-Port/artifacts/SCIEPort.netmod`，76 条目，2,198,186 bytes，SHA-256 `7AF8909F5373B5A741CDF4943C603507CAA17FDD132ED7CC8C5A2E012A5EF013`。
- Windows 游戏、Android 7723、触屏、多点触控、性能、主机/普通客户端/观察者均未运行，状态为待验证。

## 2026-10-26 — SCIE-Port 0.27.0 合并同步验证

- Git 同步：已确认。`main` 快进至远端 `a2adeca` 后，保留 0.26.0 直升机/载具激活功能并合入雷神之锤与苍穹之怒；共享加载器同时注册 `VehicleActivationPackage` 和 `StormhammerSkillPackage`，共享打包清单同时包含两组资源。
- 环境/ABI：已确认。`scripts/Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'` exit 0；程序集 2.4.0.0、API 1.42、SC 2.4.10.6、net472，原生块 260 个、最大 ID 263。为允许 PowerShell 反射加载，仅移除了三份本机目标程序集的 `Zone.Identifier`，未修改程序集内容，哈希检查通过。
- Release：已确认。系统 .NET SDK `9.0.314` 执行 `scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` exit 0；全部既有离线规则测试通过，编译 0 warning/0 error，`DATABASE VALIDATION PASSED`。
- 产物：已确认。`artifacts/SCIEPort.netmod` 共 81 条目、2,287,868 bytes，包内 `modinfo.json` Version=`0.27.0`、DLL AssemblyVersion=`0.27.0.0`，SHA-256 `497F5A94B6D5A2D755937236D68A4A6D9848A67464622059F957F2813932E4D9`。
- 固定 SDK 包装器：待验证。`Build-WithPinnedSdk.ps1` 要求 `%LOCALAPPDATA%\Microsoft\dotnet` 中存在固定 SDK，本机该位置不存在；本次使用已安装的系统 SDK 9.0.314 完成相同 `Build-NetMod.ps1` 流程。
- Windows 游戏、Android 7723 与真实多人联机：待验证。本轮仅执行 ABI、离线测试、目标 Engine 资源导入、编译和包校验，未启动游戏或连接设备。

## 2026-10-26 — SCIE-Port 0.28.0 / 苍穹之怒 V3.2

- 目标 ABI 审计：已确认。`ilspycmd 9.1.0.7988` 对 x25.01.17 `Survivalcraft.exe` 反编译确认 `ComponentMiner.Hit/AttackBody`、`ComponentHealth.Injure/NetInjure/Attacked`、HumanModel 六骨骼、`OnModelRendererDrawExtra`、`ChangeSkyColor`、`ViewFogColor` 与 `SubsystemGlow` 的准确签名和调用顺序。
- 环境：已确认。`scripts/Test-Environment.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17'` exit 0；程序集 2.4.0.0、API 1.42、SC 2.4.10.6、net472，原生块 260、最大 ID 263。
- Release：已确认。`dotnet build` 与 `scripts/Build-NetMod.ps1 -GameDir 'E:\SCNET\SCNETx25.01.17' -Configuration Release` 均 exit 0，0 warning/0 error；Kelly 11、ATV 35、越野 20、Watercraft 16、Helicopter 41、Tank、Ecology 47、Corn 26、Bamboo 25、Flax 29、Sunflower 33、Apple 49、VehicleActivation 22 全部通过；`DATABASE VALIDATION PASSED`。
- V3.2 构建守卫：已确认。觉醒 CSV `scieport-skywrath-v3`、恰好两个 HUD 按钮、旧技能标识完全移除、40/25/5 与 12/55 常量、`TargetId/CastId/ServerTime` 均由打包脚本锁定。
- 模型与资源回归：已确认。既有觉醒锤 1 bone/39 meshes/1604 tris、雷矛历史资源 1 bone/5 meshes/108 tris、1024/256/2048 PNG、UV/挂点/音频签名继续通过；V3.2 不把历史雷矛模型用于主动技能。
- 产物：已确认。`artifacts/SCIEPort.netmod` 81 条目、2,292,859 bytes，包内 Version=`0.28.0`、DLL AssemblyVersion=`0.28.0.0`，SHA-256 `B261BC031C8B26B8CBA0D463F2F81457F7D87EEF45AEB35D710B53290A19D66B`。
- Windows 游戏、Android 7723、实际伤害/反伤、触屏、多玩家观察、丢包/延迟、天空恢复和性能：待验证。本轮未启动游戏或连接设备，不能把源码常量与编译通过当作实际扣血和视觉验收。

## 2026-10-26 — SCIE-Port 0.28.1 / 整包物品消失修复

- 故障复现：已确认。将 0.28.0 临时部署至 Windows x25.01.17 后真实启动，日志首先报告 `SCIEPort.dll`“数据包存在冲突”，随后 16 份方块 CSV 全部因找不到自定义 Block 类型失败；这与 Android 上所有新增物品消失的现象一致。
- 根因：已确认。`VehicleActivationPackage` 与 `StormhammerSkillPackage` 同时使用网络包 ID 242，加载器在 `PackageManager.RegisterPackage` 第二次注册时中断 DLL 初始化。
- 次级冲突：已确认。迁移网络包后真实启动发现直升机与普通雷神之锤同占方块 ID 941。保留已发布直升机 941 与苍穹之怒 942，普通雷神之锤迁移到 943；旧测试版中值 941 无法自动判断原意，故不做危险迁移。
- 回归守卫：已确认。`Build-NetMod.ps1` 现在扫描并拒绝重复网络包 ID 和重复 `public const int Index`；`Test-Environment.ps1` 检查原生 ID 与自定义 903–943、995–1000 及网络包 240–243。
- Release / 包：已确认。完整构建 exit 0，0 warning/0 error；全部既有规则测试及数据库校验通过。产物 81 条目、2,292,868 bytes，Version/AssemblyVersion `0.28.1`/`0.28.1.0`，SHA-256 `264D1A793E9DB8F41E0DDB0AFB0A571FAA78286839BD4E496E86F949707A912D`。
- Windows 实际加载：已确认。0.28.1 临时部署后启动到主菜单，日志确认 DLL、全部 XDB 和全部 CSV 正常加载，无数据包冲突或缺失 Block 类型错误；测试结束恢复原安装包。
- Android 7723：待验证。需要用户重新导入 0.28.1，确认创造栏/武器分类和既有世界；技能、伤害、HUD 与多人仍未因本次加载验证而自动转为已确认。
## 2026-10-27 昆古尼尔 V4 候选

真实Release编译0警告0错误；根环境脚本在Windows PowerShell5.1通过；SCIE环境/模型导入/XDB/88项昆古尼尔纯规则及包内容检查通过。首次完整构建因远端基线引用但未提交`tests/SCIEPort.Stormhammer.Tests`而失败；项目所有者随后明确要求忽略该测试，构建门槛已移除并重新完整执行。Windows游戏/Android/主机/普通客户端均未测试。具体命令、实现缺口与验收步骤见[GUNGNIR-V4-AUDIT.md](GUNGNIR-V4-AUDIT.md)。

最终`SCIEPort.netmod`：3,027,538字节；SHA-256 `ADE009B962DE2935320554B6DB0ACD9212351B7C522A4A767F70AFB76B8B1793`；DLL与modinfo版本均为0.31.0。
