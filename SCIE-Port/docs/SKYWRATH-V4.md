# 苍穹之怒 V4.0 —— 雷霆万钧与万象雷劫重制

目标运行时 SCNET `Net_x25.01.17`，Android / 7723，产物 `SCIEPort.netmod`。版本 `0.30.0`。

状态：**源码/编译/离线规则/打包已确认；Android 真机表现待验证**。

## 一、审计：为什么旧版只有"蓝色光团"

真实代码路径与根因：

| 位置 | 旧实现 | 结果 |
|---|---|---|
| `StormhammerVisuals.EmitArc` → `StormhammerParticleSystem.CreateArc` | 沿起终点均匀撒 18/30/44 个**相机朝向粒子**，尺寸 `0.035–0.075`（个位数厘米级），中间夹几个更小的"核心"点 | 视觉上就是一条**由小亮点组成的虚线**，没有连续带状几何，远看只剩一团蓝光 |
| `AwakenedStormhammerVisuals.EmitSkyBolt` | 主雷也只是 `EmitArc(target + 17..28 格上空, target)`，再配 3–8 条同样的粒子弧 | 没有比人高的柱状雷电，更没有纵向巨柱 |
| `SubsystemAwakenedStormhammerBlockBehavior.StartUltimate` | 锁定 24 个目标，逐个 `WarningTime/StrikeTime` 落 130 伤害 | 伤害分散、单体收益低，视觉上是"24 处小闪"，总伤害不如二技能 |
| `UltimateMainStrike` | `distance <= 2.2f ? 220 : 100`，半径 7 | 内外圈无过渡，边缘收益断崖 |
| `ChangeSkyColor` | 单一 `Lerp` 到深紫，`GetStormBlend()` 对所有项目全局生效 | 只变暗无"维持期"、无落雷补光；且任何客户端都可能被染色 |

引擎侧确认可用（均从 x25.01.17 实际程序集反编译取证）：

- `Game.SubsystemDrawing` + `Game.IDrawable`：任意 `Subsystem` 实现 `IDrawable` 即可被 `Project.FindSubsystems<IDrawable>()` 自动注册（`Project.Load` 先构造全部子系统再 `Load*`，顺序安全）。
- `Engine.Graphics.PrimitivesRenderer3D.TexturedBatch(Texture2D, useAlphaTest, layer, DepthStencilState, RasterizerState, BlendState, SamplerState)`。
- `TexturedBatch3D.QueueQuad(p1..p4, uv1..uv4, color1..color4)`，配合 `BlendState.Additive`、`DepthStencilState.DepthRead`、`RasterizerState.CullNone` 即可绘制双面加色带。
- `SubsystemSky.CalculateFog / ViewFogColor / VisibilityRange`，与 `SubsystemGlow.Draw` 同一套雾融合做法。
- `ComponentBody.ApplyShaking(float)`：原生受控镜头/身体抖动。
- `SubsystemAudio.PlaySound(name, volume, pitch, position, minDistance, autoDelay)`。

结论：引擎**完全支持**自定义动态顶点与加色面片，旧版不是能力不足，而是从来没有构造过连续几何。

## 二、Lightning Geometry System

新增两个文件，几何与规则分离：

### `Features/Stormhammer/LightningRules.cs`（纯逻辑，可离线测试）

- `BuildPath(...)`：把起终点切成 `segments` 段，用**相干值噪声**在两条垂直轴上扰动（粗 0.68 + 细 0.32 + 高频折角 0.16），端点强制锚定。
  - 相干噪声而不是逐点随机 → 折线**连续且有结构**，不会退化成散点云。
  - 自写 xorshift32 + 值噪声，**同 seed 逐位可复现**，跨客户端一致（不用 `Engine.Random`/`System.Random`）。
- `Taper`、`LifeAlpha`（攻击 6% / 保持 / 释放 42%）。
- 伤害表：`JudgmentDamage`、`TribulationDamage`、`AftershockDamage`。
- `BuildStormSchedule`：天幕落雷的时间/方位/距离/高度/种子，按时间排序，`DueStrikes` 用于掉帧时按时间轴跳过，而不是补播。

### `Features/Stormhammer/LightningScene.cs`（渲染）

- 固定 48 条 `LightningBolt` 池 + 12 个 `LightningRune`，**运行期零分配**（路径缓冲预分配）。
- 每条雷绘制两层带：**白蓝亮芯**（宽度 ×0.46）+ **青蓝辉光**（全宽），主雷额外绘制一张垂直交叉面 → 侧移视角下仍有体积。
- 每段按"相机朝向"展开四边形：`side = normalize(cross(axis, toCamera))`，逐点宽度按 `Taper(1 → 0.45)` 收细。
- **角宽下限**：`max(width, distance × 0.0035 × k)`，保证 50 格外仍有清晰轮廓，不会缩成一条线。
- 深度偏移对齐 `SubsystemGlow` 的 z-bias，避免与地面 z-fighting。
- 雾融合：`Color.LerpNotSaturated(color, sky.ViewFogColor, fog × 0.85)`，alpha 随雾衰减。
- 地面符文：`LightningRune.png` 平面四边形，半径按 `1-(1-p)²` 扩张。
- 命中爆闪：`Textures/RoundGlow` 加色面片，0.22 s 内从 2.4× 扩到 8.9× 后消失。
- 每帧四边形预算：高画质 1400，按画质缩放（中 700 / 低 350）。

