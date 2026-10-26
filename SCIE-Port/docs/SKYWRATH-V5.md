# 苍穹之怒 V5.0 —— 万象雷劫「雷神降世」

目标运行时 SCNET `Net_x25.01.17`，Android / 7723，产物 `SCIEPort.netmod`，版本 `0.31.0`。

状态：**源码 / 编译 / 离线规则 / 打包已确认；真机表现与联机待验证**。

V4.0 的带状雷电、巨型主雷、前方万雷架构全部保留，本轮只是把「放完大招收工」改成「放完大招进入 12 秒强势期」。

## 一、改动文件清单

| 文件 | 改动 |
|---|---|
| `Features/Stormhammer/SubsystemAwakenedStormhammerBlockBehavior.cs` | 新增 `ThunderGodState`（12 s、按实体 id 单实例）、大招激活/过期/清理、雷霆万钧 CD 刷新、雷电伤害 ×2 通道、伤害日志改造 |
| `Features/Stormhammer/AwakenedStormhammerVisuals.cs` | 天幕延长到 12 s 并改为五段密度、超载人物电弧与足迹雷纹、强化版雷霆万钧视觉、主雷加粗 + 环形冲击波 |
| `Features/Stormhammer/LightningRules.cs` | 新增 `StormPhase` 与分段调度 `BuildStormSchedule(seed, phases)`，共用排序 |
| `Features/Stormhammer/StormhammerSkillPackage.cs` | 新增 `ThunderGodStart = 12`、`ThunderGodEnd = 13` |
| `Features/Stormhammer/StormhammerSkillHud.cs` | 新增「雷神降世 Ns」计时文字与剩余条 |
| `Features/Stormhammer/StormhammerVisuals.cs` | 锤头能量核在超载期转白蓝高亮并放大 |
| `scripts/Build-NetMod.ps1` | 新增 6 条 V5 守卫（见第六节） |
| `tests/SCIEPort.Stormhammer.Tests/Program.cs` | 新增分阶段天幕断言（277 → **580**） |
| `src/SCIEPort/modinfo.json`、`SCIEPort.csproj` | 版本 0.31.0 |

## 二、数值与状态机

```
UltimateStart (t=0)
  ├─ ThunderGodState{ EndTime = now + 12, JudgmentBoostUsed = false }
  ├─ 雷霆万钧 CD := 0（只刷新这一个，且每次大招只刷一次）
  └─ 广播 ThunderGodStart(Value = 12)
t = 4.85  主雷落地（**不享受本次增幅**）
t = 12.0  EndTime 到期 → 广播 ThunderGodEnd → 状态移除
```

| 伤害来源 | 基础值 | 增幅 | 说明 |
|---|---|---|---|
| 普攻物理 | 40 | **×1** | 走 `PrepareAwakenedMelee`，永不进增幅通道 |
| 普攻雷电追加 | 25 | **×2** | `DealElementalPower(..., multiplier)` 唯一入口 |
| 雷霆万钧 | 110 → 70 | **×2** | `DealAttackPower(..., multiplier)` |
| 固定反伤 | 5 | **×1** | 显式传 `1f`，日志可核 |
| 主雷 | 220 / 120 / 70 | **×1** | 显式不传 multiplier，避免自我增幅 |
| 余震 | 18 × 4 | **×1** | 同上 |

- 状态按 `EntityId` 存字典，**再次施法只覆盖 EndTime，不累加**，所以倍率永远只作用一次。
- 死亡、退出世界、断线、卸下武器：`UpdateThunderGod` 每帧校验 `IsAddedToProject / Health > 0 / IsEquipped`，任一不满足即广播 `ThunderGodEnd` 并移除；子系统 `Dispose` 清空字典与视觉状态。
- 客户端只接收 `ThunderGodStart/End` 做演出，不参与伤害与冷却判定。

## 三、视觉

