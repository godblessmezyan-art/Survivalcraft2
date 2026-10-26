# 雷神之锤·妙尔尼尔 V6 —— 造型回归、瞬时神罚与雷霆压制

状态：**离线已确认 / 真机待验证**。分支 `feature/mjolnir-v6`，基线 0.32.0，目标运行时 SCNET `Net_x25.01.17`（Android 7723）。

## 1. 本轮改了什么

| # | 需求 | 实现 |
|---|---|---|
| 1 | 沿用雷神之锤建模，外观加炫酷效果，大招更爆裂 | 觉醒武器改为直接加载 `Models/Stormhammer` + `Textures/Stormhammer`；觉醒身份改由**实时雷光**承担：锤头环绕光点 + 环点之间的真实电弧，超载时转速、亮度、放电频率全面上调 |
| 2 | 改名 | 雷神之锤 → **雷神之锤·伪**；苍穹之怒 → **雷神之锤·妙尔尼尔** |
| 3 | 大招瞬时落雷 + 提高巨雷频率 + 减少小技能 CD | 主雷从 4.85 s 提前到 **0.28 s**；新增**重雷层**（每场 5/8/11 道全尺寸分叉巨雷）；雷霆万钧冷却 12 s → **8 s** |
| 4 | 去掉 HUD 技能名与蓝框 | 按钮只保留图标与冷却数字，描边改为透明、厚度 0 |

## 2. 造型：为什么换回雷神之锤

`StormhammerAwakenedBlock` 原本加载 `Models/StormhammerAwakened`（39 网格 / 1604 三角面 / 1024 贴图）。玩家反馈更喜欢第一形态的外观，因此：

```csharp
Model model = ContentManager.Get<Model>("Models/Stormhammer");
s_texture = ContentManager.Get<Texture2D>("Textures/Stormhammer");
```

尺寸系数从 `0.245` 对齐到基础形态的 `0.265`，握持比例与图标取景保持一致。`Models/StormhammerAwakened.dae` 已从包内移除并从模型契约中删除（它已无任何加载点）；`Textures/StormhammerAwakened.png` 保留，因为雷矛拖尾粒子仍在用它。

**觉醒感不再靠换模型**，而是靠下面这套实时效果。

## 3. 锤体雷光（`StormhammerVisuals`）

`DrawBlock` 每帧被调用，因此在其中维护受控数量的发光点（沿用文件里已有的做法，最多 24 个状态）：

- **锤头主光**：沿用原有单点，超载时 1.45 倍、颜色转白蓝。
- **环绕光环**：2（低画质）/ 3（中高画质）个光点绕锤头旋转，半径 `0.105 → 0.075`（随超载收紧），转速 `4.2 → 7.4 rad/s`。
- **环间电弧**：通过 `LightningScene` 生成真实带状雷电（不是粒子），按环顺序相连；超载时额外补一条对角弧，间隔 `0.17 s → 0.08 s`（低画质 `0.34 / 0.18`）。

放电节流是刻意保守的：这段代码跑在绘制路径里，逐帧生成会瞬间占满 48 条闪电池，把技能本身的雷电淹没。

## 4. 大招：从"排队等待"改为"立刻砸下"

### 时间轴

| 时刻 | 事件 |
|---|---|
| 0.00 s | 施法：天空转暗、落点符文开始扩张、低频雷鸣、镜头轻震 |
| 0.02 s | 落点预警符文（服务端事件，客户端播） |
| **0.28 s** | **终焉主雷落地**，同帧结算一次权威范围伤害 |
| 0.55 s – 11.20 s | **重雷层**：5/8/11 道全尺寸分叉巨雷（按画质） |
| 1.28 s 起 | 4 次余震，每次 18 点，间隔 1 s |
| 10.80 s | 大招结束，天空 1.6 s 内恢复 |

`MainStrikeTime` 与伤害结算共用同一常量，视觉与扣血不会错位。

### 重雷层

`LightningRules.BuildGreatBoltSchedule(seed, first, last, count, minDistance, maxDistance)`：

- 时间按 `(i + 0.25 + 0.5·rand)/count` 铺满窗口，**开局即有一道，收尾前还有一道**；
- 方向锥 ±34°、距离 8–18 格、起点高于施法者 30–46 格；
- `StormStrike.Great = true`，`SpawnBackdropStrike` 用 `Main` 形状族生成：芯宽 1.15 / 辉光 3.10 / 4 条分叉 / 落地爆闪 + 符文 + 冲击粒子 + 独立雷鸣；
- 完全由 cast id 决定，两端渲染同一套节奏；**重雷层在主雷落地后才开始消费**（`state.MainSpawned`），避免开场巨雷盖过主雷。

重雷是**纯视觉**，不造成伤害：伤害仍然只有主雷一次 + 余震四次，不新增伤害源。

### 密度阶段

旧的"屏息 4.10–4.85"是为 4.85 s 主雷设计的铺垫，现在没有铺垫可做了，改为开场即高潮：

| 阶段 | 窗口 | 近 / 远（高画质） |
|---|---|---|
| 起势 | 0.05–1.10 | 4 / 10 |
| 压制 | 1.10–3.20 | 7 / 26 |
| 高潮 | 3.20–6.60 | 10 / 40 |
| 长尾 | 6.60–11.60 | 7 / 28 |

天空闪烁从"只到 6.6 s"延长到覆盖整个 11.6 s 窗口。

## 5. 小技能

`JudgmentCooldown` 12 s → **8 s**。伤害表、0.42 s 权威蓄力、单次结算、每个目标一次主伤害全部不变；雷神降世期间仍然 ×2，且只在唯一入口 `DealElementalPower` 上乘一次。

## 6. HUD

- 删除技能名 `LabelWidget`，按钮高度从 `size + 24` 收回 `size`。
- `OutlineColor = Color.Transparent`、`OutlineThickness = 0`，冷却态不再画青色描边，只做图标压暗 + 数字。
- 「雷神降世 Ns」计时条保留（它不是技能名，是状态提示）。

## 7. 验证

```text
dotnet run --project tests/SCIEPort.Stormhammer.Tests   -> 667 断言
Build-NetMod.ps1 -GameDir E:\SCNET\SCNETx25.01.17 -Configuration Release
```

新增断言覆盖：重雷数量、时间排序、窗口边界、距离与角度锥、`Great` 标记、同 cast id 逐位复现、不同 cast id 改变节奏、零数量与空窗口、天幕落雷不得被标记为重雷。

新增构建守卫：`JudgmentCooldown = 8f`、`MainStrikeTime = 0.28f`、`BuildGreatBoltSchedule` 存在且被 `MainSpawned` 门控、觉醒块必须加载 `Models/Stormhammer` 且不得再引用 `Models/StormhammerAwakened`、`DriveCorona` / `GetScene` 存在、HUD 不得出现技能名与描边、两个 CSV 的中文名必须正确。

> 注意：`scripts/Build-NetMod.ps1` 现在带 UTF-8 BOM。原因是中文名守卫在无 BOM 的 BOM-less 脚本里被 Windows PowerShell 5.1 按 ANSI 解码成乱码并导致解析失败——这不是风格选择，是必须项。

## 8. 待验证

- 真机观感：锤体光环在手持视角的大小与位置、电弧密度是否过密、低画质下的帧率；
- 大招：0.28 s 落雷的实际手感、11 道重雷是否过载、与主雷的视觉层次；
- 联机：重雷为本地演出，不同步逐道落雷；主雷与伤害仍走权威事件；
- 未启动游戏、未做双机测试、未在 7723 上跑过帧耗时。