新增资源：`Assets/Textures/LightningBeam.png`（128×16 横向渐变截面）、`Assets/Textures/LightningRune.png`（256×256 符文阵）、`Assets/Audio/Stormhammer/Strike.wav`（爆裂+低频冲击，1.65 s）、`Rumble.wav`（低频雷滚，3.30 s）。全部由 `scripts/New-StormhammerV4Assets.py` 可重复生成。

## 三、二技能：雷霆万钧

| 项 | 值 |
|---|---|
| 冷却 | 12 s（不变） |
| 蓄力 | 0.42 s（权威端计时，蓄力结束才结算伤害） |
| 半径 | 9 格 |
| 伤害 | ≤4 格 110；4→9 格线性降到 70；>9 格 0 |
| 单次上限 | 每个目标每次施法只结算一次（`HashSet` 去重） |
| 击退 | 沿用 `ComponentMiner.AttackBody` 的原生冲击 |

视觉：蓄力期锤头聚光（4 条短电弧 + 地面小符文 + 火花）→ 释放时 10–12 条**粗壮径向雷电**（芯宽 0.52、辉光 1.45，各 2 条分叉）从胸口射向 8.4 格外的地面 → 双层地面符文 + 中心向上雷柱爆闪 → 粒子火花仅作点缀。

## 四、三技能：万象雷劫 V4.0

### 时间轴（权威端与客户端同一条时间线）

| 时间 | 事件 | 伤害 |
|---|---|---|
| 0.00 | `UltimateStart`：冻结前向，地面落点符文，低频蓄能声 | — |
| 0.35 | 天空开始转深靛蓝/紫黑（1.25 s 淡入） | — |
| 1.15–4.35 | 天幕雷暴（近景 10 + 远景 26 道） | 无 |
| 4.25 | `UltimateMainWarning`：落点符文扩张到 9.5 格 | — |
| 4.85 | `UltimateMainStrike`：巨雷落地 | **一次性**结算 |
| 5.85 / 6.85 / 7.85 / 8.85 | `UltimateAftershock` | 每次 18，共 4 次 |
| 10.8 | `UltimateEnd`，1.4 s 内恢复天空/雾 | — |

### 主雷伤害

| 距离 | 伤害 |
|---|---|
| ≤2.5 格 | 220 |
| 2.5→5 格 | 220 → 120 |
| 5→8 格 | 120 → 70 |
| =8 格 | 70（边界含） |
| >8 格 | 0 |

玩家/友军默认不受伤（沿用 `WorldSettings.IsFriendlyFireEnabled` 判定）；不破坏方块、不生成火焰、不调用 `Block.DestroyCell` 或炸药桶逻辑——伤害走 `ComponentMiner.AttackBody`，与普攻同一条既有权威链路。

### 主雷视觉

- 起点高度：`max(落点地面 + 34, 落点 + 22)`，上限 250；先用 `Terrain` 扫描该列真实地表，再抬头，保证"从天而降"的纵向感。
- 芯宽 **1.95**、辉光 **4.6**，18 段折线 + 5 条多段分叉（长度/方向/下坠各不相同），寿命 0.95 s，55 ms 重新抖动一次。
- 落地：主雷爆闪 + 两层巨型地面符文（11.5 / 7.6 格）+ 两层粒子冲击环 + 34 格内玩家受控抖动 0.85 + 天空亮度冲击 1.0。

### 前方万雷天幕（纯视觉）

- 只在**施法者本机**生成（`IsLocalCaster` 判定 `PlayerData.IsMainPlayer`），其他客户端只看到真实主雷与爆炸。
- 朝向在施法瞬间冻结（`UltimateStart.Direction`），镜头转动不会让整片雷暴瞬移。
- 近景层：15–25 格、±43°、10 道、芯宽 0.30、有分叉、每道带闪光与错峰雷声（音量 0.34，避免齐鸣炸麦）。
- 远景层：25–55 格、±49°、26 道、芯宽 0.13、无分叉、寿命 0.34 s，密集但廉价。
- 不为特效加载区块：目标列未驻留时，雷在"空中安全点"终止，绝不查询未加载地形生成落点。
- 掉帧策略：`DueStrikes` 按时间轴推进游标，过期的不补播；池满时回收最旧一条。

## 五、天地异变

- `ChangeSkyColor`：`GetStormBlend()` 控制深靛蓝→紫黑渐变，`GetFlash()` 叠加落雷补光（`ReducedFlashes` 时压到 35%）。
- `ViewFogColor`：冷雾同步 + 闪光提亮（仅在水下/岩浆下之外）。
- **染色只对施法者本机生效**：`GetStormBlend()` 过滤 `LocalCaster`，旁观者不会被染成紫天。
- 结束：`UltimateEnd` 后 1.4 s 内 `End` 提前，混合自然回落；`Reset(Project)` 在子系统 `Dispose` 时清空全部状态，防止天空卡紫。
- 未修改世界天气、时间、地形。