**人物超载（`TouchAvatar` + `s_overcharge`）**
- 常规：7 个雷光锚点 + 每 0.16 s 一条细电弧。
- 超载：电弧间隔压到 0.42 倍、芯宽 0.13 / 辉光 0.36、额外两条跨肩→髋与双手之间的**连续分叉电弧**；每 0.42 s 在脚下生成 0.95 格雷纹 + 一条落地电弧。
- 第一人称看得到手臂与武器周边电弧（锚点在第 2/3 个手骨上），第三人称看到完整轮廓。
- 锤头核心：`IsOverchargedNear` 让 `StormhammerVisuals.TouchModelGlow` 在 2.5 格内检测到超载玩家时，颜色转 `196,232,255` 并放大 1.45 倍，与身体雷光同步呼吸。

**雷霆万钧强化（仅首次）**
- 服务端在结算时判定 `JudgmentBoostUsed`，通过 `JudgmentBurst.Step = 1` 通知客户端。
- 视觉：12 → 14 条、芯 0.52 → 0.70、辉光 1.45 → 1.95、分叉 2 → 3、额外 10.5 格符文、火花与抖动加强。**伤害仍走统一 ×2，不二次叠加**。

**巨型主雷**
- 芯 1.95 → **2.45**，辉光 4.6 → **5.6**，分叉 5 → **7**，寿命 0.95 → 1.05 s。
- 新增两圈**纯视觉**冲击波符文（15.5 / 10.0 格）与一圈 15.5 格粒子环；不产生任何伤害、不碰方块。
- 伤害仍是单次 220/120/70，落地只结算一次。

**前方万雷天幕（12 s 五段密度）**

| 阶段 | 时间窗 | 近景 | 远景 |
|---|---|---|---|
| 起势 | 1.15–2.70 | 5 | 18 |
| 蓄压 | 2.70–4.10 | 6 | 26 |
| 屏息 | 4.10–4.85 | 1 | 4 |
| 高潮（主雷落地） | 4.85–6.80 | 9 | 44 |
| 长尾 | 6.80–11.60 | 6 | 26 |

高画质合计 145 道；中画质 ×0.62、低画质 ×0.34 自动缩放。仍是本地确定性生成、冻结朝向、不加载区块、掉帧跳过。

## 四、天地异变

- 时长与 12 s 强势期一致：`StormReleaseTime = 12.0f`，`End = start + 12 + 1.6` 淡出尾巴。
- `GetStormBlend` 仍只对 `LocalCaster` 生效；`GetFlash` 由每次落雷累加，`ReducedFlashes` 时压到 35%。
- 多重视觉叠加用 `1-(1-a)(1-b)` 合成，`EndStorm` 取 `min` 提前收束，不会互相打架；`Reset` 兜底防天空卡紫。

## 五、联机

- 增幅、CD 刷新、主雷落点与全部伤害：权威端 `AcceptRequest → ActivateThunderGod / UpdateUltimate`。
- 广播内容：`CastId`、施法者 id、落点、朝向、开始时间（`ServerTime`）、`ThunderGodStart.Value = 12`；背景雷一条都不过网。
- 主雷与余震只结算一次（`MainStruck` + `AftershockTicks < 4`）；异常中断/死亡发布 `UltimateEnd` 并清状态。
- 客户端 `WorkType.Client` 在 `Update` 里提前返回，不执行任何伤害分支。

## 六、构建守卫（新增）

1. `ThunderGodDuration = 12f`
2. `ThunderGodPowerMultiplier = 2f`
3. `SetCooldown(id, StormhammerSkill.ThunderousJudgment, 0f);` 存在（且不是刷新大招 CD）
4. 物理伤害日志必须带 `PhysicalMeleePower, 1f`
5. 反伤日志必须带 `RetaliationPower, 1f`
6. 主雷/余震调用不得出现 `ThunderGodPowerMultiplier`
7. `StormReleaseTime = 12.0f`、`s_overcharge`、`NextFootstep`、`BuildStormSchedule(seed, phases)`、HUD `GetThunderGodRemaining`

## 七、验证

| 项 | 结果 |
|---|---|
| 离线规则 | `SCIEPort.Stormhammer.Tests` **580 断言**（含五段密度：总 145、排序、阶段内计数、高潮 > 屏息×4、长尾 > 10.5 s、确定性、`DueStrikes` 跨阶段） |
| 编译 | net472 Release **0 error / 0 warning** |
| 全量回归 | Kelly 11 / ATV 35 / Offroad 20 / Watercraft 16 / Helicopter 41 / Tank / Ecology 47 / Corn 26 / Bamboo 25 / Flax 29 / Sunflower 33 / Apple 49 / VehicleActivation 22 / PortableFortress 291 / **Skywrath V5 580** |
| 数据库 | 15 个 XDB `DATABASE VALIDATION PASSED` |
| 打包 | `artifacts/SCIEPort.netmod` **2,650,795 字节 / 91 条目**，SHA-256 `B3C5FFBC508F343A800ACF4E9A4E2AF5E3DCECCDD0175420E07AF9390593F3F5`，modinfo `0.31.0` |
| Windows 游戏运行 | **未验证** |
| Android 7723 | **未验证** |
| 双机联机 | **未验证** |

### 伤害日志格式（诊断开关打开后）

```
[SCIEPort Skywrath V5] lightning attacker=12 target=31 base=25 x2 final=50 health=100->98.4
[SCIEPort Skywrath V5] 雷霆万钧 attacker=12 target=31 base=110 x2 final=220 health=100->70.5
[SCIEPort Skywrath V5] physical attacker=12 target=31 base=40 x1 final=40 health=100->96.7
[SCIEPort Skywrath V5] retaliation attacker=31 target=12 base=5 x1 final=5 health=... 
[SCIEPort Skywrath V5] 终焉主雷 attacker=12 target=31 base=220 x1 final=220 health=...
```

`final` 是传入原生系统的值；`health=before->after` 是原生 `Injure/AttackBody` 经护甲与 `AttackResilience` 处理后的**真实生命变化**，两者不相等是正常的（护甲/韧性削减），日志同时给出便于核对。

## 八、真机验收步骤

1. 备份存档，新建可丢弃创造世界，装入 `SCIEPort.netmod`，日志出现 `[SCIEPort]`。
2. 确认回归项：普攻双段伤害、全身雷光、5 点反伤、HUD 冷却、雷霆万钧单放（无增幅时伤害为旧值）。
3. 按住 3 放大招：**立刻**看雷霆万钧按钮——冷却应瞬间归零，HUD 出现「雷神降世 12s」并在 12 s 内递减，大招自身按钮仍在 55 s 冷却。
4. 12 s 内按 2：第一次雷霆万钧应明显更粗（14 条、更亮、更大爆心），怪物掉血约为平时的 2 倍；第二次起视觉回到常规、伤害仍 ×2。
5. 12 s 内平砍：物理伤害不变，雷电追加那段翻倍（需开诊断日志核对 `physical x1` 与 `lightning x2`）。
6. 站到怪堆里被打：反伤仍为 5（日志 `retaliation x1`）。
7. 主雷落地：观察白芯/蓝辉光/分叉是否比 V4 更粗，落点是否有两圈向外扩张的冲击波环；**记录怪物血量只掉一次**（`终焉主雷 ... x1`），随后 4 次 `雷霆余震 ... x1`。
8. 观察前方天幕：应在 1–11.6 s 内持续出现，4.1–4.85 s 明显变稀（屏息），4.85 s 后进入最密集阶段。
9. 天空：深靛蓝/紫黑在 0.3 s 内进入并**稳定保持约 12 s**，结束时平滑恢复，不应残留紫色。
10. 12 s 内退出世界/被打死/切武器，再进：天空与雷光必须恢复正常（不应卡在超载或紫天）。
11. 双机：客户端放大招，旁观者应看到主雷与爆炸、**看不到**施法者的天幕、天空不被染色；两端伤害数字应一致。

## 九、已知限制（全部未验证或明确不支持）

- 12 s 强势期的实际手感、超载电弧的视觉密度、HUD 计时在手机上的可读性：**未验证**。
- 五段密度在低端机上的帧率：**未验证**（低画质已降到 0.34 倍，但仍需实测）。
- 主雷冲击波环为**纯视觉**，不产生第二次伤害——这是设计，不是遗漏。
- 「首次雷霆万钧强化」按**每次大招各一次**计；同一大招内多次雷霆万钧只有第一次加粗，伤害始终 ×2。
- 未实现：可调闪烁强度滑条（沿用 `ReducedFlashes` 开关）；`CameraShake` 默认关闭，需在设置里开启才会触发抖动。
- 未做真机与双机测试，以上"已确认"仅指源码、编译、离线规则与打包。