## 六、联机

- 二技能伤害与冷却：权威端 `AcceptRequest` → 0.42 s 后结算 → `Publish` 广播 `JudgmentBurst`。
- 三技能落点：权威端 `ResolveAimPoint`（准星射线 ≤16 格 → 逐格前进找可站立列且不穿墙 → 兜底前方 12 格）。
- 主雷伤害只结算一次：`MainStruck` 标志 + 服务器单点执行。
- 天幕：客户端本地生成，不广播、不查询目标、不调用 `AttackBody`、不产生服务器事件。
- 随机种子来自广播的 `CastId`（`LightningRules.Mix(castId, …)`），因此同一场施法在所有客户端几何一致。
- 客户端视觉不参与任何伤害判定。

## 七、验证

| 项 | 结果 |
|---|---|
| 离线规则 | `SCIEPort.Stormhammer.Tests` **277 断言**通过（路径锚定/连续性/位移上限/确定性/寿命曲线/宽度收细/两张伤害表/天幕分带与排序/DueStrikes） |
| 编译 | net472 Release **0 error / 0 warning** |
| 全量回归 | Kelly 11 / ATV 35 / Offroad 20 / Watercraft 16 / Helicopter 41 / Tank / Ecology 47 / Corn 26 / Bamboo 25 / Flax 29 / Sunflower 33 / Apple 49 / VehicleActivation 22 / PortableFortress 291 / **Skywrath V4 277** |
| 数据库 | 15 个 XDB `DATABASE VALIDATION PASSED` |
| 新增构建守卫 | 拦截 `UltimateTarget` 回潮、拦截 `EmitSkyBolt` 回潮、强制 `QueueRibbon`/`LightningRune`/`BuildStormSchedule`/`UpdatePendingJudgment`/`TribulationDamage` 存在、贴图尺寸、音效 RIFF 头 |
| 打包 | `artifacts/SCIEPort.netmod` **2,648,230 字节 / 91 条目**，SHA-256 `D2EE8300C12BC21DC69A5EBC0D5516B2EC8B0ADE146D738CC18B1A21CEDA3BFA`，modinfo `0.30.0` |
| Windows 游戏运行 | 待验证 |
| Android 7723 | 待验证 |
| 联机双机 | 待验证 |

## 八、Android 7723 验收步骤

1. 备份存档，新建可丢弃创造世界；装入 `SCIEPort.netmod`（`NetMods`），日志应出现 `[SCIEPort]` 初始化。
2. 拿出苍穹之怒，确认 HUD 冷却、全身雷光、普攻双段伤害与 5 点反伤仍在（回归项）。
3. **二技能**：对准空地按 2 → 0.42 s 蓄力后应看到 10–12 条**有主干、有分叉、有白芯的粗雷**从身体射向四周地面，而不是一圈亮点；怪物血量骤降且**不会掉两次**。
4. **三技能**：对准前方地面按 3 →
   - 0.3 s 内天空转深靛蓝，前方 15–55 格外开始错峰落雷（只有你能看到这片天幕）；
   - 4.25 s 落点符文扩张；
   - 4.85 s 一条**远高于人的曲折巨柱**砸下（芯白、外围青蓝、带分叉），落地爆闪 + 双层符文；
   - 8 格内怪物按 220/120/70 掉血，之后 4 次 18 点余震；
   - 10.8 s 后天空在 1.4 s 内恢复。
5. 站到 20 格外看别人放大：应能看到**真实主雷与爆炸**，但**看不到**那片第一人称天幕，天空也不应被染色。
6. 帧率记录：二技能释放瞬间、三技能 1.15–4.35 s 天幕段、主雷落地帧。
7. 掉帧测试：开后台下载/低电量模式，确认天幕是"跳过"而不是"补播成一片糊"。
8. 负例：前方是墙时主雷不应穿墙落到墙后；落点在未加载区块方向上时不应出现悬空雷。

## 九、已知限制

- 主雷宽度/亮度是初始美术值，必须按真机截图调（`AwakenedStormhammerVisuals.EmitMainStrike` 里 `1.95 / 4.6`）。
- 天幕密度按画质分档（高 10+26 / 中 8+18 / 低 5+10），低端机可能需要再降。
- 天幕只覆盖 ±43°/±49°，是"前方万雷"而非 360° 雷暴（按需求）。
- 第三人称看到的主雷与第一人称一致，但**天幕不可见**（设计如此）。
- 余震固定 4 次，不做叠加；若真机帧率极低导致单帧跨越多拍，会一次补齐但绝不重复生成爆炸。
- 未实现：镜头闪烁时长/强度滑条（沿用现有 `ReducedFlashes` / `CameraShake` 开关，设置项在 `SaveSettings`/`LoadSettings` 中持久化）。
- 未做真机与双机测试，以上"已确认"仅指源码、编译、离线规则与打包。
